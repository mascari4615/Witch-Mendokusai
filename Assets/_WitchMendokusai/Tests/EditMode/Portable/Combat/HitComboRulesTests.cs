using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class HitComboRulesTests
	{
		[Test]
		public void HitsAccumulateUpToCap()
		{
			int count = 0;
			for (int i = 0; i < 20; i++)
				count = HitComboRules.AfterHit(count, 10);
			Assert.AreEqual(10, count);
		}

		[Test]
		public void MissResetsToZero() => Assert.AreEqual(0, HitComboRules.AfterMiss());

		[Test]
		public void FirstHitHasNoBonus()
		{
			Assert.AreEqual(1f, HitComboRules.DamageMultiplier(0, 10f, 10), 0.0001f);
			Assert.AreEqual(1.3f, HitComboRules.DamageMultiplier(3, 10f, 10), 0.0001f);
		}

		[Test]
		public void BonusStopsAtCap() => Assert.AreEqual(2f, HitComboRules.DamageMultiplier(50, 10f, 10), 0.0001f);

		[Test]
		public void ApplyTruncates() => Assert.AreEqual(13, HitComboRules.Apply(10, 1.35f));

		[Test]
		public void FollowUpChainIsCapped()
		{
			int done = 0;
			while (HitComboRules.CanFollowUp(done, 3))
				done++;
			Assert.AreEqual(3, done);
			Assert.IsFalse(HitComboRules.CanFollowUp(0, 0));
		}
	}
}
