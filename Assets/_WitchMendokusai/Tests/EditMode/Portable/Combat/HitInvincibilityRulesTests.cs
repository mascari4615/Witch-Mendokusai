using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class HitInvincibilityRulesTests
	{
		[Test]
		public void UnitValueWinsOverTypeDefault()
		{
			Assert.AreEqual(0.2f, HitInvincibilityRules.Seconds(0.2f, true, 0.1f, 0f), 0.0001f);
			Assert.AreEqual(0f, HitInvincibilityRules.Seconds(0f, true, 0.1f, 0f), 0.0001f);
		}

		[Test]
		public void NotSetUsesTypeDefault()
		{
			Assert.AreEqual(0.1f, HitInvincibilityRules.Seconds(HitInvincibilityRules.NOT_SET, true, 0.1f, 0.02f), 0.0001f);
			Assert.AreEqual(0.02f, HitInvincibilityRules.Seconds(HitInvincibilityRules.NOT_SET, false, 0.1f, 0.02f), 0.0001f);
		}

		[Test]
		public void ValueIsClamped()
		{
			Assert.AreEqual(HitInvincibilityRules.MAX_SECONDS, HitInvincibilityRules.Seconds(99f, true, 0.1f, 0f), 0.0001f);
		}

		[Test]
		public void ActiveOnlyInsideWindow()
		{
			Assert.IsTrue(HitInvincibilityRules.IsActive(10f, 10.05f, 0.1f));
			Assert.IsFalse(HitInvincibilityRules.IsActive(10f, 10.1f, 0.1f));
			Assert.IsFalse(HitInvincibilityRules.IsActive(10f, 10.01f, 0f));
			Assert.IsFalse(HitInvincibilityRules.IsActive(float.NegativeInfinity, 0f, 0.1f));
		}

		[Test]
		public void MultiShotWeaponHitsBossOncePerWindow()
		{
			// 60fps 로 1초 동안 매 프레임 맞히려는 무기. 무적 0.1초면 약 10번만 들어감
			float last = float.NegativeInfinity;
			int hits = 0;
			for (int frame = 0; frame < 60; frame++)
			{
				float now = frame / 60f;
				if (HitInvincibilityRules.IsActive(last, now, 0.1f))
					continue;
				last = now;
				hits++;
			}
			Assert.AreEqual(10, hits);
		}
	}
}
