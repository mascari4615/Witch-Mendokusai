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
			SendPlan.Choice plan = SendPlan.For(120, 100, 0, 7);

			Assert.That(plan.Send, Is.True);
			Assert.That(plan.BehindSteps, Is.EqualTo(0), "안 밀리는 창을 좁힐 이유가 없다");
		}

		[Test]
		public void 밀리면_좁힌다()
		{
			SendPlan.Choice plan = SendPlan.For(900, 200, 0, 8);

			Assert.That(plan.BehindSteps, Is.EqualTo(SendPlan.BEHIND_STEPS_WHEN_LAGGING));
		}

		/// <summary>
		/// ★ 좁히기<b>만</b> 하면 오히려 더 나른다 (2026-08-14 CI: 나쁜 16.0KB/s vs 곧은 8.4KB/s) —
		/// 좁힌 판은 「작은 <b>전체</b> 그림」이라 델타보다 클 수 있다. 그래서 박자도 절반이다.
		/// </summary>
		[Test]
		public void 밀리면_박자도_절반이다()
		{
			Assert.That(SendPlan.For(900, 200, 3, 8).Send, Is.True, "짝수 판은 보낸다");
			Assert.That(SendPlan.For(900, 200, 3, 9).Send, Is.False, "홀수 판은 건너뛴다");
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
				if (SendPlan.For(900, 200, 3, sequence).Send)
					sent++;
			}

			Assert.That(sent, Is.GreaterThanOrEqualTo(8), $"초당 {sent}판 — 여덟 판 아래면 사람은 「끊긴다」로 읽는다");
		}

		/// <summary>
		/// ★ 회선이 <b>원래 먼</b> 것과 <b>밀리는</b> 것은 다르다 (TASK-WM-341).
		/// 절대 400ms 로 자르던 시절, 왕복 400ms 회선에서는 모두가 밀린 것으로 잡혀
		/// 좁혀졌고 보던 사람이 판에서 빠져 화면이 100% 멎었다(CI 실측).
		/// </summary>
		[Test]
		public void 원래_먼_회선은_밀리는_것이_아니다()
		{
			// 바닥도 400ms · 지금도 420ms — 그냥 먼 회선이다.
			SendPlan.Choice plan = SendPlan.For(420, 400, 0, 9);

			Assert.That(plan.Send, Is.True, "먼 회선이라고 건너뛰면 그 사람 화면이 멎는다");
			Assert.That(plan.BehindSteps, Is.EqualTo(0));
		}

		[Test]
		public void 바닥보다_많이_늦어지면_그때_밀린_것이다()
		{
			SendPlan.Choice plan = SendPlan.For(700, 400, 0, 9);

			Assert.That(plan.Send, Is.False, "홀수 판이라 건너뛴다");
			Assert.That(plan.BehindSteps, Is.EqualTo(SendPlan.BEHIND_STEPS_WHEN_LAGGING));
		}

		[Test]
		public void 아직_왕복을_모르면_안_건드린다()
		{
			SendPlan.Choice plan = SendPlan.For(0, 0, 0, 9);

			Assert.That(plan.Send, Is.True);
		}

		[Test]
		public void 이미_더_뒤처진_창은_덜_좁히지_않는다()
		{
			SendPlan.Choice plan = SendPlan.For(900, 200, 6, 8);

			Assert.That(plan.BehindSteps, Is.EqualTo(6), "이미 6걸음 뒤처졌으면 3으로 되돌리면 안 된다");
		}
	}
}
