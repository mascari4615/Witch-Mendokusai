using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class WaveDensityRulesTests
	{
		private const int SOFT = 300;
		private const int HARD = 500;

		[Test]
		public void DeficitIsGapBelowMinimum()
		{
			Assert.AreEqual(15, WaveDensityRules.Deficit(0, 15));
			Assert.AreEqual(5, WaveDensityRules.Deficit(10, 15));
			Assert.AreEqual(0, WaveDensityRules.Deficit(15, 15));
			Assert.AreEqual(0, WaveDensityRules.Deficit(40, 15));
		}

		[Test]
		public void DeficitIsZeroWhenMinimumDisabled()
		{
			Assert.AreEqual(0, WaveDensityRules.Deficit(0, 0));
			Assert.AreEqual(0, WaveDensityRules.Deficit(0, -5));
		}

		[Test]
		public void PeriodicStopsAtSoftCap()
		{
			Assert.AreEqual(1, WaveDensityRules.AllowedCount(SpawnClass.Periodic, 299, 1, SOFT, HARD));
			Assert.AreEqual(0, WaveDensityRules.AllowedCount(SpawnClass.Periodic, 300, 1, SOFT, HARD));
			Assert.AreEqual(0, WaveDensityRules.AllowedCount(SpawnClass.Periodic, 450, 1, SOFT, HARD));
		}

		[Test]
		public void PeriodicBatchIsClippedToRoomUnderSoftCap()
		{
			Assert.AreEqual(10, WaveDensityRules.AllowedCount(SpawnClass.Periodic, 290, 30, SOFT, HARD));
		}

		[Test]
		public void EventUsesHardCapInsteadOfSoftCap()
		{
			Assert.AreEqual(24, WaveDensityRules.AllowedCount(SpawnClass.Event, 300, 24, SOFT, HARD));
			Assert.AreEqual(10, WaveDensityRules.AllowedCount(SpawnClass.Event, 490, 24, SOFT, HARD));
			Assert.AreEqual(0, WaveDensityRules.AllowedCount(SpawnClass.Event, 500, 24, SOFT, HARD));
		}

		[Test]
		public void PriorityIgnoresBothCaps()
		{
			Assert.AreEqual(1, WaveDensityRules.AllowedCount(SpawnClass.Priority, 500, 1, SOFT, HARD));
			Assert.AreEqual(2, WaveDensityRules.AllowedCount(SpawnClass.Priority, 9999, 2, SOFT, HARD));
		}

		[Test]
		public void ZeroCapMeansNoLimit()
		{
			Assert.AreEqual(5, WaveDensityRules.AllowedCount(SpawnClass.Periodic, 9999, 5, 0, 0));
			Assert.AreEqual(5, WaveDensityRules.AllowedCount(SpawnClass.Event, 9999, 5, 0, 0));
		}

		[Test]
		public void PeriodicFallsBackToHardCapWhenSoftCapDisabled()
		{
			Assert.AreEqual(0, WaveDensityRules.AllowedCount(SpawnClass.Periodic, 500, 1, 0, HARD));
			Assert.AreEqual(1, WaveDensityRules.AllowedCount(SpawnClass.Periodic, 499, 1, 0, HARD));
		}

		[Test]
		public void PeriodicLimitNeverExceedsHardCapWhenSoftIsLarger()
		{
			Assert.AreEqual(0, WaveDensityRules.AllowedCount(SpawnClass.Periodic, 100, 1, 600, 100));
		}

		[Test]
		public void NonPositiveRequestAllowsNothing()
		{
			Assert.AreEqual(0, WaveDensityRules.AllowedCount(SpawnClass.Priority, 0, 0, SOFT, HARD));
			Assert.AreEqual(0, WaveDensityRules.AllowedCount(SpawnClass.Periodic, 0, -3, SOFT, HARD));
		}

		[Test]
		public void PeriodicCountIsOneWhenAboveMinimum()
		{
			Assert.AreEqual(1, WaveDensityRules.PeriodicCount(50, 15, 30, SOFT, HARD));
			Assert.AreEqual(1, WaveDensityRules.PeriodicCount(0, 0, 30, SOFT, HARD));
		}

		[Test]
		public void PeriodicCountRefillsDeficitUpToBurstMax()
		{
			Assert.AreEqual(12, WaveDensityRules.PeriodicCount(3, 15, 30, SOFT, HARD));
			Assert.AreEqual(30, WaveDensityRules.PeriodicCount(0, 130, 30, SOFT, HARD));
			Assert.AreEqual(130, WaveDensityRules.PeriodicCount(0, 130, 0, SOFT, HARD));
		}

		[Test]
		public void PeriodicCountRefillIsStillCapped()
		{
			Assert.AreEqual(0, WaveDensityRules.PeriodicCount(300, 400, 30, SOFT, HARD));
			Assert.AreEqual(5, WaveDensityRules.PeriodicCount(295, 400, 30, SOFT, HARD));
		}

		[Test]
		public void PeriodicCountIsClippedWhenMinimumExceedsCap()
		{
			Assert.AreEqual(20, WaveDensityRules.PeriodicCount(280, 500, 0, SOFT, HARD));
		}
	}
}
