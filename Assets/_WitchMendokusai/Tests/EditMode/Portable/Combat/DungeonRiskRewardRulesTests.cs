using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class DungeonRiskRewardRulesTests
	{
		private static readonly int[] MILESTONES = { 1, 2, 3 };

		[Test]
		public void FirstClearPaysEveryMilestoneUpToClearedRisk()
		{
			CollectionAssert.AreEqual(new[] { 0, 1 }, DungeonRiskRewardRules.NewlyReached(MILESTONES, new HashSet<int>(), 2));
		}

		[Test]
		public void LaterClearPaysOnlyUnclaimedMilestones()
		{
			HashSet<int> claimed = new HashSet<int> { 1, 2 };
			CollectionAssert.AreEqual(new[] { 2 }, DungeonRiskRewardRules.NewlyReached(MILESTONES, claimed, 5));
			CollectionAssert.IsEmpty(DungeonRiskRewardRules.NewlyReached(MILESTONES, new HashSet<int> { 1, 2, 3 }, 3));
			CollectionAssert.IsEmpty(DungeonRiskRewardRules.NewlyReached(MILESTONES, claimed, 1));
		}

		[Test]
		public void MilestoneAddedBelowBestIsStillPaid()
		{
			// 최고 3 까지 받은 뒤 구간 2 를 새로 넣은 경우. 최고 기록이 아니라 받은 목록으로 판정
			int[] milestones = { 1, 2, 3 };
			HashSet<int> claimed = new HashSet<int> { 1, 3 };
			CollectionAssert.AreEqual(new[] { 1 }, DungeonRiskRewardRules.NewlyReached(milestones, claimed, 2));
		}

		[Test]
		public void RiskZeroClearPaysNothing()
		{
			CollectionAssert.IsEmpty(DungeonRiskRewardRules.NewlyReached(MILESTONES, new HashSet<int>(), 0));
		}

		[Test]
		public void BestKeepsHighestClear()
		{
			Assert.AreEqual(3, DungeonRiskRewardRules.AfterClear(3, 1));
			Assert.AreEqual(4, DungeonRiskRewardRules.AfterClear(3, 4));
		}

		[Test]
		public void StateSeparatesClaimedThisClearAndUnreached()
		{
			HashSet<int> claimed = new HashSet<int> { 1, 2 };
			Assert.AreEqual(DungeonRiskMilestoneState.Claimed, DungeonRiskRewardRules.State(1, claimed, 0));
			Assert.AreEqual(DungeonRiskMilestoneState.ThisClear, DungeonRiskRewardRules.State(3, claimed, 3));
			Assert.AreEqual(DungeonRiskMilestoneState.Unreached, DungeonRiskRewardRules.State(3, claimed, 2));
		}

		[Test]
		public void TopMilestoneIsHighestRisk()
		{
			Assert.AreEqual(3, DungeonRiskRewardRules.TopMilestone(new List<int> { 2, 3, 1 }));
			Assert.AreEqual(0, DungeonRiskRewardRules.TopMilestone(new List<int>()));
		}
	}
}
