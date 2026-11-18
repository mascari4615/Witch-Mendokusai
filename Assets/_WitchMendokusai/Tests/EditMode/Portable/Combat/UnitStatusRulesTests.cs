using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class UnitStatusRulesTests
	{
		[Test]
		public void EveryStatusHasStatSlot()
		{
			foreach (UnitStatusType type in UnitStatusRules.ALL)
				Assert.AreNotEqual(UnitStatType.NONE, UnitStatusRules.StatType(type));
			Assert.AreEqual(UnitStatType.FREEZE, UnitStatusRules.StatType(UnitStatusType.Freeze));
		}

		[Test]
		public void SlotIndexMatchesTypeValue()
		{
			for (int i = 0; i < UnitStatusRules.Count; i++)
				Assert.AreEqual(i, (int)UnitStatusRules.ALL[i]);
		}

		[Test]
		public void SecondsBecomeMilliseconds()
		{
			Assert.AreEqual(8000, UnitStatusRules.Milliseconds(8f));
			Assert.AreEqual(0, UnitStatusRules.Milliseconds(0f));
			Assert.AreEqual(0, UnitStatusRules.Milliseconds(-1f));
		}

		[Test]
		public void RefreshKeepsLongerWithoutStacking()
		{
			Assert.AreEqual(8000, UnitStatusRules.Refresh(0, 8000));
			Assert.AreEqual(8000, UnitStatusRules.Refresh(8000, 3000));
			Assert.AreEqual(9000, UnitStatusRules.Refresh(2000, 9000));
		}

		[Test]
		public void BossDurationIsShorterOrImmune()
		{
			Assert.AreEqual(2f, UnitStatusRules.BossSeconds(8f, 0.25f), 0.0001f);
			Assert.AreEqual(0f, UnitStatusRules.BossSeconds(8f, 0f), 0.0001f);
			Assert.AreEqual(8f, UnitStatusRules.BossSeconds(8f, 3f), 0.0001f);
		}

		[Test]
		public void TickDamageUsesLargerOfPercentAndMinimum()
		{
			Assert.AreEqual(20, UnitStatusRules.TickDamage(1000, 2f, 1));
			Assert.AreEqual(1, UnitStatusRules.TickDamage(10, 2f, 1));
			Assert.AreEqual(0, UnitStatusRules.TickDamage(0, 2f, 0));
		}

		[Test]
		public void ElapseStopsAtZero()
		{
			Assert.AreEqual(500, UnitStatusRules.Elapse(1000, 500));
			Assert.AreEqual(0, UnitStatusRules.Elapse(300, 500));
		}
	}
}
