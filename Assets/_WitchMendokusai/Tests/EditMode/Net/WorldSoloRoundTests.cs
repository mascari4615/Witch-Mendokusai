using NUnit.Framework;
using WitchMendokusai.DomainSDK.Alchemy;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 혼자 노는 한 바퀴를 <b>파수꾼과 같은 순서</b>로 돌린다 (TASK-WM-217).
	///
	/// ★ 왜: 진짜 판 관문이 <c>potion=0</c> 으로 멈췄는데, 판정 층 시험은 전부 초록이었다.
	///   각 조각은 되는데 <b>이어 붙이면 안 되는</b> 자리가 있다는 뜻이다 — 그 자리를 여기서 잡는다.
	///   줍기 → 상자 짓기 → 넣고 꺼내기 → 솥 짓기 → 재료 넣기 → 완성.
	/// </summary>
	public sealed class WorldSoloRoundTests
	{
		private const int CHEST = 4005;

		/// <summary>시험용 낱말표 — 세계는 「무엇을 몇 개까지 들 수 있나」만 알면 된다.</summary>
		private static readonly WorldItemCatalog items = new WorldItemCatalog(new ItemCatalogData
		{
			items = new[]
			{
				new ItemCatalogEntry { id = WorldSeeds.WOOD, maxAmount = 500 },
				new ItemCatalogEntry { id = WorldSeeds.COAL, maxAmount = 500 },
				new ItemCatalogEntry { id = WorldSeeds.IRON, maxAmount = 500 },
				new ItemCatalogEntry { id = WorldSeeds.HEALING_POTION, maxAmount = 99 },
			},
		});

		private static WorldSim Seeded()
		{
			WorldSim world = new WorldSim
			{
				Gatherables = new WorldGatherables(WorldSeeds.Gatherables()),
				Ingredients = new WorldIngredients(WorldSeeds.Ingredients()),
				Buildables = new WorldBuildingCatalog(WorldSeeds.Buildings()),
			};

			return world;
		}

		[Test]
		public void 혼자서_한_바퀴가_끝까지_돈다()
		{
			WorldSim world = Seeded();
			WorldDoll me = world.Join();

			// ① 나무를 모은다 — 상자·솥을 짓고도 솥에 넣을 것이 남아야 한다.
			int wood = 0;
			foreach (GatherableNode node in world.Gatherables.Alive(0))
			{
				if (node.ItemId != WorldSeeds.WOOD)
					continue;

				world.TryMove(me.Id, new Vector3(node.X - world.PositionOf(me.Id).x, 0f, node.Z - world.PositionOf(me.Id).z));
				me.Position = new Vector3(node.X, 0f, node.Z);

				if (world.Gatherables.TryTake(node.Id, node.X, node.Z, 0, out int itemId, out int amount) == false)
					continue;

				world.TryGather(me.Id, items.Find(itemId), amount);
				wood += amount;
				if (wood >= 6)
					break;
			}

			Assert.GreaterOrEqual(wood, 6, "나무를 못 모으면 그 뒤 걸음이 전부 막힌다");

			// ② 상자를 짓는다 (재료가 빠진다).
			Vector3Int chest = new Vector3Int(Mathf.RoundToInt(me.Position.x), 0, Mathf.RoundToInt(me.Position.z));
			Assert.IsTrue(Pay(world, me, CHEST), "상자 재료가 모자란다");
			Assert.IsTrue(world.TryPlaceBuilding(chest, CHEST, world.Buildables), "상자를 못 지었다");

			// ③ 넣고 도로 꺼낸다.
			world.TryConsume(me.Id, WorldSeeds.WOOD, 1);
			Assert.AreEqual(0, world.Storages.Put(chest, items.Find(WorldSeeds.WOOD), 1, me.Position.x, me.Position.z),
				"상자가 안 받으면 나눔이 안 도는 세계다");

			Assert.AreEqual(1, world.Storages.Take(chest, WorldSeeds.WOOD, 1, me.Position.x, me.Position.z));
			world.TryGather(me.Id, items.Find(WorldSeeds.WOOD), 1);

			// ④ 솥을 <b>상자 옆에</b> 짓는다 — 파수꾼이 하는 그대로.
			Vector3Int potCell = new Vector3Int(chest.x + 1, 0, chest.z);
			Assert.IsTrue(Pay(world, me, WorldSim.CAULDRON_BUILDING_ID), "솥 재료가 모자란다");
			Assert.IsTrue(world.TryPlaceBuilding(potCell, WorldSim.CAULDRON_BUILDING_ID, world.Buildables),
				"솥을 못 지었다 — 상자 옆이 막혀 있다면 파수꾼은 영영 조리를 못 한다");

			// ⑤ 그 솥에 손이 닿나 (파수꾼은 상자 자리에 서 있다).
			WorldCauldron pot = world.Cauldrons.Reachable(potCell, me.Position.x, me.Position.z);
			Assert.IsNotNull(pot, "지어 놓고 손이 안 닿으면 조리는 시작도 못 한다");

			// ⑥ 재료를 넣는다.
			Assert.IsTrue(world.Ingredients.TryStep(WorldSeeds.WOOD, out BrewStep step), "나무가 솥에 못 들어간다");
			Assert.AreEqual(0, world.TryConsume(me.Id, WorldSeeds.WOOD, 1), "가방에 나무가 없다");
			pot.AddStep(step);

			// ⑦ 완성 — 여기까지 와서 0 이면 「놀 수 있다」가 거짓이다.
			WorldRecipeBook book = new WorldRecipeBook(WorldSeeds.Recipes());
			Assert.IsTrue(pot.TryComplete(book, out BrewCompletion taken), "완성을 못 가져간다");
			Assert.IsFalse(taken.Empty, "아무 쪽에도 안 닿았다");
			Assert.AreEqual(WorldSeeds.HEALING_POTION, taken.ResultItemId);
		}

		private static bool Pay(WorldSim world, WorldDoll me, int buildingId)
		{
			if (world.Buildables.TryCost(buildingId, out int itemId, out int amount) == false || amount <= 0)
				return true;

			return world.TryConsume(me.Id, itemId, amount) == 0;
		}
	}
}
