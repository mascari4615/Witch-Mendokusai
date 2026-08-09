using NUnit.Framework;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Tests.EditMode.Net
{
	/// <summary>
	/// 「다시 들어와도 내 것이 있다」 (TASK-WM-218 단계 2) — 신원별 자리·가방.
	/// </summary>
	public sealed class WorldPeopleMemoryTests
	{
		private const int STONE = 1;

		private static WorldItemCatalog Catalog() => new WorldItemCatalog(new ItemCatalogData
		{
			items = new[] { new ItemCatalogEntry { id = STONE, maxAmount = 99 } },
		});

		[Test]
		public void 나갔다_와도_가방이_그대로다()
		{
			WorldSim world = new WorldSim();
			WorldItemCatalog catalog = Catalog();

			WorldDoll first = world.Join(identityId: 7, catalog: catalog);
			world.TryGather(first.Id, catalog.Find(STONE), 5);
			world.Leave(first.Id);

			WorldDoll again = world.Join(identityId: 7, catalog: catalog);

			Assert.That(world.BagCount(again.Id, STONE), Is.EqualTo(5));
			Assert.That(again.Id, Is.Not.EqualTo(first.Id), "인형 번호는 새로 줘도 된다 — 이어지는 건 신원이다.");
		}

		[Test]
		public void 나갔다_와도_서_있던_자리다()
		{
			WorldSim world = new WorldSim();
			WorldDoll doll = world.Join(identityId: 3, catalog: null);
			world.TryMove(doll.Id, new Vector3(1f, 0f, 1f));
			Vector3 where = doll.Position;
			world.Leave(doll.Id);

			WorldDoll again = world.Join(identityId: 3, catalog: null);

			Assert.That(again.Position.x, Is.EqualTo(where.x).Within(0.001f));
			Assert.That(again.Position.z, Is.EqualTo(where.z).Within(0.001f));
		}

		[Test]
		public void 남의_것은_안_준다()
		{
			WorldSim world = new WorldSim();
			WorldItemCatalog catalog = Catalog();
			WorldDoll mine = world.Join(identityId: 7, catalog: catalog);
			world.TryGather(mine.Id, catalog.Find(STONE), 5);
			world.Leave(mine.Id);

			WorldDoll stranger = world.Join(identityId: 8, catalog: catalog);

			Assert.That(world.BagCount(stranger.Id, STONE), Is.EqualTo(0));
			Assert.That(stranger.Position.x, Is.EqualTo(0f));
		}

		[Test]
		public void 세계를_껐다_켜도_내_것이_있다()
		{
			WorldSim before = new WorldSim();
			WorldItemCatalog catalog = Catalog();
			WorldDoll doll = before.Join(identityId: 12, catalog: catalog);
			before.TryGather(doll.Id, catalog.Find(STONE), 9);
			before.TryMove(doll.Id, new Vector3(1.2f, 0f, 0f));

			// 접속 중이어도 저장에 담긴다 — 서버가 그대로 꺼져도 안 잃는다.
			WorldSaveData saved = before.Save();

			WorldSim after = new WorldSim();
			after.Load(saved);
			WorldDoll again = after.Join(identityId: 12, catalog: catalog);

			Assert.That(after.BagCount(again.Id, STONE), Is.EqualTo(9));
			Assert.That(again.Position.x, Is.EqualTo(1.2f).Within(0.001f));
		}

		[Test]
		public void 세계가_모르는_물건은_조용히_버린다()
		{
			WorldSim world = new WorldSim();
			world.LoadPeople(new[]
			{
				new PersonSaveData
				{
					identityId = 5,
					bag = new[] { new BagSaveEntry { itemId = 999, amount = 3 } },
				},
			});

			WorldDoll doll = world.Join(identityId: 5, catalog: Catalog());

			// 목록에서 빠진 아이템 하나 때문에 가방이 안 열리면 안 된다.
			Assert.That(world.BagCount(doll.Id, 999), Is.EqualTo(0));
			Assert.That(doll, Is.Not.Null);
		}

		[Test]
		public void 신원_없이_들어오면_옛_방식_그대로다()
		{
			WorldSim world = new WorldSim();
			WorldDoll first = world.Join();
			world.TryMove(first.Id, new Vector3(1f, 0f, 0f));
			world.Leave(first.Id);

			WorldDoll second = world.Join();

			// 회귀 0: 신원을 안 쓰는 경로는 전과 똑같이 매번 새 인형이다.
			Assert.That(second.Position.x, Is.EqualTo(0f));
			Assert.That(world.SavePeople(), Is.Empty);
		}
	}
}
