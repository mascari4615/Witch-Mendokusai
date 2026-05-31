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

		// 리뷰 C1 — 음수/0 값은 불변식(저장값>0) 위반이라 로드 시 드롭.
		[Test]
		public void FromJson_DropsNonPositiveValues()
		{
			ChestInventory restored = ChestInventory.FromJson("{\"100\":-5,\"200\":0,\"300\":4}");
			Assert.AreEqual(0, restored.GetCount(100));
			Assert.IsFalse(restored.ItemCounts.ContainsKey(100));
			Assert.IsFalse(restored.ItemCounts.ContainsKey(200));
			Assert.AreEqual(4, restored.GetCount(300));
		}

		// 리뷰 C2/S1 — 손상/악의적 JSON 이 throw 로 전체 로드를 죽이지 않고 빈 인벤토리 폴백.
		[Test]
		public void FromJson_MalformedJson_DoesNotThrow_GivesEmpty()
		{
			Assert.DoesNotThrow(() => ChestInventory.FromJson("{not valid"));
			Assert.AreEqual(0, ChestInventory.FromJson("{not valid").GetCount(100));
			Assert.AreEqual(0, ChestInventory.FromJson("[1,2,3]").GetCount(100));
			Assert.AreEqual(0, ChestInventory.FromJson("garbage").GetCount(100));
			Assert.AreEqual(0, ChestInventory.FromJson("{\"abc\":1}").GetCount(100));
		}

		[Test]
		public void Add_NonPositive_IsNoOp()
		{
			ChestInventory inventory = new();
			inventory.Add(100, 0);
			inventory.Add(100, -3);
			Assert.AreEqual(0, inventory.GetCount(100));
			Assert.IsFalse(inventory.ItemCounts.ContainsKey(100));
		}

		[Test]
		public void Remove_NonPositiveOrAbsent_Fails()
		{
			ChestInventory inventory = new();
			Assert.IsFalse(inventory.Remove(100, 1));
			inventory.Add(100, 2);
			Assert.IsFalse(inventory.Remove(100, 0));
			Assert.IsFalse(inventory.Remove(100, -1));
			Assert.AreEqual(2, inventory.GetCount(100));
		}
	}
}
