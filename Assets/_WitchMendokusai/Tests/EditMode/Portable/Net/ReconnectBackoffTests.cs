using NUnit.Framework;
using WitchMendokusai.Net;

namespace WitchMendokusai.Tests.EditMode.Net
{
	/// <summary>다시 붙는 간격 (TASK-WM-217) — 서버를 두들겨 패지도, 사람을 오래 세워 두지도 않게.</summary>
	public sealed class ReconnectBackoffTests
	{
		[Test]
		public void 처음엔_금방_다시_붙어_본다()
		{
			ReconnectBackoff backoff = new ReconnectBackoff();

			Assert.That(backoff.NextDelay(), Is.EqualTo(ReconnectBackoff.FIRST_DELAY_SECONDS));
		}

		[Test]
		public void 실패할수록_뜸해진다()
		{
			ReconnectBackoff backoff = new ReconnectBackoff();

			float first = backoff.NextDelay();
			float second = backoff.NextDelay();
			float third = backoff.NextDelay();

			Assert.That(second, Is.EqualTo(first * 2f));
			Assert.That(third, Is.EqualTo(second * 2f));
		}

		[Test]
		public void 아무리_뜸해도_상한이_있다()
		{
			ReconnectBackoff backoff = new ReconnectBackoff();

			float delay = 0f;
			for (int i = 0; i < 20; i++)
				delay = backoff.NextDelay();

			Assert.That(delay, Is.EqualTo(ReconnectBackoff.MAX_DELAY_SECONDS));
			Assert.That(backoff.Attempts, Is.EqualTo(20));
		}

		[Test]
		public void 붙으면_처음으로_돌아간다()
		{
			ReconnectBackoff backoff = new ReconnectBackoff();
			backoff.NextDelay();
			backoff.NextDelay();

			backoff.Reset();

			Assert.That(backoff.Attempts, Is.EqualTo(0));
			Assert.That(backoff.NextDelay(), Is.EqualTo(ReconnectBackoff.FIRST_DELAY_SECONDS));
		}
	}
}
