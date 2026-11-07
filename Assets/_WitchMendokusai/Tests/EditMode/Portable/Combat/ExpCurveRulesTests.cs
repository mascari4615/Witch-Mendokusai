using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class ExpCurveRulesTests
	{
		private const int BASE = 30;
		private static readonly float BARRIER_MULTIPLIER = 2f;

		private static readonly List<ExpCurveBand> LINEAR = new List<ExpCurveBand>
		{
			new ExpCurveBand { FromLevel = 1, Increment = 30 },
		};

		private static readonly List<ExpCurveBand> BANDS = new List<ExpCurveBand>
		{
			new ExpCurveBand { FromLevel = 1, Increment = 30 },
			new ExpCurveBand { FromLevel = 15, Increment = 60, BarrierExtra = 1500 },
			new ExpCurveBand { FromLevel = 30, Increment = 90, BarrierExtra = 4300 },
		};

		[Test]
		public void LinearBandMatchesOldCurve()
		{
			for (int level = 0; level < 50; level++)
				Assert.AreEqual(30 * (level + 1), ExpCurveRules.Required(LINEAR, BASE, level));
		}

		[Test]
		public void EmptyTableKeepsBaseRequirement()
		{
			Assert.AreEqual(BASE, ExpCurveRules.Required(new List<ExpCurveBand>(), BASE, 10));
			Assert.AreEqual(BASE, ExpCurveRules.Required(null, BASE, 10));
		}

		[Test]
		public void EarlyLevelsMatchLinearCurve()
		{
			for (int level = 0; level < 15; level++)
				Assert.AreEqual(ExpCurveRules.Required(LINEAR, BASE, level), ExpCurveRules.Required(BANDS, BASE, level));
		}

		[Test]
		public void BarrierAddsOnlyAtBandStart()
		{
			Assert.AreEqual(450, ExpCurveRules.Required(BANDS, BASE, 14));
			Assert.AreEqual(510 + 1500, ExpCurveRules.Required(BANDS, BASE, 15));
			Assert.AreEqual(570, ExpCurveRules.Required(BANDS, BASE, 16));
			Assert.AreEqual(1350, ExpCurveRules.Required(BANDS, BASE, 29));
			Assert.AreEqual(1440 + 4300, ExpCurveRules.Required(BANDS, BASE, 30));
			Assert.AreEqual(1530, ExpCurveRules.Required(BANDS, BASE, 31));
		}

		[Test]
		public void GainMultiplierOnlyOnBarrierLevels()
		{
			Assert.AreEqual(1f, ExpCurveRules.GainMultiplier(BANDS, 14, BARRIER_MULTIPLIER));
			Assert.AreEqual(2f, ExpCurveRules.GainMultiplier(BANDS, 15, BARRIER_MULTIPLIER));
			Assert.AreEqual(1f, ExpCurveRules.GainMultiplier(BANDS, 16, BARRIER_MULTIPLIER));
			Assert.AreEqual(2f, ExpCurveRules.GainMultiplier(BANDS, 30, BARRIER_MULTIPLIER));
			Assert.AreEqual(1f, ExpCurveRules.GainMultiplier(BANDS, 1, BARRIER_MULTIPLIER));
		}

		[Test]
		public void BandWithoutBarrierIsNotBarrierLevel()
		{
			Assert.IsFalse(ExpCurveRules.IsBarrierLevel(LINEAR, 1));
			Assert.AreEqual(1f, ExpCurveRules.GainMultiplier(LINEAR, 1, BARRIER_MULTIPLIER));
		}

		// 봇 완주 레벨 42~43의 수집 경험치 구간: 15 x L x (L + 1), 레벨 42 시작 27090, 레벨 44 직전 29699
		[TestCase(27090, 42, 34)]
		[TestCase(28380, 43, 35)]
		[TestCase(29699, 43, 36)]
		public void BotRunExpectedLevelDrop(int collectedExp, int oldLevel, int newLevel)
		{
			Assert.AreEqual(oldLevel, ExpCurveRules.LevelAfter(LINEAR, BASE, BARRIER_MULTIPLIER, collectedExp));
			Assert.AreEqual(newLevel, ExpCurveRules.LevelAfter(BANDS, BASE, BARRIER_MULTIPLIER, collectedExp));
		}
	}
}
