using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class VolleyGaugeRulesTests
	{
		[Test]
		public void 정예와_보스는_더_채우고_충전_카드가_곱해진다()
		{
			Assert.AreEqual(1f, VolleyGaugeRules.Charge(false, 1f, 3f, 0), 0.0001f);
			Assert.AreEqual(3f, VolleyGaugeRules.Charge(true, 1f, 3f, 0), 0.0001f);
			Assert.AreEqual(1.25f, VolleyGaugeRules.Charge(false, 1f, 3f, 25), 0.0001f);
			Assert.AreEqual(1f, VolleyGaugeRules.Charge(false, 1f, 3f, -50), 0.0001f, "음수 충전 카드는 무시");
		}

		[Test]
		public void 가득_차면_더_쌓지_않는다()
		{
			Assert.AreEqual(119f, VolleyGaugeRules.Add(118f, 1f, 120f), 0.0001f);
			Assert.AreEqual(120f, VolleyGaugeRules.Add(119.5f, 3f, 120f), 0.0001f);
		}

		[Test]
		public void 가득_차고_최소_간격이_지나야_쏜다()
		{
			Assert.IsFalse(VolleyGaugeRules.CanFire(119f, 120f, 10f, 6f));
			Assert.IsFalse(VolleyGaugeRules.CanFire(120f, 120f, 5.9f, 6f));
			Assert.IsTrue(VolleyGaugeRules.CanFire(120f, 120f, 6f, 6f));
		}

		[Test]
		public void 무기_수와_무기당_발로_탄_수를_낸다()
		{
			Assert.AreEqual(16, VolleyGaugeRules.ShotCount(8, 0, 2));
			Assert.AreEqual(24, VolleyGaugeRules.ShotCount(8, 4, 2));
		}

		[Test]
		public void 피해_배율은_누적과_나눠_쏘기와_집중을_곱한다()
		{
			Assert.AreEqual(100, VolleyGaugeRules.DamagePercent(0, false, 60, false, 140));
			Assert.AreEqual(130, VolleyGaugeRules.DamagePercent(30, false, 60, false, 140));
			Assert.AreEqual(60, VolleyGaugeRules.DamagePercent(0, true, 60, false, 140));
			Assert.AreEqual(140, VolleyGaugeRules.DamagePercent(0, false, 60, true, 140));
		}

		[Test]
		public void 부채꼴은_고르게_나누고_가운데가_0()
		{
			Assert.AreEqual(0f, VolleyGaugeRules.FanAngle(0, 1, 50f), 0.0001f);
			Assert.AreEqual(-25f, VolleyGaugeRules.FanAngle(0, 3, 50f), 0.0001f);
			Assert.AreEqual(0f, VolleyGaugeRules.FanAngle(1, 3, 50f), 0.0001f);
			Assert.AreEqual(25f, VolleyGaugeRules.FanAngle(2, 3, 50f), 0.0001f);
		}
	}
}
