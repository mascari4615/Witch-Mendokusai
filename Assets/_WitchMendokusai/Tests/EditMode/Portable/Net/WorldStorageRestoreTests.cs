using NUnit.Framework;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 되살릴 때 상자를 잃지 않는다 (TASK-WM-217).
	///
	/// ★ 왜 이 시험이 있나 (실측 2026-08-10): 목록(무엇이 몇 칸짜리 상자인가)을 <b>꽂기 전에</b>
	///   세계를 되살렸더니 상자를 통째로 버렸고, 그 상태가 다시 저장되면서 넣어 둔 것이 사라졌다.
	///   파일에는 건물이 남아 있어 <b>아무 증상도 없었다</b> — 그래서 시험이 지켜야 한다.
	/// </summary>
	public sealed class WorldStorageRestoreTests
	{
		private const int CHEST = 4005;
		private const int WOOD = 0;

		private static readonly Vector3Int Here = new Vector3Int(1, 0, 3);

		private static WorldItemCatalog Items()
		{
			return new WorldItemCatalog(new ItemCatalogData
			{
				items = new[] { new ItemCatalogEntry { id = WOOD, name = "나무", maxAmount = 99 } },
			});
		}

		private static WorldBuildingCatalog Buildings()
		{
			return new WorldBuildingCatalog(new BuildingCatalogData
			{
				buildings = new[] { new BuildingCatalogEntry { id = CHEST, name = "보관 상자", w = 1, l = 1, slots = 30 } },
			});
		}

		private static WorldSaveData SavedWorldWithFullChest()
		{
			WorldSim world = new WorldSim { Buildables = Buildings() };
			world.TryPlaceBuilding(Here, CHEST, world.Buildables);
			world.Storages.Put(Here, Items().Find(WOOD), 6, Here.x, Here.z);

			return world.Save();
		}

		[Test]
		public void 목록을_꽂고_되살리면_상자_안이_그대로다()
		{
			WorldSim reborn = new WorldSim { Buildables = Buildings() };
			reborn.Load(SavedWorldWithFullChest(), Items());

			Assert.AreEqual(1, reborn.Storages.Contents(Here).Count);
			Assert.AreEqual(6, reborn.Storages.Contents(Here)[0].amount);
		}

		[Test]
		public void 목록을_안_꽂으면_상자를_잃는다()
		{
			// 이건 「고쳐야 할 버그」가 아니라 <b>규칙</b>이다: 몇 칸짜리인지 모르면 상자를 못 세운다.
			// 그러니 <b>부르는 쪽이 목록을 먼저 꽂아야 한다</b> — 이 시험은 그 계약을 적어 둔 것이다.
			WorldSim reborn = new WorldSim();
			reborn.Load(SavedWorldWithFullChest(), Items());

			Assert.IsFalse(reborn.Storages.Has(Here), "목록이 없으면 상자는 안 선다");
		}

		[Test]
		public void 되살린_세계를_다시_저장해도_안_잃는다()
		{
			// 실제 유실은 여기서 났다 — 잃은 채로 다시 저장되면 파일에서 영영 사라진다.
			WorldSim reborn = new WorldSim { Buildables = Buildings() };
			reborn.Load(SavedWorldWithFullChest(), Items());

			WorldSaveData again = reborn.Save();

			Assert.AreEqual(1, again.storages.Length, "다시 저장한 파일에도 상자가 있어야 한다");
			Assert.AreEqual(6, again.storages[0].items[0].amount);
		}

		[Test]
		public void 아이템_목록이_없으면_상자는_서되_안은_빈다()
		{
			// 모르는 물건을 지어내지 않는다 — 서 있는 상자는 남기되 내용은 비운다.
			WorldSim reborn = new WorldSim { Buildables = Buildings() };
			reborn.Load(SavedWorldWithFullChest(), null);

			Assert.IsTrue(reborn.Storages.Has(Here));
			Assert.AreEqual(0, reborn.Storages.Contents(Here).Count);
		}
	}
}
