using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using WitchMendokusai.Numerics;
using WitchMendokusai.Server;

namespace WitchMendokusai.Server.Tests
{
	/// <summary>
	/// 서버가 굴리는 세계의 규칙 (TASK-WM-216).
	/// 핵심은 하나 — <b>클라가 보낸 값을 그대로 믿지 않는다.</b>
	/// </summary>
	public sealed class WorldTests
	{
		[Test]
		public void 들어오면_인형을_하나_받는다()
		{
			WorldSim world = new WorldSim();

			WorldDoll first = world.Join();
			WorldDoll second = world.Join();

			Assert.AreNotEqual(first.Id, second.Id, "번호는 겹치지 않는다");
			Assert.AreEqual(2, world.Snapshot().Length);
		}

		[Test]
		public void 나가면_세계에서_사라진다()
		{
			WorldSim world = new WorldSim();
			WorldDoll doll = world.Join();

			world.Leave(doll.Id);

			Assert.AreEqual(0, world.Snapshot().Length);
		}

		[Test]
		public void 한_번에_갈_수_있는_거리를_넘으면_잘린다()
		{
			WorldSim world = new WorldSim();
			WorldDoll doll = world.Join();

			world.TryMove(doll.Id, new Vector3(100f, 0f, 0f));

			Assert.AreEqual(WorldSim.MAX_STEP, doll.Position.x, 0.001f, "순간이동은 공짜가 아니다");
		}

		[Test]
		public void 상한_안쪽_움직임은_그대로_간다()
		{
			WorldSim world = new WorldSim();
			WorldDoll doll = world.Join();

			world.TryMove(doll.Id, new Vector3(0.5f, 0f, 0.5f));

			Assert.AreEqual(0.5f, doll.Position.x, 0.001f);
			Assert.AreEqual(0.5f, doll.Position.z, 0.001f);
		}

		[Test]
		public void 없는_인형을_움직이라_하면_거절한다()
		{
			WorldSim world = new WorldSim();

			Assert.IsFalse(world.TryMove(999, new Vector3(1f, 0f, 0f)));
		}

		[Test]
		public void 훑는_동안_들락거려도_터지지_않는다()
		{
			// 실제로 이걸로 서버가 죽었다 — 알림 루프가 훑는 도중 접속·퇴장이 겹쳤다.
			WorldSim world = new WorldSim();
			for (int i = 0; i < 50; i++)
				world.Join();

			Task churn = Task.Run(() =>
			{
				for (int i = 0; i < 2000; i++)
				{
					WorldDoll doll = world.Join();
					world.Leave(doll.Id);
				}
			});

			for (int i = 0; i < 2000; i++)
			{
				WorldDoll[] snapshot = world.Snapshot();
				foreach (WorldDoll doll in snapshot)
					Assert.IsNotNull(doll, "사본에 빈 자리가 섞이면 안 된다");
			}

			Assert.DoesNotThrow(() => churn.Wait());
		}
	}

	/// <summary>
	/// 계약이 갈라지지 않았나 (TASK-WM-216).
	/// 웹이 쓰는 타입 선언은 서버가 뽑아낸다 — 손으로 고치면 이 시험이 잡는다.
	/// </summary>
	public sealed class ProtocolTests
	{
		/// <summary>줄 끝 차이(윈도우/리눅스)로 갈라졌다고 오판하지 않게 맞춘다.</summary>
		private static string Normalize(string text) => text.Replace("\r\n", "\n").TrimEnd();

		private static string GeneratedPath()
		{
			// 시험은 bin 안에서 돈다 — 저장소의 진짜 파일을 찾아 올라간다.
			System.IO.DirectoryInfo directory = new System.IO.DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && System.IO.Directory.Exists(System.IO.Path.Combine(directory.FullName, "WM.Server")) == false)
				directory = directory.Parent;

			Assert.IsNotNull(directory, "서버 폴더를 못 찾았다");
			return System.IO.Path.Combine(directory.FullName, "WM.Server", "wwwroot", "protocol.d.ts");
		}

		[Test]
		public void 뽑아낸_계약과_저장된_파일이_같다()
		{
			string expected = Normalize(Protocol.ToTypeScript());
			string path = GeneratedPath();

			Assert.IsTrue(System.IO.File.Exists(path), "생성물이 없다 — 계약을 한 번 뽑아 커밋할 것: " + path);

			string actual = Normalize(System.IO.File.ReadAllText(path));
			Assert.AreEqual(expected, actual, "계약이 갈라졌다 — 손으로 고치지 말고 Protocol.cs 에서 다시 뽑아라");
		}

		[Test]
		public void 계약에_세_가지_말이_들어_있다()
		{
			string typescript = Protocol.ToTypeScript();

			StringAssert.Contains("Welcome", typescript);
			StringAssert.Contains("WorldSnapshot", typescript);
			StringAssert.Contains("MoveRequest", typescript);
		}

		[Test]
		public void 낱말표는_이름을_그대로_싣는다()
		{
			string json = Protocol.Catalog(new[]
			{
				new System.Collections.Generic.KeyValuePair<int, string>(7, "치유 물약"),
			});

			StringAssert.Contains("\"itemId\":7", json);
			StringAssert.Contains("치유 물약", json);
		}

		[Test]
		public void 이름에_따옴표가_있어도_창이_안_깨진다()
		{
			// 사람이 지은 이름에 " 나 \ 가 들어가면, 감싸지 않은 낱말표는 그 자리에서 문장을 끊는다.
			string json = Protocol.Catalog(new[]
			{
				new System.Collections.Generic.KeyValuePair<int, string>(8, "이름에 \" 와 \\ 가 있다"),
			});

			System.Text.Json.JsonDocument parsed = System.Text.Json.JsonDocument.Parse(json);
			System.Text.Json.JsonElement first = parsed.RootElement.GetProperty("items")[0];

			Assert.AreEqual("이름에 \" 와 \\ 가 있다", first.GetProperty("name").GetString());
		}
	}

	/// <summary>서버가 「짓기」도 판정한다 (TASK-WM-216).</summary>
	public sealed class WorldBuildingTests
	{
		[Test]
		public void 빈_자리에는_지어진다()
		{
			WorldSim world = new WorldSim();

			bool placed = world.TryPlaceBuilding(new Vector3Int(0, 0, 0), new Vector2Int(2, 2), 4000);

			Assert.IsTrue(placed);
			Assert.AreEqual(1, world.Buildings().Length);
		}

		[Test]
		public void 겹치면_거절한다()
		{
			WorldSim world = new WorldSim();
			world.TryPlaceBuilding(new Vector3Int(0, 0, 0), new Vector2Int(2, 2), 4000);

			bool second = world.TryPlaceBuilding(new Vector3Int(-1, 0, 1), new Vector2Int(1, 1), 4000);

			Assert.IsFalse(second, "이미 깔린 칸이면 못 짓는다");
			Assert.AreEqual(1, world.Buildings().Length);
		}

		[Test]
		public void 옆_칸에는_지어진다()
		{
			WorldSim world = new WorldSim();
			world.TryPlaceBuilding(new Vector3Int(0, 0, 0), new Vector2Int(1, 1), 4000);

			bool second = world.TryPlaceBuilding(new Vector3Int(5, 0, 5), new Vector2Int(1, 1), 4000);

			Assert.IsTrue(second);
			Assert.AreEqual(2, world.Buildings().Length);
		}

		[Test]
		public void 종류별로_몇_개인지_센다()
		{
			WorldSim world = new WorldSim();
			world.TryPlaceBuilding(new Vector3Int(0, 0, 0), new Vector2Int(1, 1), 4000);
			world.TryPlaceBuilding(new Vector3Int(3, 0, 0), new Vector2Int(1, 1), 4000);
			world.TryPlaceBuilding(new Vector3Int(6, 0, 0), new Vector2Int(1, 1), 4004);

			Assert.AreEqual(2, world.CountBuildings(4000));
			Assert.AreEqual(1, world.CountBuildings(4004));
			Assert.AreEqual(0, world.CountBuildings(9999));
		}
	}

	/// <summary>서버가 가방도 굴린다 — 게임과 같은 규칙으로 (TASK-WM-216).</summary>
	public sealed class WorldBagTests
	{
		[Test]
		public void 주우면_가방에_쌓인다()
		{
			WorldSim world = new WorldSim();
			WorldDoll doll = world.Join();

			int leftover = world.TryGather(doll.Id, ServerItemCatalog.Find(ServerItemCatalog.STONE), 10);

			Assert.AreEqual(0, leftover);
			Assert.AreEqual(10, world.BagCount(doll.Id, ServerItemCatalog.STONE));
		}

		[Test]
		public void 가방을_통째로_묻는다()
		{
			// 창이 가방을 그리려면 「서버가 아는 두 종류」가 아니라 **든 것 전부**를 받아야 한다.
			WorldSim world = new WorldSim();
			WorldDoll doll = world.Join();

			world.TryGather(doll.Id, new ServerItemData(880001, 99), 3);
			world.TryGather(doll.Id, new ServerItemData(880002, 99), 7);

			System.Collections.Generic.List<BagSaveEntry> bag = world.BagOf(doll.Id);

			Assert.AreEqual(2, bag.Count, "두 종류를 넣었으면 두 종류가 나와야 한다");
			Assert.AreEqual(3, bag.Find(entry => entry.itemId == 880001).amount);
			Assert.AreEqual(7, bag.Find(entry => entry.itemId == 880002).amount);
		}

		[Test]
		public void 없는_인형의_가방은_빈_목록이다()
		{
			WorldSim world = new WorldSim();

			Assert.AreEqual(0, world.BagOf(9999).Count, "없는 사람을 물어도 죽지 않는다");
		}

		[Test]
		public void 모르는_아이템은_안_들어간다()
		{
			WorldSim world = new WorldSim();
			WorldDoll doll = world.Join();

			int leftover = world.TryGather(doll.Id, ServerItemCatalog.Find(9999), 5);

			Assert.AreEqual(5, leftover, "서버가 모르는 건 그대로 남는다");
		}

		[Test]
		public void 칸_최대치를_넘으면_다음_칸으로_간다()
		{
			WorldSim world = new WorldSim();
			WorldDoll doll = world.Join();

			// ⚠ 서버 목록에 기대지 않는다 — 게임에서 뽑은 목록이 옆에 있으면 한 칸 크기가 달라져
			//   이 시험이 「가방 규칙」이 아니라 「그날의 데이터」를 재게 된다(실측 2026-08-10).
			ServerItemData small = new ServerItemData(770001, 20);
			world.TryGather(doll.Id, small, 45); // 한 칸 20

			Assert.AreEqual(45, world.BagCount(doll.Id, small.ID), "세 칸에 나뉘어 들어간다");
		}

		[Test]
		public void 가방이_꽉_차면_남은_개수를_알려준다()
		{
			WorldSim world = new WorldSim();
			WorldDoll doll = world.Join();

			// 30칸 * 20 = 600 이 한계 (한 칸 크기는 시험이 정한다 — 데이터가 바뀌어도 규칙은 그대로다)
			ServerItemData small = new ServerItemData(770002, 20);
			int leftover = world.TryGather(doll.Id, small, 650);

			Assert.AreEqual(50, leftover);
			Assert.AreEqual(600, world.BagCount(doll.Id, small.ID));
		}

		[Test]
		public void 쓰면_줄어든다()
		{
			WorldSim world = new WorldSim();
			WorldDoll doll = world.Join();
			world.TryGather(doll.Id, ServerItemCatalog.Find(ServerItemCatalog.STONE), 30);

			int missing = world.TryConsume(doll.Id, ServerItemCatalog.STONE, 12);

			Assert.AreEqual(0, missing);
			Assert.AreEqual(18, world.BagCount(doll.Id, ServerItemCatalog.STONE));
		}

		[Test]
		public void 없는_인형의_가방은_비어_있다()
		{
			WorldSim world = new WorldSim();

			Assert.AreEqual(0, world.BagCount(123, ServerItemCatalog.STONE));
			Assert.AreEqual(3, world.TryGather(123, ServerItemCatalog.Find(ServerItemCatalog.STONE), 3));
		}
	}
}
