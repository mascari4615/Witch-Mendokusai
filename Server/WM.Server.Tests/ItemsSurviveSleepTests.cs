using System.Linq;
using NUnit.Framework;
using WitchMendokusai;
using WitchMendokusai.Numerics;
using WitchMendokusai.Server;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// 세계를 <b>껐다 켜도</b> 총량은 그대로다 (TASK-WM-380).
	///
	/// ★ 왜: WM-376 은 <b>도는 세계 안</b>에서만 총량을 봤다. 그런데 세계는 배포마다 껐다 켜진다 —
	///   그 길에서 새면 아무도 그 순간을 못 본다(사람은 며칠 뒤 「내 것이 없어졌다」로 겪는다).
	///   가방 남기(BagSurvivesRestart)와 상자 남기는 <b>따로</b> 지켜졌지만 <b>합</b>은 아무도 안 봤다.
	/// </summary>
	/// <remarks>
	/// [빨강-확인] 저장할 때 <b>상자 칸을 안 적게</b> 해 보니 둘 다 빨강 —
	/// 「껐다 켜는 사이에 개수가 바뀌었다」 (2026-08-14).
	/// </remarks>
	public sealed class ItemsSurviveSleepTests
	{
		private const int ITEM = 10;
		private static readonly Vector3Int CHEST = new Vector3Int(0, 0, 0);

		private static WorldSim FreshWorld()
		{
			return new WorldSim
			{
				Gatherables = new WorldGatherables(WorldSeeds.Gatherables()),
				Buildables = ServerBuildingCatalog.Catalog,
			};
		}

		private static int HowManyInWorld(WorldSim world, params int[] dollIds)
		{
			int inChest = world.Storages.Contents(CHEST).Where((one) => one.itemId == ITEM).Sum((one) => one.amount);
			int inBags = dollIds.Sum((dollId) => world.BagOf(dollId).Where((one) => one.itemId == ITEM).Sum((one) => one.amount));
			return inChest + inBags;
		}

		[Test]
		public void 상자에_반_가방에_반_두고_껐다_켜도_합은_그대로다()
		{
			const int STOCK = 30;
			WorldSim before = FreshWorld();
			Assert.That(before.TryPlaceBuilding(CHEST, 4005, before.Buildables), Is.True, "상자를 못 지었다");
			Assert.That(before.Storages.Put(CHEST, ServerItemCatalog.Find(ITEM), STOCK, CHEST.x, CHEST.z), Is.Zero, "상자에 못 넣었다");

			WorldDoll doll = before.Join(identityId: 21, ServerItemCatalog.Catalog);
			int took = before.Storages.Take(CHEST, ITEM, STOCK / 2, CHEST.x, CHEST.z);
			before.TryGather(doll.Id, ServerItemCatalog.Find(ITEM), took);
			Assert.That(HowManyInWorld(before, doll.Id), Is.EqualTo(STOCK), "자기 전부터 어긋났으면 잴 것이 없다");
			before.Leave(doll.Id);

			WorldSim after = FreshWorld();
			after.Load(before.Save(), ServerItemCatalog.Catalog);
			WorldDoll again = after.Join(identityId: 21, ServerItemCatalog.Catalog);

			Assert.That(HowManyInWorld(after, again.Id), Is.EqualTo(STOCK),
				"껐다 켜는 사이에 개수가 바뀌었다 — 늘었으면 지어진 것이고, 줄었으면 사라진 것이다");
			Assert.That(after.BagOf(again.Id).Where((one) => one.itemId == ITEM).Sum((one) => one.amount),
				Is.EqualTo(STOCK / 2), "가방 몫이 상자로 옮겨 가 있으면 합만 맞고 남의 것이 된다");
		}

		/// <summary>★ 두 번 자도 그대로다 — 한 번은 맞고 두 번째에 새는 자리가 있다(되살린 값을 다시 저장할 때).</summary>
		[Test]
		public void 두_번_껐다_켜도_그대로다()
		{
			const int STOCK = 12;
			WorldSim world = FreshWorld();
			Assert.That(world.TryPlaceBuilding(CHEST, 4005, world.Buildables), Is.True);
			world.Storages.Put(CHEST, ServerItemCatalog.Find(ITEM), STOCK, CHEST.x, CHEST.z);
			WorldDoll doll = world.Join(identityId: 22, ServerItemCatalog.Catalog);
			world.TryGather(doll.Id, ServerItemCatalog.Find(ITEM), world.Storages.Take(CHEST, ITEM, 4, CHEST.x, CHEST.z));
			world.Leave(doll.Id);

			for (int sleep = 0; sleep < 2; sleep++)
			{
				WorldSim next = FreshWorld();
				next.Load(world.Save(), ServerItemCatalog.Catalog);
				world = next;
			}

			WorldDoll again = world.Join(identityId: 22, ServerItemCatalog.Catalog);
			Assert.That(HowManyInWorld(world, again.Id), Is.EqualTo(STOCK),
				"두 번째 잠에서 새면 배포를 두 번 할 때마다 세계가 조금씩 가난해진다");
		}
	}
}
