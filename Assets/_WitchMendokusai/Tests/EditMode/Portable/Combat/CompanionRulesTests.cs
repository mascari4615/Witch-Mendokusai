using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class CompanionRulesTests
	{
		[Test]
		public void HitStackBonus_CapsAtMaxStacks()
		{
			Assert.AreEqual(9, DollPassiveRules.HitStackBonus(3, 3, 20));
			Assert.AreEqual(60, DollPassiveRules.HitStackBonus(99, 3, 20));
			Assert.AreEqual(297, DollPassiveRules.HitStackBonus(99, 3, 0));
			Assert.AreEqual(0, DollPassiveRules.HitStackBonus(-2, 3, 20));
		}

		[Test]
		public void TiersReached_CountsThresholdsAtOrBelowLevel()
		{
			int[] thresholds = { 10, 20, 35 };
			Assert.AreEqual(0, DollPassiveRules.TiersReached(9, thresholds));
			Assert.AreEqual(1, DollPassiveRules.TiersReached(10, thresholds));
			Assert.AreEqual(2, DollPassiveRules.TiersReached(34, thresholds));
			Assert.AreEqual(3, DollPassiveRules.TiersReached(99, thresholds));
			Assert.AreEqual(0, DollPassiveRules.TiersReached(99, null));
		}

		[Test]
		public void Shield_AbsorbsOnceThenRechargesAfterCooldown()
		{
			ShieldState shield = new(1, 10f);
			Assert.IsTrue(shield.TryAbsorb());
			Assert.IsFalse(shield.HasShield);
			Assert.IsFalse(shield.TryAbsorb());
			shield.Tick(9.9f);
			Assert.IsFalse(shield.HasShield);
			shield.Tick(0.2f);
			Assert.IsTrue(shield.HasShield);
		}

		[Test]
		public void Shield_AdvanceOnHitShortensRecharge()
		{
			ShieldState shield = new(1, 10f);
			shield.TryAbsorb();
			shield.Advance(4f);
			Assert.AreEqual(0.4f, shield.RechargeRatio, 0.0001f);
			shield.Advance(6f);
			Assert.IsTrue(shield.HasShield);
		}

		[Test]
		public void Shield_FullShieldIgnoresAdvance_AndTimerStartsOnFirstBreak()
		{
			ShieldState shield = new(2, 10f);
			shield.Advance(50f);
			Assert.AreEqual(2, shield.Charges);
			shield.TryAbsorb();
			Assert.AreEqual(10f, shield.Remain, 0.0001f);
			shield.TryAbsorb();
			Assert.AreEqual(10f, shield.Remain, 0.0001f);
			shield.Tick(10f);
			Assert.AreEqual(1, shield.Charges);
			shield.Tick(10f);
			Assert.AreEqual(2, shield.Charges);
		}

		[Test]
		public void TimedSummon_RatioFallsAndExpires()
		{
			TimedSummonState summon = new(8f);
			Assert.AreEqual(1f, summon.Ratio, 0.0001f);
			summon.Tick(2f);
			Assert.AreEqual(0.75f, summon.Ratio, 0.0001f);
			Assert.IsFalse(summon.IsExpired);
			summon.Tick(6f);
			Assert.IsTrue(summon.IsExpired);
			Assert.AreEqual(0f, summon.Ratio, 0.0001f);
		}

		[Test]
		public void SummonRules_BlockedAtMaxOrWhileCooling()
		{
			Assert.IsTrue(SummonRules.CanSummon(0, 1, 0f));
			Assert.IsFalse(SummonRules.CanSummon(1, 1, 0f));
			Assert.IsFalse(SummonRules.CanSummon(0, 1, 3f));
		}

		[Test]
		public void LevelUpPulse_FiresOnLevelUpRegardlessOfCooldown_AndRestartsCooldown()
		{
			LevelUpPulseState pulse = new(45f);
			Assert.IsFalse(pulse.Tick(10f));
			Assert.IsTrue(pulse.OnLevelUp());
			Assert.AreEqual(45f, pulse.Remain, 0.0001f);
			Assert.IsFalse(pulse.Tick(44f));
			Assert.IsTrue(pulse.Tick(1f));
		}

		[Test]
		public void CompanionRush_RunsConfiguredRushesThenReturnsAndCools()
		{
			CompanionRushState rush = new(8f, 0.6f, 10f);
			Assert.IsFalse(rush.IsReady);
			rush.TickFollow(8f);
			Assert.IsTrue(rush.IsReady);
			Assert.IsTrue(rush.BeginRush(2));
			Assert.AreEqual(CompanionPhase.Approach, rush.Phase);
			rush.ArriveAtTarget();
			Assert.AreEqual(CompanionPhase.Dash, rush.Phase);
			Assert.IsFalse(rush.TickDash(0.5f));
			Assert.IsTrue(rush.TickDash(0.2f));
			Assert.IsTrue(rush.NextRushOrFinish());
			Assert.AreEqual(CompanionPhase.Approach, rush.Phase);
			rush.ArriveAtTarget();
			rush.TickDash(1f);
			Assert.IsFalse(rush.NextRushOrFinish());
			Assert.AreEqual(CompanionPhase.Follow, rush.Phase);
			Assert.AreEqual(8f, rush.CooldownRemain, 0.0001f);
		}

		[Test]
		public void CompanionRush_CooldownOnlyTicksWhileFollowing_AndPullAbortsFarRush()
		{
			CompanionRushState rush = new(8f, 0.6f, 10f);
			rush.TickFollow(8f);
			rush.BeginRush(1);
			rush.TickFollow(5f);
			Assert.AreEqual(0f, rush.CooldownRemain, 0.0001f);
			Assert.IsFalse(rush.ShouldPull(9.9f));
			Assert.IsTrue(rush.ShouldPull(10f));
			rush.Abort();
			Assert.AreEqual(CompanionPhase.Follow, rush.Phase);
			Assert.IsFalse(rush.ShouldPull(50f));
			Assert.AreEqual(8f, rush.CooldownRemain, 0.0001f);
		}
	}
}
