using NUnit.Framework;
using WitchMendokusai.Server;

namespace WitchMendokusai.ServerTests
{
	/// <summary>세계가 「초당 20번」을 지키는 셈 (TASK-WM-220).</summary>
	public class TickScheduleTests
	{
		private const double PERIOD = 50.0;

		[Test]
		public void 제때면_남은_만큼만_잔다()
		{
			(double wait, double next) = TickSchedule.Next(1000.0, 1030.0, PERIOD);

			Assert.That(wait, Is.EqualTo(30.0));
			Assert.That(next, Is.EqualTo(1080.0));
		}

		[Test]
		public void 늦게_깼으면_그만큼_덜_잔다()
		{
			// 50ms 를 재웠는데 62ms 만에 깼다 — 다음 차례까지 38ms 밖에 안 남았다.
			(double wait, double next) = TickSchedule.Next(1062.0, 1100.0, PERIOD);

			Assert.That(wait, Is.EqualTo(38.0));
			Assert.That(next, Is.EqualTo(1150.0));
		}

		[Test]
		public void 이미_지났으면_안_잔다()
		{
			(double wait, _) = TickSchedule.Next(1010.0, 1000.0, PERIOD);

			Assert.That(wait, Is.EqualTo(0.0));
		}

		[Test]
		public void 한_차례_넘게_밀리면_차례를_지금_기준으로_다시_잡는다()
		{
			// 안 그러면 밀린 만큼 쉼 없이 돌며 따라잡느라 세계가 헐떡인다.
			(double wait, double next) = TickSchedule.Next(2000.0, 1000.0, PERIOD);

			Assert.That(wait, Is.EqualTo(0.0));
			Assert.That(next, Is.EqualTo(2050.0));
		}

		[Test]
		public void 늦음이_쌓이지_않는다()
		{
			// 매번 12ms 씩 늦게 깨도, 100 바퀴 뒤 차례는 여전히 「처음 + 100×50」이다.
			double due = 0.0;
			double now = 0.0;

			for (int i = 0; i < 100; i++)
			{
				(double wait, double next) = TickSchedule.Next(now, due, PERIOD);
				now += wait + 12.0;
				due = next;
			}

			Assert.That(due, Is.EqualTo(5000.0).Within(0.001));
		}
	}
}
