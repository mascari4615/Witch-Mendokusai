using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class CrossShurikenRulesTests
	{
		[Test]
		public void HeavyThrowIsSlowerAndCooldownApplies()
		{
			Assert.AreEqual(5f, CrossShurikenRules.ThrowInterval(5f, 6.5f, false, 0, 0.15f, 0.25f, 0), 0.0001f);
			Assert.AreEqual(6.5f, CrossShurikenRules.ThrowInterval(5f, 6.5f, true, 0, 0.15f, 0.25f, 0), 0.0001f);
			Assert.AreEqual(4f, CrossShurikenRules.ThrowInterval(5f, 6.5f, false, 0, 0.15f, 0.25f, 20), 0.0001f);
			Assert.AreEqual(1.25f, CrossShurikenRules.ThrowInterval(5f, 6.5f, false, 9, 0.15f, 0.25f, 0), 0.0001f, "바닥 25%");
		}

		[Test]
		public void BlastPercentMultipliesHeavyAndRepeat()
		{
			Assert.AreEqual(100, CrossShurikenRules.BlastPercent(false, 160, 0));
			Assert.AreEqual(160, CrossShurikenRules.BlastPercent(true, 160, 0));
			Assert.AreEqual(208, CrossShurikenRules.BlastPercent(true, 160, 30));
			Assert.AreEqual(100, CrossShurikenRules.BlastPercent(false, 160, -10));
		}

		// 뇌격은 한 방을 키우되 초당 값은 조금만 오름 (수치안 x1.23)
		[Test]
		public void HeavyGainsPerSecondStaysSmall()
		{
			float light = 100f / CrossShurikenRules.ThrowInterval(5f, 6.5f, false, 0, 0.15f, 0.25f, 0);
			float heavy = CrossShurikenRules.BlastPercent(true, 160, 0) / CrossShurikenRules.ThrowInterval(5f, 6.5f, true, 0, 0.15f, 0.25f, 0);
			Assert.AreEqual(1.23f, heavy / light, 0.01f);
		}

		[Test]
		public void EchoBlastIsLaterAndSmaller()
		{
			Assert.AreEqual(1, CrossShurikenRules.BlastCount(false));
			Assert.AreEqual(2, CrossShurikenRules.BlastCount(true));
			CrossBlast first = CrossShurikenRules.Blast(0, 160, 0.35f, 50, 0.7f);
			Assert.AreEqual(0f, first.DelaySeconds, 0.0001f);
			Assert.AreEqual(160, first.DamagePercent);
			Assert.AreEqual(1f, first.RadiusMultiplier, 0.0001f);
			CrossBlast echo = CrossShurikenRules.Blast(1, 160, 0.35f, 50, 0.7f);
			Assert.AreEqual(0.35f, echo.DelaySeconds, 0.0001f);
			Assert.AreEqual(80, echo.DamagePercent);
			Assert.AreEqual(0.7f, echo.RadiusMultiplier, 0.0001f);
		}

		[Test]
		public void FieldTicksCountWholeIntervals()
		{
			Assert.AreEqual(4, CrossShurikenRules.FieldTicks(2f, 0.5f));
			Assert.AreEqual(6, CrossShurikenRules.FieldTicks(0.6f, 0.1f), "소수 오차로 한 번 빠지지 않음");
			Assert.AreEqual(0, CrossShurikenRules.FieldTicks(2f, 0f));
		}

		[Test]
		public void NthCountsFromOne()
		{
			Assert.IsFalse(CrossShurikenRules.IsNth(0, 4));
			Assert.IsFalse(CrossShurikenRules.IsNth(3, 4));
			Assert.IsTrue(CrossShurikenRules.IsNth(4, 4));
			Assert.IsTrue(CrossShurikenRules.IsNth(8, 4));
			Assert.IsFalse(CrossShurikenRules.IsNth(4, 0));
		}
	}
}
