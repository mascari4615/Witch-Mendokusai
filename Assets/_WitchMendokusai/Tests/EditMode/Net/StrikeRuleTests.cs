using NUnit.Framework;
using WitchMendokusai.Net;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Tests.EditMode.Net
{
	/// <summary>
	/// 때리기는 <b>세계가</b> 심판한다 (TASK-WM-251).
	/// 창이 우길 수 있는 셋 — 얼마나 멀리서 · 얼마나 자주 · 누구를 — 을 여기서 다 본다.
	/// </summary>
	public class StrikeRuleTests
	{
		private static readonly Vector3 here = new Vector3(0f, 0f, 0f);

		[Test]
		public void 손이_닿으면_때린다()
		{
			Assert.AreEqual(StrikeRule.Denial.None,
				StrikeRule.CanStrike(1, 2, true, here, new Vector3(1f, 0f, 0f), 100, 0L, 10000L));
		}

		[Test]
		public void 멀면_손이_안_닿는다()
		{
			Assert.AreEqual(StrikeRule.Denial.TooFar,
				StrikeRule.CanStrike(1, 2, true, here, new Vector3(StrikeRule.REACH + 0.1f, 0f, 0f), 100, 0L, 10000L));
		}

		[Test]
		public void 너무_자주는_못_때린다()
		{
			// 창을 고쳐 초당 100번 보내도 팔은 그만큼 안 돌아온다.
			Assert.AreEqual(StrikeRule.Denial.TooSoon,
				StrikeRule.CanStrike(1, 2, true, here, new Vector3(1f, 0f, 0f), 100, 10000L, 10000L + StrikeRule.COOLDOWN_MS - 1));

			Assert.AreEqual(StrikeRule.Denial.None,
				StrikeRule.CanStrike(1, 2, true, here, new Vector3(1f, 0f, 0f), 100, 10000L, 10000L + StrikeRule.COOLDOWN_MS));
		}

		[Test]
		public void 자기_자신은_못_때린다()
		{
			Assert.AreEqual(StrikeRule.Denial.Myself,
				StrikeRule.CanStrike(1, 1, true, here, here, 100, 0L, 10000L));
		}

		[Test]
		public void 없는_사람은_못_때린다()
		{
			Assert.AreEqual(StrikeRule.Denial.NoSuchOne,
				StrikeRule.CanStrike(1, 99, false, here, here, 100, 0L, 10000L));
		}

		[Test]
		public void 이미_쓰러진_사람은_안_때린다()
		{
			Assert.AreEqual(StrikeRule.Denial.AlreadyDown,
				StrikeRule.CanStrike(1, 2, true, here, new Vector3(1f, 0f, 0f), 0, 0L, 10000L));
		}

		[Test]
		public void 맞으면_그만큼_줄고_0_아래로는_안_간다()
		{
			Assert.AreEqual(StrikeRule.FULL_HEALTH - StrikeRule.DAMAGE, StrikeRule.HealthAfterHit(StrikeRule.FULL_HEALTH));
			Assert.AreEqual(0, StrikeRule.HealthAfterHit(1), "몸이 마이너스가 되면 되살리기 셈이 다 어긋난다");
			Assert.AreEqual(0, StrikeRule.HealthAfterHit(0));
		}
	}
}
