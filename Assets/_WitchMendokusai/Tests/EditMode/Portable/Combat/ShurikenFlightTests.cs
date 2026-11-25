using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class ShurikenFlightTests
	{
		private const float OUTBOUND_SECONDS = 0.6f;
		private const float RETURN_TIMEOUT = 3f;
		private const float OUT_SPEED = 16f;
		private const float RETURN_SPEED = 18f;

		private static ShurikenFlight Create(bool returns, int bounces)
		{
			ShurikenFlight flight = new();
			flight.Begin(OUTBOUND_SECONDS, returns, bounces, RETURN_TIMEOUT);
			return flight;
		}

		[Test]
		public void PlainThrowEndsAfterOutbound()
		{
			ShurikenFlight flight = Create(false, 0);
			flight.Tick(0.59f);
			Assert.AreEqual(ShurikenFlightPhase.Outbound, flight.Phase);
			Assert.AreEqual(OUT_SPEED, flight.Speed(OUT_SPEED, RETURN_SPEED), 0.0001f, "안 돌아오는 수리검은 고른 속도");
			flight.Tick(0.02f);
			Assert.AreEqual(ShurikenFlightPhase.Done, flight.Phase);
			Assert.AreEqual(0f, flight.Speed(OUT_SPEED, RETURN_SPEED), 0.0001f);
		}

		[Test]
		public void ReturningThrowSlowsThenComesBackUntilCaught()
		{
			ShurikenFlight flight = Create(true, 0);
			Assert.AreEqual(OUT_SPEED, flight.Speed(OUT_SPEED, RETURN_SPEED), 0.0001f);
			flight.Tick(0.3f);
			Assert.AreEqual(OUT_SPEED / 2f, flight.Speed(OUT_SPEED, RETURN_SPEED), 0.001f);
			flight.Tick(0.31f);
			Assert.AreEqual(ShurikenFlightPhase.Returning, flight.Phase);
			flight.Tick(2f);
			Assert.AreEqual(RETURN_SPEED, flight.Speed(OUT_SPEED, RETURN_SPEED), 0.001f, "돌아오는 속도는 더 커지지 않음");
			flight.Catch();
			Assert.AreEqual(ShurikenFlightPhase.Done, flight.Phase);
		}

		[Test]
		public void LongFrameCarriesOverAndNegativeTimeIsIgnored()
		{
			ShurikenFlight flight = Create(true, 0);
			flight.Tick(-5f);
			Assert.AreEqual(OUT_SPEED, flight.Speed(OUT_SPEED, RETURN_SPEED), 0.0001f);
			flight.Tick(OUTBOUND_SECONDS + 0.3f);
			Assert.AreEqual(RETURN_SPEED / 2f, flight.Speed(OUT_SPEED, RETURN_SPEED), 0.001f, "넘친 0.3초만큼 돌아오는 중");
			ShurikenFlight frozen = Create(true, 0);
			frozen.Tick(OUTBOUND_SECONDS + RETURN_TIMEOUT);
			Assert.AreEqual(ShurikenFlightPhase.Done, frozen.Phase);
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
			foreach (bool returns in new[] { false, true })
			{
				ShurikenFlight flight = Create(returns, 1);
				Assert.IsFalse(flight.TryBounce(false), "다음 적이 없으면 튕김을 쓰지 않음");
				Assert.AreEqual(1, flight.BouncesLeft);
				Assert.IsTrue(flight.TryBounce(true));
				Assert.AreEqual(0, flight.BouncesLeft);
				Assert.IsFalse(flight.TryBounce(true));
			}
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
		public void TimeEndsTheWayOutEvenWithBouncesLeft()
		{
			ShurikenFlight flight = Create(true, 2);
			flight.TryBounce(true);
			flight.Tick(0.3f);
			Assert.AreEqual(OUT_SPEED, flight.Speed(OUT_SPEED, RETURN_SPEED), 0.0001f, "튕김이 남은 동안은 느려지지 않음");
			flight.Tick(0.31f);
			Assert.AreEqual(ShurikenFlightPhase.Returning, flight.Phase);
			Assert.AreEqual(1, flight.BouncesLeft);
			Assert.IsFalse(flight.TryBounce(true), "돌아오는 길에는 남은 튕김을 쓰지 않음");
		}

		// 다음 적을 찾는 거리는 실제로 갈 수 있는 거리 안
		[Test]
		public void ReachMatchesTravelledDistance()
		{
			Assert.AreEqual(9.6f, Create(false, 1).ReachAfterBounce(OUT_SPEED), 0.001f);
			Assert.AreEqual(9.6f, Create(true, 2).ReachAfterBounce(OUT_SPEED), 0.001f);
			ShurikenFlight last = Create(true, 1);
			float reach = last.ReachAfterBounce(OUT_SPEED);
			Assert.IsTrue(last.TryBounce(true));
			float travelled = 0f;
			const float STEP = 0.001f;
			while (last.Phase == ShurikenFlightPhase.Outbound)
			{
				travelled += last.Speed(OUT_SPEED, RETURN_SPEED) * STEP;
				last.Tick(STEP);
			}
			Assert.AreEqual(reach, travelled, 0.02f);
		}

		[Test]
		public void SpentPierceTurnsBackOrEnds()
		{
			ShurikenFlight back = Create(true, 0);
			back.Expend();
			Assert.AreEqual(ShurikenFlightPhase.Returning, back.Phase);
			back.Expend();
			Assert.AreEqual(ShurikenFlightPhase.Returning, back.Phase, "돌아오는 길에는 관통을 세지 않음");
			ShurikenFlight plain = Create(false, 2);
			plain.Expend();
			Assert.AreEqual(ShurikenFlightPhase.Done, plain.Phase);
		}

		[Test]
		public void BeginResetsPooledFlight()
		{
			ShurikenFlight flight = Create(true, 1);
			flight.TryBounce(true);
			flight.Tick(OUTBOUND_SECONDS + RETURN_TIMEOUT);
			flight.Begin(OUTBOUND_SECONDS, false, 2, RETURN_TIMEOUT);
			Assert.AreEqual(ShurikenFlightPhase.Outbound, flight.Phase);
			Assert.AreEqual(2, flight.BouncesLeft);
			flight.Tick(0.5f);
			Assert.AreEqual(ShurikenFlightPhase.Outbound, flight.Phase);
		}
	}
}
