using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 「이만큼 받을 자리가 있나」 (TASK-WM-217).
	///
	/// ★ 왜: 솥 완성처럼 <b>되돌릴 수 없는 보상</b>은 넣어 보고 버리면 안 된다 —
	///   사람 눈엔 「만들었는데 사라졌다」다. 먼저 묻고, 자리가 없으면 아예 안 준다.
	/// </summary>
	public sealed class InventoryCanReceiveTests
	{
		private sealed class Stuff : IItemData
		{
			public Stuff(int id, int max)
			{
				ID = id;
				MaxAmount = max;
			}

			public int ID { get; }
			public int MaxAmount { get; }
			public ItemType Type => default;
			public ItemGrade Grade => default;
		}

		private static InventoryCore Bag(int slotCount)
		{
			List<Item> slots = new List<Item>();
			for (int i = 0; i < slotCount; i++)
				slots.Add(null);

			return new InventoryCore(slots, slotCount);
		}

		[Test]
		public void 빈_가방은_받는다()
		{
			Assert.IsTrue(Bag(2).CanReceive(new Stuff(1, 10), 20));
		}

		[Test]
		public void 자리가_모자라면_못_받는다()
		{
			Assert.IsFalse(Bag(1).CanReceive(new Stuff(1, 10), 11), "한 칸에 10개까지면 11개는 못 받는다");
		}

		[Test]
		public void 같은_것이_들어_있으면_그_여유도_센다()
		{
			InventoryCore bag = Bag(1);
			Stuff wood = new Stuff(1, 10);
			bag.Add(wood, 7);

			Assert.IsTrue(bag.CanReceive(wood, 3), "남은 3자리에 들어간다");
			Assert.IsFalse(bag.CanReceive(wood, 4));
		}

		[Test]
		public void 꽉_찬_가방은_못_받는다()
		{
			InventoryCore bag = Bag(1);
			Stuff wood = new Stuff(1, 10);
			bag.Add(wood, 10);

			Assert.IsFalse(bag.CanReceive(wood, 1));
			Assert.IsFalse(bag.CanReceive(new Stuff(2, 10), 1), "다른 물건도 넣을 칸이 없다");
		}

		[Test]
		public void 없는_물건은_못_받는다()
		{
			Assert.IsFalse(Bag(3).CanReceive(null, 1));
			Assert.IsFalse(Bag(3).CanReceive(new Stuff(1, 10), 0));
		}
	}
}
