using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Tests
{
	/// <summary>지은 상자에 물건을 넣고 꺼낸다 — 내가 넣은 걸 친구가 꺼낸다 (TASK-WM-217 후속).</summary>
	public sealed class WorldStoragesTests
	{
		private const int WOOD = 0;

		private static readonly Vector3Int Here = new Vector3Int(2, 0, 2);

		private static WorldItemCatalog Catalog()
		{
			return new WorldItemCatalog(new ItemCatalogData
			{
				items = new[] { new ItemCatalogEntry { id = WOOD, name = "나무", maxAmount = 99 } },
			});
		}

		private static IItemData Wood() => Catalog().Find(WOOD);

		[Test]
		public void 상자에_넣고_꺼낸다()
		{
			WorldStorages storages = new WorldStorages();
			storages.Place(Here, 10);

			Assert.AreEqual(0, storages.Put(Here, Wood(), 5, Here.x, Here.z), "칸이 남으면 다 들어간다");
			Assert.AreEqual(5, storages.Take(Here, WOOD, 5, Here.x, Here.z));
			Assert.AreEqual(0, storages.Contents(Here).Count, "다 꺼내면 빈 상자다");
		}

		[Test]
		public void 멀리서는_못_연다()
		{
			WorldStorages storages = new WorldStorages();
			storages.Place(Here, 10);

			Assert.AreEqual(3, storages.Put(Here, Wood(), 3, Here.x + 50f, Here.z), "손이 안 닿으면 아무것도 안 들어간다");
			storages.Put(Here, Wood(), 3, Here.x, Here.z);
			Assert.AreEqual(0, storages.Take(Here, WOOD, 3, Here.x, Here.z + 50f), "멀리서 꺼내 가면 그건 도둑질이다");
		}

		[Test]
		public void 없는_상자엔_아무것도_못_한다()
		{
			WorldStorages storages = new WorldStorages();

			Assert.AreEqual(2, storages.Put(Here, Wood(), 2, Here.x, Here.z));
			Assert.AreEqual(0, storages.Take(Here, WOOD, 2, Here.x, Here.z));
			Assert.IsFalse(storages.Has(Here));
		}

		[Test]
		public void 내가_넣은_것을_남이_꺼낸다()
		{
			// 상자는 세계의 것이다 — 자리만 같으면 누가 넣고 누가 꺼내든 같은 상자다.
			WorldStorages storages = new WorldStorages();
			storages.Place(Here, 10);
			storages.Put(Here, Wood(), 4, Here.x, Here.z);

			// 다른 사람이 와서(같은 자리 옆) 꺼낸다.
			Assert.AreEqual(4, storages.Take(Here, WOOD, 9, Here.x + 1f, Here.z + 1f), "있는 만큼만 나온다");
		}

		[Test]
		public void 칸이_차면_남는다()
		{
			WorldStorages storages = new WorldStorages();
			storages.Place(Here, 1); // 한 칸(최대 99)

			Assert.AreEqual(51, storages.Put(Here, Wood(), 150, Here.x, Here.z), "칸이 모자라면 남는다");
		}

		[Test]
		public void 껐다_켜도_상자_안이_그대로다()
		{
			WorldStorages storages = new WorldStorages();
			storages.Place(Here, 10);
			storages.Put(Here, Wood(), 7, Here.x, Here.z);

			WorldStorages reborn = new WorldStorages();
			reborn.Load(storages.Save(), cell => 10, Catalog());

			List<BagSaveEntry> contents = reborn.Contents(Here);
			Assert.AreEqual(1, contents.Count);
			Assert.AreEqual(7, contents[0].amount, "넣어 둔 게 사라지면 아무도 안 쓴다");
		}

		[Test]
		public void 부수면_상자도_사라진다()
		{
			WorldStorages storages = new WorldStorages();
			storages.Place(Here, 10);

			Assert.IsTrue(storages.Remove(Here));
			Assert.IsFalse(storages.Has(Here));
			Assert.IsFalse(storages.Remove(Here), "두 번 부술 수는 없다");
		}
	}
}
