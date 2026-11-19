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
		public void StacksGrowToLimitAndRestartWhenExpired()
		{
			Assert.AreEqual(1, UnitStatusRules.NextStacks(0, false, 5));
			Assert.AreEqual(2, UnitStatusRules.NextStacks(1, true, 5));
			Assert.AreEqual(5, UnitStatusRules.NextStacks(5, true, 5));
			Assert.AreEqual(1, UnitStatusRules.NextStacks(4, false, 5));
			// 상한 1 은 쌓이지 않음
			Assert.AreEqual(1, UnitStatusRules.NextStacks(1, true, 1));
			Assert.AreEqual(1, UnitStatusRules.NextStacks(1, true, 0));
		}

		[Test]
		public void StackedTickDamageMultipliesByStacks()
		{
			Assert.AreEqual(UnitStatusRules.TickDamage(1000, 0.5f, 1), UnitStatusRules.StackedTickDamage(1000, 0.5f, 1, 1));
			Assert.AreEqual(5 * 3, UnitStatusRules.StackedTickDamage(1000, 0.5f, 1, 3));
			// 쌓임 0 이하는 1 로
			Assert.AreEqual(5, UnitStatusRules.StackedTickDamage(1000, 0.5f, 1, 0));
			Assert.AreEqual(5, UnitStatusRules.StackedTickDamage(10, 0.5f, 1, 5));
		}

		[Test]
		public void MarkAmplifiesDamageAndRoundsUp()
		{
			Assert.AreEqual(13, UnitStatusRules.AmplifiedDamage(10, 30f));
			Assert.AreEqual(2, UnitStatusRules.AmplifiedDamage(1, 30f));
			Assert.AreEqual(10, UnitStatusRules.AmplifiedDamage(10, 0f));
			Assert.AreEqual(0, UnitStatusRules.AmplifiedDamage(0, 30f));
		}

		[Test]
		public void ChainDamageIsPercentOfTickWithMinimumOne()
		{
			Assert.AreEqual(6, UnitStatusRules.ChainDamage(10, 60f));
			Assert.AreEqual(1, UnitStatusRules.ChainDamage(1, 10f));
			Assert.AreEqual(0, UnitStatusRules.ChainDamage(10, 0f));
			Assert.AreEqual(0, UnitStatusRules.ChainDamage(0, 60f));
		}

		[Test]
		public void RingFrameShrinksWithRemainingTime()
		{
			Assert.AreEqual(47, UnitStatusRules.RingFrame(3000, 3000, 48));
			Assert.AreEqual(0, UnitStatusRules.RingFrame(1, 3000, 48));
			Assert.AreEqual(23, UnitStatusRules.RingFrame(1500, 3000, 48));
			Assert.AreEqual(0, UnitStatusRules.RingFrame(0, 3000, 48));
			// 전체 시간을 모르면 가득, 남은 시간이 전체보다 크면 가득
			Assert.AreEqual(47, UnitStatusRules.RingFrame(500, 0, 48));
			Assert.AreEqual(47, UnitStatusRules.RingFrame(4000, 3000, 48));
		}

		[Test]
		public void ElapseStopsAtZero()
		{
			Assert.AreEqual(500, UnitStatusRules.Elapse(1000, 500));
			Assert.AreEqual(0, UnitStatusRules.Elapse(300, 500));
		}
	}
}
