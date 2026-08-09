using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 가방 규칙이 <b>엔진 없이도</b> 같은 답을 낸다 (TASK-WM-215).
	/// 서버가 가방을 굴려야 웹에서도 줍고 만들 수 있다.
	/// </summary>
	public sealed class InventoryCoreTests
	{
		private sealed class FakeItemData : IItemData
		{
			public FakeItemData(int id, int maxAmount)
			{
				ID = id;
				MaxAmount = maxAmount;
			}

			public int ID { get; }
			public int MaxAmount { get; }
			public ItemType Type => default;
			public ItemGrade Grade => default;
		}

		private static InventoryCore MakeInventory(int capacity, out List<Item> slots)
		{
			slots = new List<Item>();
			for (int i = 0; i < capacity; i++)
				slots.Add(null);

			return new InventoryCore(slots, capacity);
		}

		[Test]
		public void 쌓이는_아이템은_한_칸에_모인다()
		{
			InventoryCore inventory = MakeInventory(5, out List<Item> slots);
			FakeItemData stone = new FakeItemData(1, 99);

			int excess = inventory.Add(stone, 10);

			Assert.AreEqual(0, excess, "다 들어가야 한다");
			Assert.AreEqual(10, slots[0].Amount);
			Assert.IsNull(slots[1], "두 번째 칸은 비어 있어야 한다");
		}

		[Test]
		public void 칸_최대치를_넘으면_다음_칸으로_넘어간다()
		{
			InventoryCore inventory = MakeInventory(5, out List<Item> slots);
			FakeItemData stone = new FakeItemData(1, 10);

			int excess = inventory.Add(stone, 25);

			Assert.AreEqual(0, excess);
			Assert.AreEqual(10, slots[0].Amount);
			Assert.AreEqual(10, slots[1].Amount);
			Assert.AreEqual(5, slots[2].Amount);
		}

		[Test]
		public void 가방이_꽉_차면_남은_개수를_돌려준다()
		{
			InventoryCore inventory = MakeInventory(2, out List<Item> _);
			FakeItemData stone = new FakeItemData(1, 10);

			int excess = inventory.Add(stone, 25);

			Assert.AreEqual(5, excess, "두 칸(10+10)만 차고 5 가 남는다");
		}

		[Test]
		public void 안_쌓이는_아이템은_한_칸에_하나씩_들어간다()
		{
			InventoryCore inventory = MakeInventory(5, out List<Item> slots);
			FakeItemData sword = new FakeItemData(2, 1);

			int excess = inventory.Add(sword, 3);

			Assert.AreEqual(0, excess);
			Assert.IsNotNull(slots[0]);
			Assert.IsNotNull(slots[1]);
			Assert.IsNotNull(slots[2]);
			Assert.IsNull(slots[3]);
		}

		[Test]
		public void 빼면_수량이_줄고_0_이_되면_칸이_빈다()
		{
			InventoryCore inventory = MakeInventory(3, out List<Item> slots);
			FakeItemData stone = new FakeItemData(1, 99);
			inventory.Add(stone, 5);

			inventory.Remove(0, 2);
			Assert.AreEqual(3, slots[0].Amount);

			inventory.Remove(0, 3);
			Assert.IsNull(slots[0], "다 빼면 칸이 비어야 한다");
		}

		[Test]
		public void 칸이_바뀌면_바깥에_알린다()
		{
			InventoryCore inventory = MakeInventory(3, out List<Item> _);
			List<int> changed = new List<int>();
			inventory.SlotChanged += index => changed.Add(index);

			inventory.Add(new FakeItemData(1, 99), 4);

			Assert.AreEqual(1, changed.Count, "한 칸만 바뀌었다");
			Assert.AreEqual(0, changed[0]);
		}

		[Test]
		public void 없는_칸을_건드리면_아무_일도_안_일어난다()
		{
			InventoryCore inventory = MakeInventory(2, out List<Item> slots);

			inventory.Remove(99);
			inventory.SetItem(-1, null);

			Assert.IsNull(slots[0]);
			Assert.IsNull(slots[1]);
		}

		[Test]
		public void 같은_종류를_다시_넣으면_있던_칸부터_채운다()
		{
			InventoryCore inventory = MakeInventory(5, out List<Item> slots);
			FakeItemData stone = new FakeItemData(1, 10);
			inventory.Add(stone, 4);

			inventory.Add(stone, 3);

			Assert.AreEqual(7, slots[0].Amount, "빈 칸을 새로 쓰지 않고 있던 칸에 얹는다");
			Assert.IsNull(slots[1]);
		}
	}
}
