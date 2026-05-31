using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	// TASK-WM-169 Phase 1a — 보관 상자 인벤토리 코어 + RuntimeData JSON round-trip 잠금.
	// Editor/PlayMode 무관 (순수 POCO 왕복).
	public sealed class ChestInventoryTest
	{
		[Test]
		public void Add_Accumulates_SameItem()
		{
			ChestInventory inventory = new();
			inventory.Add(100, 2);
			inventory.Add(100, 3);
			Assert.AreEqual(5, inventory.GetCount(100));
		}

		[Test]
		public void Remove_DownToZero_ClearsKey()
		{
			ChestInventory inventory = new();
			inventory.Add(100, 2);
			Assert.IsTrue(inventory.Remove(100, 2));
			Assert.AreEqual(0, inventory.GetCount(100));
			Assert.IsFalse(inventory.ItemCounts.ContainsKey(100));
		}

		[Test]
		public void Remove_MoreThanHeld_Fails()
		{
			ChestInventory inventory = new();
			inventory.Add(100, 1);
			Assert.IsFalse(inventory.Remove(100, 2));
			Assert.AreEqual(1, inventory.GetCount(100));
		}

		[Test]
		public void JsonRoundTrip_PreservesContents()
		{
			ChestInventory original = new();
			original.Add(100, 2);
			original.Add(205, 7);

			string json = original.ToJson();
			ChestInventory restored = ChestInventory.FromJson(json);

			Assert.AreEqual(2, restored.GetCount(100));
			Assert.AreEqual(7, restored.GetCount(205));
		}

		[Test]
		public void FromJson_EmptyOrNull_GivesEmptyInventory()
		{
			Assert.AreEqual(0, ChestInventory.FromJson("").GetCount(100));
			Assert.AreEqual(0, ChestInventory.FromJson(null).GetCount(100));
		}
	}
}
