using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class ShurikenFlightTests
	{
		private const float OUTBOUND_SECONDS = 0.6f;
		private const float RETURN_TIMEOUT = 3f;

		private static ShurikenFlight Create(bool returns, int bounces) => new(OUTBOUND_SECONDS, returns, bounces, RETURN_TIMEOUT);

		[Test]
		public void PlainThrowEndsAfterOutbound()
		{
			ShurikenFlight flight = Create(false, 0);
			flight.Tick(0.59f);
			Assert.AreEqual(ShurikenFlightPhase.Outbound, flight.Phase);
			Assert.AreEqual(16f, flight.Speed(16f, 18f), 0.0001f, "안 돌아오는 수리검은 고른 속도");
			flight.Tick(0.02f);
			Assert.AreEqual(ShurikenFlightPhase.Done, flight.Phase);
			Assert.AreEqual(0f, flight.Speed(16f, 18f), 0.0001f);
		}

		[Test]
		public void ReturningThrowSlowsThenComesBackUntilCaught()
		{
			ShurikenFlight flight = Create(true, 0);
			Assert.AreEqual(16f, flight.Speed(16f, 18f), 0.0001f);
			flight.Tick(0.3f);
			Assert.AreEqual(8f, flight.Speed(16f, 18f), 0.001f);
			flight.Tick(0.3f);
			Assert.AreEqual(ShurikenFlightPhase.Returning, flight.Phase);
			Assert.AreEqual(0f, flight.Speed(16f, 18f), 0.001f);
			flight.Tick(0.6f);
			Assert.AreEqual(18f, flight.Speed(16f, 18f), 0.001f);
			flight.Catch();
			Assert.AreEqual(ShurikenFlightPhase.Done, flight.Phase);
		}

		[Test]
		public void CatchIsIgnoredOnTheWayOut()
		{
			ShurikenFlight flight = Create(true, 0);
			flight.Catch();
			Assert.AreEqual(ShurikenFlightPhase.Outbound, flight.Phase, "던진 자리에서 바로 잡히지 않음");
		}

		[Test]
		public void BounceNeedsTargetAndCharge()
		{
			ShurikenFlight flight = Create(false, 1);
			Assert.IsFalse(flight.TryBounce(false), "다음 적이 없으면 튕김을 쓰지 않음");
			Assert.AreEqual(1, flight.BouncesLeft);
			Assert.IsTrue(flight.TryBounce(true));
			Assert.IsFalse(flight.TryBounce(true));
		}

		[Test]
		public void BounceRestartsOutboundClock()
		{
			ShurikenFlight flight = Create(false, 1);
			flight.Tick(0.5f);
			Assert.IsTrue(flight.TryBounce(true));
			flight.Tick(0.5f);
			Assert.AreEqual(ShurikenFlightPhase.Outbound, flight.Phase);
		}

		[Test]
		public void BouncesComeBeforeReturnAndNeverOnTheWayBack()
		{
			ShurikenFlight flight = Create(true, 2);
			Assert.IsTrue(flight.TryBounce(true));
			flight.Tick(OUTBOUND_SECONDS);
			Assert.AreEqual(ShurikenFlightPhase.Returning, flight.Phase);
			Assert.IsFalse(flight.TryBounce(true), "돌아오는 길에는 남은 튕김을 쓰지 않음");
		}

		[Test]
		public void ReturnGivesUpAfterTimeout()
		{
			ShurikenFlight flight = Create(true, 0);
			flight.Tick(OUTBOUND_SECONDS);
			flight.Tick(RETURN_TIMEOUT);
			Assert.AreEqual(ShurikenFlightPhase.Done, flight.Phase, "주인이 계속 달아나도 남지 않음");
		}
	}
}
