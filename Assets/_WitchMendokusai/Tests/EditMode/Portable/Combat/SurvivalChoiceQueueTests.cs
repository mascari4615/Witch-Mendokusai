using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	// CardManager 레벨업 선택과 상자 대기열 흐름
	public sealed class SurvivalChoiceQueueTests
	{
		[Test]
		public void FirstLevelUpStartsSelectionAndLaterOnesWait()
		{
			SurvivalChoiceQueue queue = new SurvivalChoiceQueue();

			Assert.IsTrue(queue.AddLevelUp());
			Assert.IsFalse(queue.AddLevelUp());
			Assert.IsFalse(queue.AddLevelUp());
			Assert.AreEqual(3, queue.PendingLevelUps);
		}

		[Test]
		public void EachChoiceConsumesOneLevelUpUntilEmpty()
		{
			SurvivalChoiceQueue queue = new SurvivalChoiceQueue();
			queue.AddLevelUp();
			queue.AddLevelUp();

			queue.ConsumeLevelUp();
			Assert.AreEqual(1, queue.PendingLevelUps);
			queue.ConsumeLevelUp();
			Assert.AreEqual(0, queue.PendingLevelUps);
			queue.ConsumeLevelUp();
			Assert.AreEqual(0, queue.PendingLevelUps);
			Assert.IsTrue(queue.AddLevelUp());
		}

		[Test]
		public void ChestPickedDuringSelectionWaitsUntilSelectionsEnd()
		{
			SurvivalChoiceQueue queue = new SurvivalChoiceQueue();
			queue.AddLevelUp();
			queue.AddLevelUp();
			queue.AddChest();

			Assert.IsFalse(queue.TryOpenChest());
			queue.ConsumeLevelUp();
			Assert.IsFalse(queue.TryOpenChest());
			queue.ConsumeLevelUp();

			Assert.IsTrue(queue.TryOpenChest());
			Assert.IsTrue(queue.IsChestOpen);
			Assert.AreEqual(0, queue.PendingChests);
		}

		[Test]
		public void LevelUpDuringOpenChestWaitsAndChestsOpenOneAtATime()
		{
			SurvivalChoiceQueue queue = new SurvivalChoiceQueue();
			queue.AddChest();
			queue.AddChest();
			Assert.IsTrue(queue.TryOpenChest());
			Assert.IsFalse(queue.TryOpenChest());

			Assert.IsFalse(queue.AddLevelUp());
			Assert.IsTrue(queue.CloseChest());
			Assert.IsFalse(queue.CloseChest());

			Assert.IsFalse(queue.TryOpenChest());
			queue.ConsumeLevelUp();
			Assert.IsTrue(queue.TryOpenChest());
			Assert.AreEqual(0, queue.PendingChests);
		}

		[Test]
		public void TakeAllLevelUpsEmptiesQueueAndLetsWaitingChestOpen()
		{
			SurvivalChoiceQueue queue = new SurvivalChoiceQueue();
			queue.AddLevelUp();
			queue.AddLevelUp();
			queue.AddLevelUp();
			queue.AddChest();

			Assert.AreEqual(3, queue.TakeAllLevelUps());
			Assert.AreEqual(0, queue.PendingLevelUps);
			Assert.IsTrue(queue.TryOpenChest());
		}

		[Test]
		public void CancelSelectionKeepsWaitingChestsButResetRunDropsThem()
		{
			SurvivalChoiceQueue queue = new SurvivalChoiceQueue();
			queue.AddChest();
			queue.AddChest();
			Assert.IsTrue(queue.TryOpenChest());
			queue.AddLevelUp();

			queue.CancelSelection();
			Assert.AreEqual(0, queue.PendingLevelUps);
			Assert.IsFalse(queue.IsChestOpen);
			Assert.AreEqual(1, queue.PendingChests);

			queue.ResetRun();
			Assert.AreEqual(0, queue.PendingChests);
			Assert.IsFalse(queue.TryOpenChest());
			Assert.IsTrue(queue.AddLevelUp());
		}
	}
}
