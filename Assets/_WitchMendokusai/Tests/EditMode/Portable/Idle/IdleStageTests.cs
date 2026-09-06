using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;
using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 내려가는 구조가 성립하는가 (TASK-WM-406).
	///
	/// ★ 레퍼런스는 대열 방치 전투 계열다 — 「스테이지마다 나올 수 있는 등급의 상한」.
	///   깊이가 관문이라야 <b>같은 자리 반복이 아니라 더 내려가는 것</b>이 성장이 된다.
	///   여기서 지키는 것은 그 구조의 두 기둥이다: ① 경계에서 셈이 안 어긋난다 ② 벽이 실제로 생긴다.
	/// </summary>
	public sealed class IdleStageTests
	{
		private const double TOLERANCE = 1e-6d;

		private static IdleTuning NewTuning()
		{
			return new IdleTuning();
		}

		/// <summary>정해진 수를 처치하면 다음 단계로 내려간다.</summary>
		[Test]
		public void ClearingStage_MovesDeeper()
		{
			IdleTuning tuning = NewTuning();
			IdleState state = new IdleState();

			Assert.AreEqual(1, state.Stage);

			double attacks = IdleModel.HitsToFell(state, tuning) * tuning.KillsPerStage;
			IdleModel.Step(state, tuning, attacks / IdleModel.AttackSpeedOf(state, tuning));

			Assert.AreEqual(2, state.Stage, "정해진 수를 다 처치했는데 안 내려갔다");
			Assert.AreEqual(0, state.KillsInStage, "내려갔는데 이번 단계 처치 수가 안 비었다");
			Assert.AreEqual(2, state.BestStage, "가장 깊이 닿은 단계가 안 따라왔다");
		}

		/// <summary>
		/// ★ 가장 중요한 판 — <b>단계 경계를 넘어가도</b> 60초 한 번과 0.1초 600번이 같다.
		///
		/// 경계에서 체력도 보상도 바뀌므로, 경계를 무시하고 한 번에 나누면 다음 단계 몫을
		/// 이전 단계 값으로 쳐준다. 그 어긋남은 오프라인 보상으로 그대로 새어 나간다.
		/// </summary>
		[Test]
		public void StepInvariance_HoldsAcrossStageBoundaries()
		{
			IdleTuning tuning = NewTuning();

			IdleState atOnce = new IdleState();
			IdleModel.Step(atOnce, tuning, 600d);

			IdleState bitByBit = new IdleState();
			for (int i = 0; i < 6000; i++)
			{
				IdleModel.Step(bitByBit, tuning, 0.1d);
			}

			Assert.Greater(atOnce.Stage, 1, "단계가 안 넘어갔다 — 이 시험이 경계를 안 밟았다");
			Assert.AreEqual(atOnce.Stage, bitByBit.Stage, "쪼개서 밟으니 단계가 달라졌다");
			Assert.AreEqual(atOnce.Kills, bitByBit.Kills, "쪼개서 밟으니 처치 수가 달라졌다");
			Assert.AreEqual(atOnce.Resource, bitByBit.Resource, TOLERANCE, "쪼개서 밟으니 자원이 달라졌다");
			Assert.AreEqual(atOnce.KillsInStage, bitByBit.KillsInStage, "쪼개서 밟으니 단계 안 진행이 달라졌다");
		}

		/// <summary>깊이 갈수록 대상이 단단해지고 보상도 커진다.</summary>
		[Test]
		public void DeeperStage_IsTougherAndRicher()
		{
			IdleTuning tuning = NewTuning();
			IdleState shallow = new IdleState { Stage = 1 };
			IdleState deep = new IdleState { Stage = 6 };

			Assert.Greater(IdleModel.TargetHealthOf(deep, tuning), IdleModel.TargetHealthOf(shallow, tuning));
			Assert.Greater(IdleModel.RewardOf(deep, tuning), IdleModel.RewardOf(shallow, tuning));
		}

		/// <summary>
		/// ★ <b>벽이 실제로 있다</b> — 같은 능력치로 계속 내려가면 초당 수입이 도로 준다.
		///
		/// 이게 없으면 아무 데서나 무한히 내려가고, 올릴 이유가 사라진다.
		/// 방치형에서 「올릴 이유」가 사라지면 남는 건 기다림뿐이다.
		/// </summary>
		[Test]
		public void Descending_EventuallyStarves()
		{
			IdleTuning tuning = NewTuning();
			IdleState shallow = new IdleState { Stage = 1 };
			IdleState deep = new IdleState { Stage = 20 };

			// ★ 층을 가른 뒤로 자원은 기지가 낸다 — 벽은 <b>초당 처치</b>에서 드러난다.
			double near = IdleModel.KillsPerSecond(shallow, tuning);
			double far = IdleModel.KillsPerSecond(deep, tuning);

			Assert.Less(far, near, "20단계 아래가 1단계만큼 잡는다 — 벽이 없다");
		}

		/// <summary>손잡이를 돌려 체력 배수를 보상 배수 아래로 내리면 벽이 사라진다 — 벽의 근거가 그 둘의 대소임을 못 박는다.</summary>
		[Test]
		public void Wall_ComesFromRatioGap_NotFromMagic()
		{
			IdleTuning generous = NewTuning();
			generous.TargetHealthByStage = new GeometricScale(10d, 1.2d);
			generous.RewardByStage = new GeometricScale(1d, 1.5d);

			IdleState shallow = new IdleState { Stage = 1 };
			IdleState deep = new IdleState { Stage = 20 };

			// ★ 벽이 <b>체력 배수</b>에서 온다는 것을 못 박는다 —
			//   손잡이를 완만하게 돌리면 같은 깊이가 훨씬 덜 힘들어야 한다.
			IdleTuning steep = NewTuning();

			Assert.Greater(IdleModel.KillsPerSecond(deep, generous), IdleModel.KillsPerSecond(deep, steep),
				"체력 배수를 낮췄는데도 깊은 쪽이 안 쉬워진다 — 벽이 손잡이가 아닌 데서 오고 있다");
		}

		/// <summary>단계도 저장에 담긴다. 그리고 <b>단계가 없던 옛 저장</b>은 1단계로 들어온다.</summary>
		[Test]
		public void Save_CarriesStage_AndOldSavesLandOnStageOne()
		{
			IdleState state = new IdleState { Stage = 7, KillsInStage = 3, BestStage = 9 };

			IdleState restored = new IdleState();
			restored.Load(state.Save());

			Assert.AreEqual(7, restored.Stage);
			Assert.AreEqual(3, restored.KillsInStage);
			Assert.AreEqual(9, restored.BestStage);

			// 단계 칸이 없던 시절의 저장 — 구조체 기본값이라 0 으로 온다.
			IdleState fromOld = new IdleState();
			fromOld.Load(new IdleSaveData { Resource = 5d, Kills = 2L });

			Assert.AreEqual(1, fromOld.Stage, "옛 저장이 0단계로 들어왔다 — 판이 어긋난다");
			Assert.AreEqual(1, fromOld.BestStage);
		}

		/// <summary>사진에 단계가 실린다 — 표현이 뺄셈으로 지어내지 않게.</summary>
		[Test]
		public void Snapshot_CarriesStageProgress()
		{
			IdleTuning tuning = NewTuning();
			IdleSession session = new IdleSession(tuning);
			session.Advance(30d);

			IdleSnapshot snapshot = session.Capture();

			Assert.AreEqual(session.State.Stage, snapshot.Stage);
			Assert.AreEqual(session.State.KillsInStage, snapshot.KillsInStage);
			Assert.AreEqual(tuning.KillsPerStage, snapshot.KillsPerStage);
		}

		/// <summary>
		/// ★ 「갈 수 있나」와 「가면 되나」가 <b>같은 답</b>을 쓴다 — 두 벌이면 버튼이 거짓말한다.
		/// </summary>
		[Test]
		public void CanGoAndGoing_AgreeAlways()
		{
			IdleState state = new IdleState();
			state.Stage = 10;
			state.BestStage = 30;
			IdleSession session = new IdleSession(new IdleTuning(), state);

			int[] tries = { -5, 0, 1, 9, 10, 11, 30, 31, 999 };

			foreach (int stage in tries)
			{
				IdleState copy = new IdleState();
				copy.Stage = 10;
				copy.BestStage = 30;

				bool said = IdleModel.CanGoToStage(copy, stage);
				bool did = IdleModel.TryGoToStage(copy, stage);

				Assert.AreEqual(said, did, stage + "단계 — 「갈 수 있다」와 실제가 다르다");
				Assert.AreEqual(said, session.CanGoToStage(stage), stage + "단계 화면 답이 코어와 다르다");
			}
		}

		/// <summary>★ 앞질러는 못 간다 — 가장 깊이 간 곳까지만.</summary>
		[Test]
		public void YouCannotGoDeeperThanYouHaveBeen()
		{
			IdleState state = new IdleState();
			state.Stage = 5;
			state.BestStage = 12;

			Assert.IsTrue(IdleModel.CanGoToStage(state, 12));
			Assert.IsFalse(IdleModel.CanGoToStage(state, 13), "가 본 적 없는 데로 보낸다");
			Assert.IsFalse(IdleModel.CanGoToStage(state, 5), "이미 서 있는 자리로 가라고 한다");
		}

		/// <summary>
		/// ★ 「어디서 파는 게 제일 빠른가」를 <b>물어도 판이 안 바뀐다</b> (회귀).
		///
		/// 전에는 판의 단계를 바꿔 가며 이분 탐색하고 마지막에 되돌렸다. 화면이 <b>매 프레임</b>
		/// 부르는 자리라, 그 사이에 무슨 일이 나면 사람이 엉뚱한 깊이에 서 있게 된다.
		/// </summary>
		[Test]
		public void AskingWhereToFarm_DoesNotMoveYou()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Stage = 7;
			state.BestStage = 40;
			state.KillsInStage = 4;
			state.HitsOnTarget = 2L;

			int best = IdleModel.BestFarmingStage(state, tuning);

			Assert.AreEqual(7, state.Stage, "물었더니 자리가 옮겨졌다");
			Assert.AreEqual(4, state.KillsInStage, "물었더니 이번 단계 처치가 달라졌다");
			Assert.AreEqual(2L, state.HitsOnTarget);

			Assert.GreaterOrEqual(best, 1);
			Assert.LessOrEqual(best, state.BestStage, "가 본 적 없는 데를 파라고 한다");
		}

		/// <summary>★ 「그 단계면 몇 대에 잡히나」도 판을 안 건드리고 답한다.</summary>
		[Test]
		public void AskingAboutAnotherStage_LeavesYouWhereYouAre()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Stage = 3;

			double here = IdleModel.HitsToFell(state, tuning);
			double deeper = IdleModel.HitsToFellAt(state, tuning, 30);

			Assert.AreEqual(3, state.Stage, "물었더니 자리가 옮겨졌다");
			Assert.AreEqual(here, IdleModel.HitsToFellAt(state, tuning, 3), 1e-9d,
				"같은 단계를 물었는데 답이 다르다");
			Assert.Greater(deeper, here, "깊은 데가 더 쉽다고 한다");
		}
	}
}
