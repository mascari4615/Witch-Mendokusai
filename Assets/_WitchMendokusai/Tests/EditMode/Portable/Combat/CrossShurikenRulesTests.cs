using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class CrossShurikenRulesTests
	{
		private const float BASE_SECONDS = 5f;
		private const float HEAVY_SECONDS = 6.5f;

		private static float Interval(bool heavy, int weaponStacks, int sharedStacks, int cooldownPercent)
			=> CrossShurikenRules.ThrowInterval(BASE_SECONDS, HEAVY_SECONDS, heavy, weaponStacks, sharedStacks, 0.15f, 0.25f, cooldownPercent);

		[Test]
		public void HeavyThrowIsSlowerAndCadenceApplies()
		{
			Assert.AreEqual(5f, Interval(false, 0, 0, 0), 0.0001f);
			Assert.AreEqual(6.5f, Interval(true, 0, 0, 0), 0.0001f);
			Assert.AreEqual(4f, Interval(false, 0, 0, 20), 0.0001f);
			Assert.AreEqual(3.5f, Interval(false, 1, 1, 0), 0.0001f, "무기 스택과 공용 스택을 더함");
			Assert.AreEqual(1.25f, Interval(false, 4, 5, 0), 0.0001f, "바닥 25%");
		}

		[Test]
		public void BlastPercentMultipliesHeavyAndRepeat()
		{
			Assert.AreEqual(100, CrossShurikenRules.BlastPercent(false, 160, 0));
			Assert.AreEqual(160, CrossShurikenRules.BlastPercent(true, 160, 0));
			Assert.AreEqual(208, CrossShurikenRules.BlastPercent(true, 160, 30));
			Assert.AreEqual(171, CrossShurikenRules.BlastPercent(true, 163, 5), "171.15");
			Assert.AreEqual(168, CrossShurikenRules.BlastPercent(true, 160, 5));
			Assert.AreEqual(158, CrossShurikenRules.BlastPercent(true, 150, 5), "157.5 는 올림");
			Assert.AreEqual(100, CrossShurikenRules.BlastPercent(false, 160, -10));
		}

		// 뇌격은 한 방을 키우되 초당 값은 조금만 오름 (수치안 x1.23)
		[Test]
		public void HeavyGainsPerSecondStaysSmall()
		{
			float light = CrossShurikenRules.BlastPercent(false, 160, 0) / Interval(false, 0, 0, 0);
			float heavy = CrossShurikenRules.BlastPercent(true, 160, 0) / Interval(true, 0, 0, 0);
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
			CrossBlast echo = CrossShurikenRules.Blast(1, 165, 0.35f, 50, 0.7f);
			Assert.AreEqual(0.35f, echo.DelaySeconds, 0.0001f);
			Assert.AreEqual(83, echo.DamagePercent, "82.5 는 올림");
			Assert.AreEqual(0.7f, echo.RadiusMultiplier, 0.0001f);
			Assert.AreEqual(0.7f, CrossShurikenRules.Blast(2, 160, 0.35f, 50, 0.7f).DelaySeconds, 0.0001f);
		}

		[Test]
		public void FieldTicksCountWholeIntervals()
		{
			Assert.AreEqual(4, CrossShurikenRules.FieldTicks(2f, 0.5f));
			Assert.AreEqual(3, CrossShurikenRules.FieldTicks(0.9f, 0.3f), "소수 오차로 한 번 빠지지 않음");
			Assert.AreEqual(3, CrossShurikenRules.FieldTicks(1.8f, 0.6f));
			Assert.AreEqual(3, CrossShurikenRules.FieldTicks(1.9f, 0.5f));
			Assert.AreEqual(0, CrossShurikenRules.FieldTicks(-1f, 0.5f));
			Assert.AreEqual(0, CrossShurikenRules.FieldTicks(2f, 0f));
		}
	}
}
