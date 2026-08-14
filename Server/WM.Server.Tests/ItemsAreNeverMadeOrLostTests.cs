using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using WitchMendokusai;
using WitchMendokusai.Numerics;
using WitchMendokusai.Server;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// 물건은 <b>옮겨질 뿐</b> 늘지도 줄지도 않는다 (TASK-WM-376).
	///
	/// ★ 왜: 상자와 가방 사이를 <b>둘이 동시에</b> 오갈 때가 제일 위험하다 —
	///   「꺼냈는데 상자에도 남는다」(늘어남)나 「넣었는데 어디에도 없다」(사라짐)는
	///   조용히 일어나고, 사람은 한참 뒤에야 「내 것이 없어졌다」로 겪는다.
	///   겨루기 관문(WM-330·352)은 <b>한 자리</b>를 둘이 노리는 판을 본다.
	///   여기서는 <b>총량</b>을 본다 — 세계 어디에 있든 개수의 합은 그대로여야 한다.
	/// </summary>
	/// <remarks>
	/// [빨강-확인] 상자에서 꺼내는 자리의 <b>자물쇠를 빼</b> 보니 빨강 —
	/// 「꺼낸 합 42개 — 상자에 있던 40개보다 많으면 물건이 지어진 것이다」 (2026-08-14).
	/// 넷이 달려들면 그 틈으로 <b>없던 물건이 생긴다</b>.
	/// </remarks>
	public sealed class ItemsAreNeverMadeOrLostTests
	{
		private const int ITEM = 10;
		private static readonly Vector3Int CHEST = new Vector3Int(0, 0, 0);

		private static WorldSim WorldWithChest(int stock)
		{
			WorldSim world = new WorldSim
			{
				Gatherables = new WorldGatherables(WorldSeeds.Gatherables()),
				Buildables = ServerBuildingCatalog.Catalog,
			};

			Assert.That(world.TryPlaceBuilding(CHEST, 4005, world.Buildables), Is.True, "상자를 못 지었다");
			Assert.That(world.Storages.Put(CHEST, ServerItemCatalog.Find(ITEM), stock, CHEST.x, CHEST.z), Is.Zero,
				"상자에 처음 물건을 못 넣었다 — 이 시험이 잴 것이 없다");
			return world;
		}

		private static int HowManyInWorld(WorldSim world, params int[] dollIds)
		{
			int inChest = world.Storages.Contents(CHEST).Where((one) => one.itemId == ITEM).Sum((one) => one.amount);
			int inBags = dollIds.Sum((dollId) => world.BagOf(dollId).Where((one) => one.itemId == ITEM).Sum((one) => one.amount));
			return inChest + inBags;
		}

		/// <summary>★ 둘이 <b>같은 순간</b> 꺼내고 넣어도 총량은 그대로다.</summary>
		[Test]
		public async Task 둘이_동시에_오가도_개수는_그대로다()
		{
			const int STOCK = 60;
			WorldSim world = WorldWithChest(STOCK);
			WorldDoll first = world.Join(identityId: 11, ServerItemCatalog.Catalog);
			WorldDoll second = world.Join(identityId: 12, ServerItemCatalog.Catalog);

			Assert.That(HowManyInWorld(world, first.Id, second.Id), Is.EqualTo(STOCK), "시작이 맞아야 끝을 본다");

			Task taking = Task.Run(() =>
			{
				for (int turn = 0; turn < 200; turn++)
				{
					int took = world.Storages.Take(CHEST, ITEM, 1, CHEST.x, CHEST.z);
					if (took > 0)
						world.TryGather(first.Id, ServerItemCatalog.Find(ITEM), took);
				}
			});

			Task putting = Task.Run(() =>
			{
				for (int turn = 0; turn < 200; turn++)
				{
					int left = world.TryConsume(first.Id, ITEM, 1);
					if (left == 0)
						world.Storages.Put(CHEST, ServerItemCatalog.Find(ITEM), 1, CHEST.x, CHEST.z);
				}
			});

			await Task.WhenAll(taking, putting);

			Assert.That(HowManyInWorld(world, first.Id, second.Id), Is.EqualTo(STOCK),
				"오가는 사이에 개수가 바뀌었다 — 늘었으면 물건이 지어진 것이고, 줄었으면 사라진 것이다");
		}

		/// <summary>꺼내기가 <b>가진 것보다 많이</b> 나오지 않는다 — 여럿이 달려들어도.</summary>
		[Test]
		public async Task 여럿이_달려들어도_상자에_있던_만큼만_나온다()
		{
			const int STOCK = 40;
			WorldSim world = WorldWithChest(STOCK);

			int[] got = new int[4];
			await Task.WhenAll(Enumerable.Range(0, 4).Select((who) => Task.Run(() =>
			{
				for (int turn = 0; turn < 100; turn++)
					got[who] += world.Storages.Take(CHEST, ITEM, 1, CHEST.x, CHEST.z);
			})));

			int left = world.Storages.Contents(CHEST).Where((one) => one.itemId == ITEM).Sum((one) => one.amount);

			Assert.That(got.Sum(), Is.EqualTo(STOCK), $"꺼낸 합 {got.Sum()}개 — 상자에 있던 {STOCK}개보다 많으면 물건이 지어진 것이다");
			Assert.That(left, Is.Zero, "다 꺼냈으면 상자는 비어야 한다");
		}
	}
}
