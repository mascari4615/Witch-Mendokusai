using NUnit.Framework;
using UnityEngine;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 머물지 내려갈지가 <b>진짜 선택인가</b> (TASK-WM-406).
	///
	/// ★ 선택은 <b>양쪽 다 나은 점이 있을 때만</b> 선택이다.
	///   한쪽이 언제나 낫다면 그건 고르는 게 아니라 정답을 아는 것이고, 버튼은 장식이 된다.
	///   그래서 여기서 재는 것은 「눌리나」가 아니라 <b>「양쪽이 서로 다른 것을 준다」</b>이다:
	///   얕으면 빨리 잡아 <b>많이</b>, 깊으면 느려도 <b>좋은 것</b>.
	/// </summary>
	public sealed class IdleHoldStageTests
	{
		/// <summary>머무르면 다 밀어도 안 내려간다.</summary>
		[Test]
		public void Holding_StopsTheDescent()
		{
			IdleTuning tuning = new IdleTuning();

			IdleState going = new IdleState();
			IdleModel.Step(going, tuning, 600d);

			IdleState staying = new IdleState { HoldingStage = true };
			IdleModel.Step(staying, tuning, 600d);

			Assert.Greater(going.Stage, 1, "안 머무는 판이 안 내려갔다 — 시험이 아무것도 안 쟀다");
			Assert.AreEqual(1, staying.Stage, "머무르기로 했는데 내려갔다");
		}

		/// <summary>머무는 동안에도 <b>계속 잡는다</b> — 멈추는 게 아니라 같은 자리에서 버는 것이다.</summary>
		[Test]
		public void Holding_KeepsKilling_NotPausing()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState staying = new IdleState { HoldingStage = true };

			IdleModel.Step(staying, tuning, 600d);

			Assert.Greater(staying.Kills, 0L, "머무니까 아무것도 안 잡는다 — 그건 멈춘 것이다");
			Assert.Greater(staying.Resource, 0d);
			Assert.AreEqual(tuning.KillsPerStage, staying.KillsInStage, "막대가 꽉 찬 채로 멎어 있어야 한다");
		}

		/// <summary>
		/// ★ 핵심 — <b>얕으면 많이, 깊으면 좋은 것.</b> 이 부등식이 무너지면 선택이 사라진다.
		///
		/// 같은 시간을 두 판에 주고 비교한다:
		/// 1단계에 머문 판 vs 계속 내려간 판.
		/// </summary>
		[Test]
		public void ShallowFarming_YieldsMore_DeepDiving_YieldsBetter()
		{
			IdleTuning tuning = new IdleTuning();
			const double HOURS = 6d * 3600d;

			IdleState staying = new IdleState { HoldingStage = true };
			IdleModel.Step(staying, tuning, HOURS);

			IdleState going = new IdleState();
			IdleModel.Step(going, tuning, HOURS);

			long stayingDrops = Total(staying);
			long goingDrops = Total(going);

			int stayingBest = BestTier(staying);
			int goingBest = BestTier(going);

			Debug.Log("[IdleHold] 6시간 — 머묾: " + staying.Stage + "단계 · 떨군 것 " + stayingDrops
				+ "개 · 최고 " + stayingBest + "등급  ||  내려감: " + going.Stage + "단계 · 떨군 것 "
				+ goingDrops + "개 · 최고 " + goingBest + "등급");

			Assert.Greater(stayingDrops, goingDrops, "머물러도 더 많이 안 떨군다 — 머물 이유가 없다");
			Assert.Greater(goingBest, stayingBest, "내려가도 더 좋은 게 안 나온다 — 내려갈 이유가 없다");
		}

		/// <summary>고른 것은 저장을 건넌다 — 껐다 켜니 도로 내려가면 고른 뜻이 없다.</summary>
		[Test]
		public void Choice_SurvivesSaveLoad()
		{
			IdleState state = new IdleState { HoldingStage = true };

			IdleState restored = new IdleState();
			restored.Load(state.Save());

			Assert.IsTrue(restored.HoldingStage, "껐다 켜니 머무르기가 풀렸다");
		}

		/// <summary>언제든 뒤집을 수 있다 — 되돌릴 수 없는 선택이면 아무도 안 누른다.</summary>
		[Test]
		public void Choice_IsReversible()
		{
			IdleTuning tuning = new IdleTuning();
			IdleSession session = new IdleSession(tuning);

			Assert.IsTrue(session.Send(new IdleHoldStageIntent(true)));
			Assert.IsTrue(session.Capture().HoldingStage);

			session.Advance(600d);
			Assert.AreEqual(1, session.State.Stage);

			Assert.IsTrue(session.Send(new IdleHoldStageIntent(false)));
			session.Advance(600d);

			Assert.Greater(session.State.Stage, 1, "머무르기를 풀었는데 안 내려간다");
		}

		/// <summary>옛 저장은 「안 머문다」로 들어온다 — 없던 값이 사람을 가둬 두면 안 된다.</summary>
		[Test]
		public void OldSaves_DoNotGetStuck()
		{
			IdleState fromOld = new IdleState();
			fromOld.Load(new IdleSaveData { Resource = 1d, Stage = 5 });

			Assert.IsFalse(fromOld.HoldingStage);
		}

		private static long Total(IdleState state)
		{
			long total = 0L;
			for (int tier = 0; tier < state.DroppedByTier.Length; tier++)
			{
				total += state.DroppedByTier[tier];
			}

			return total;
		}

		private static int BestTier(IdleState state)
		{
			for (int tier = state.DroppedByTier.Length; tier >= 1; tier--)
			{
				if (state.DroppedByTier[tier - 1] > 0L)
				{
					return tier;
				}
			}

			return 0;
		}
	}
}
