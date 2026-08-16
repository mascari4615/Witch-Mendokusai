using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;
using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 내려가는 구조가 성립하는가 (TASK-WM-406).
	///
	/// ★ 레퍼런스는 울티마 스쿼드다 — 「스테이지마다 나올 수 있는 등급의 상한」.
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

			double perTarget = tuning.TargetHealthByStage.At(0);
			IdleModel.Step(state, tuning, DamageTimeFor(state, tuning, perTarget * tuning.KillsPerStage));

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

			double near = IdleModel.IncomePerSecond(shallow, tuning);
			double far = IdleModel.IncomePerSecond(deep, tuning);

			Assert.Less(far, near, "20단계 아래가 1단계보다 잘 번다 — 벽이 없다");
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

			Assert.Greater(IdleModel.IncomePerSecond(deep, generous), IdleModel.IncomePerSecond(shallow, generous),
				"보상 배수가 더 큰데도 깊은 쪽이 덜 번다 — 벽이 손잡이가 아닌 데서 오고 있다");
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

		/// <summary>주어진 피해량을 넣는 데 걸리는 시간 — 시험이 「몇 초」가 아니라 「얼마만큼」으로 말하게.</summary>
		private static double DamageTimeFor(IdleState state, IdleTuning tuning, double damage)
		{
			return damage / IdleModel.DamagePerSecond(state, tuning);
		}
	}
}
