using NUnit.Framework;
using WitchMendokusai.Server;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// 밀리는 창에게 어떻게 보낼까 — 이틀 동안 두 번 반응적으로 고친 정책을 <b>밀리초에</b> 가른다 (TASK-WM-340).
	/// </summary>
	public sealed class SendPlanTests
	{
		[Test]
		public void 안_밀리면_그대로_보낸다()
		{
			SendPlan.Choice plan = SendPlan.For(120, 0, 7);

			Assert.That(plan.Send, Is.True);
			Assert.That(plan.BehindSteps, Is.EqualTo(0), "안 밀리는 창을 좁힐 이유가 없다");
		}

		[Test]
		public void 밀리면_좁힌다()
		{
			SendPlan.Choice plan = SendPlan.For(900, 0, 8);

			Assert.That(plan.BehindSteps, Is.EqualTo(SendPlan.BEHIND_STEPS_WHEN_LAGGING));
		}

		/// <summary>
		/// ★ 좁히기<b>만</b> 하면 오히려 더 나른다 (2026-08-14 CI: 나쁜 16.0KB/s vs 곧은 8.4KB/s) —
		/// 좁힌 판은 「작은 <b>전체</b> 그림」이라 델타보다 클 수 있다. 그래서 박자도 절반이다.
		/// </summary>
		[Test]
		public void 밀리면_박자도_절반이다()
		{
			Assert.That(SendPlan.For(900, 3, 8).Send, Is.True, "짝수 판은 보낸다");
			Assert.That(SendPlan.For(900, 3, 9).Send, Is.False, "홀수 판은 건너뛴다");
		}

		/// <summary>
		/// ★ 그래도 끊기면 안 된다: 5판마다 하나(4Hz)로 줄였더니 초당 7.3장까지 떨어져
		/// 「초당 여덟 판」 바닥을 깼다. 절반(10Hz)이 그 바닥 위에 있는지 여기서 지킨다.
		/// </summary>
		[Test]
		public void 절반_박자여도_초당_여덟_판_위다()
		{
			const int worldHz = 20;
			int sent = 0;
			for (long sequence = 0; sequence < worldHz; sequence++)
			{
				if (SendPlan.For(900, 3, sequence).Send)
					sent++;
			}

			Assert.That(sent, Is.GreaterThanOrEqualTo(8), $"초당 {sent}판 — 여덟 판 아래면 사람은 「끊긴다」로 읽는다");
		}

		[Test]
		public void 이미_더_뒤처진_창은_덜_좁히지_않는다()
		{
			SendPlan.Choice plan = SendPlan.For(900, 6, 8);

			Assert.That(plan.BehindSteps, Is.EqualTo(6), "이미 6걸음 뒤처졌으면 3으로 되돌리면 안 된다");
		}
	}
}
