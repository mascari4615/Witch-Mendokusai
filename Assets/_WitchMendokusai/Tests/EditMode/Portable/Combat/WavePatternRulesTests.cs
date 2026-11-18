using System;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class WavePatternRulesTests
	{
		[Test]
		public void TakeSpawnsCarriesRemainder()
		{
			float debt = 0f;
			Assert.AreEqual(0, WavePatternRules.TakeSpawns(ref debt, 3f, 0.1f));
			Assert.AreEqual(0, WavePatternRules.TakeSpawns(ref debt, 3f, 0.1f));
			Assert.AreEqual(0, WavePatternRules.TakeSpawns(ref debt, 3f, 0.1f));
			Assert.AreEqual(1, WavePatternRules.TakeSpawns(ref debt, 3f, 0.1f));
			Assert.AreEqual(25, WavePatternRules.TakeSpawns(ref debt, 250f, 0.1f));
		}

		[Test]
		public void TakeSpawnsIgnoresZeroRate()
		{
			float debt = 0.9f;
			Assert.AreEqual(0, WavePatternRules.TakeSpawns(ref debt, 0f, 1f));
			Assert.AreEqual(0.9f, debt, 0.0001f);
		}

		[Test]
		public void EllipsePointsLieOnEllipse()
		{
			for (int i = 0; i < 30; i++)
			{
				WavePatternRules.EllipsePoint(i, 30, 7.5f, 5f, 0f, out float x, out float z);
				float normalized = x * x / (7.5f * 7.5f) + z * z / (5f * 5f);
				Assert.AreEqual(1f, normalized, 0.0001f);
			}
			WavePatternRules.EllipsePoint(0, 4, 2f, 1f, 0f, out float firstX, out float firstZ);
			Assert.AreEqual(2f, firstX, 0.0001f);
			Assert.AreEqual(0f, firstZ, 0.0001f);
		}

		[Test]
		public void ContractedScaleStopsAtMinimum()
		{
			Assert.AreEqual(1f, WavePatternRules.ContractedScale(0f, 0.1f, 0.3f), 0.0001f);
			Assert.AreEqual(0.5f, WavePatternRules.ContractedScale(5f, 0.1f, 0.3f), 0.0001f);
			Assert.AreEqual(0.3f, WavePatternRules.ContractedScale(100f, 0.1f, 0.3f), 0.0001f);
		}

		[Test]
		public void GapStaysInsideLanes()
		{
			int laneCount = 39;
			int gapCount = 2;
			int gapStart = WavePatternRules.InitialGapStart(laneCount, gapCount);
			Assert.AreEqual(18, gapStart);
			Random random = new(4615);
			for (int i = 0; i < 500; i++)
			{
				gapStart = WavePatternRules.StepGapStart(gapStart, laneCount, gapCount, 3, (min, max) => random.Next(min, max + 1));
				Assert.GreaterOrEqual(gapStart, 0);
				Assert.LessOrEqual(gapStart + gapCount, laneCount);
			}
		}

		[Test]
		public void GapStepClampsAtEdge()
		{
			Assert.AreEqual(0, WavePatternRules.StepGapStart(1, 10, 2, 5, (min, max) => min));
			Assert.AreEqual(8, WavePatternRules.StepGapStart(7, 10, 2, 5, (min, max) => max));
			Assert.AreEqual(4, WavePatternRules.StepGapStart(4, 10, 2, 0, (min, max) => throw new InvalidOperationException()));
		}

		[Test]
		public void LaneOffsetIsCentered()
		{
			Assert.AreEqual(-1f, WavePatternRules.LaneOffset(0, 3, 1f), 0.0001f);
			Assert.AreEqual(0f, WavePatternRules.LaneOffset(1, 3, 1f), 0.0001f);
			Assert.AreEqual(-0.5f, WavePatternRules.LaneOffset(0, 2, 1f), 0.0001f);
		}

		[Test]
		public void ExpRequirementFollowsSpawnRate()
		{
			Assert.AreEqual(1f, WavePatternRules.ExpRequirementMultiplier(1f, 1f), 0.0001f);
			Assert.AreEqual(2.8f, WavePatternRules.ExpRequirementMultiplier(2.8f, 1f), 0.0001f);
			Assert.AreEqual(1f, WavePatternRules.ExpRequirementMultiplier(2.8f, 0f), 0.0001f);
			Assert.AreEqual(1.4f, WavePatternRules.ExpRequirementMultiplier(1.8f, 0.5f), 0.0001f);
			Assert.Throws<ArgumentOutOfRangeException>(() => WavePatternRules.ExpRequirementMultiplier(0f, 1f));
		}

		[Test]
		public void CombineRatesMultiplies()
		{
			Assert.AreEqual(1f, WavePatternRules.CombineRates(new float[0]), 0.0001f);
			Assert.AreEqual(0.05f, WavePatternRules.CombineRates(new[] { 0.5f, 0.1f }), 0.0001f);
			Assert.AreEqual(0f, WavePatternRules.CombineRates(new[] { 2f, -1f }), 0.0001f);
		}
	}
}
