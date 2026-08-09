using System.IO;
using NUnit.Framework;
using WitchMendokusai.Numerics;
using WitchMendokusai.Server;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// 세계가 디스크에 남는지 — 「서버를 껐다 켜도 지은 게 있다」 (TASK-WM-217 단계 5).
	/// </summary>
	public sealed class WorldStoreTests
	{
		private string path;

		[SetUp]
		public void SetUp()
		{
			path = Path.Combine(Path.GetTempPath(), "wm-world-test-" + Path.GetRandomFileName() + ".json");
		}

		[TearDown]
		public void TearDown()
		{
			if (File.Exists(path))
				File.Delete(path);

			if (File.Exists(path + ".tmp"))
				File.Delete(path + ".tmp");

			if (File.Exists(path + ".bak"))
				File.Delete(path + ".bak");
		}

		[Test]
		public void 껐다_켜도_지은_게_남는다()
		{
			WorldSim before = new WorldSim();
			before.TryPlaceBuilding(new Vector3Int(4, 0, 4), new Vector2Int(2, 3), 11);

			WorldStore store = new WorldStore(path);
			Assert.IsTrue(store.TrySave(before.Save()));

			WorldSim after = new WorldSim();
			int restored = after.Load(store.TryLoad());

			Assert.AreEqual(1, restored);
			Assert.AreEqual(1, after.CountBuildings(11));
			Assert.IsFalse(after.TryPlaceBuilding(new Vector3Int(4, 0, 4), new Vector2Int(1, 1), 99));
		}

		[Test]
		public void 세계의_시각도_창에게_간다()
		{
			WorldSim world = new WorldSim();
			world.AdvanceMinutes(90f);

			string snapshot = Protocol.WorldSnapshot(world.Snapshot(), world.Buildings(), world.Calendar);

			StringAssert.Contains("\"time\":", snapshot);
			StringAssert.Contains("\"hour\":" + world.Calendar.Hour, snapshot);
		}

		[Test]
		public void 시각도_껐다_켜면_이어진다()
		{
			WorldSim before = new WorldSim();
			before.AdvanceMinutes(5f * 24f * 60f + 137f);

			WorldStore store = new WorldStore(path);
			store.TrySave(before.Save());

			WorldSim after = new WorldSim();
			after.Load(store.TryLoad());

			Assert.AreEqual(before.Calendar.Day, after.Calendar.Day);
			Assert.AreEqual(before.Calendar.Hour, after.Calendar.Hour);
			Assert.AreEqual(before.Calendar.Minute, after.Calendar.Minute);
		}

		[Test]
		public void 지금_기억이_깨지면_앞_판으로_되살린다()
		{
			WorldStore store = new WorldStore(path);

			WorldSim first = new WorldSim();
			first.TryPlaceBuilding(new Vector3Int(0, 0, 0), new Vector2Int(1, 1), 1);
			store.TrySave(first.Save());

			WorldSim second = new WorldSim();
			second.TryPlaceBuilding(new Vector3Int(5, 0, 5), new Vector2Int(1, 1), 2);
			store.TrySave(second.Save()); // 이 시점에 앞 판(건물 1개)이 .bak 으로 넘어간다

			File.WriteAllText(path, "{ 망가짐");

			WorldSaveData loaded = store.TryLoad();

			// 모두의 신원 장부가 같이 든 파일이라 「그냥 빈 세계」로 뜨면 전원이 처음 온 사람이 된다.
			Assert.IsNotNull(loaded);
			Assert.AreEqual(1, loaded.buildings.Length);
		}

		[Test]
		public void 둘_다_못_읽으면_빈_세계로_뜬다()
		{
			WorldStore store = new WorldStore(path);
			store.TrySave(new WorldSaveData());
			File.WriteAllText(path, "{ 망가짐");
			if (File.Exists(store.BackupPath))
				File.WriteAllText(store.BackupPath, "{ 이것도 망가짐");

			// 안 뜨는 것보다 낫다.
			Assert.IsNull(store.TryLoad());
		}

		[Test]
		public void 기억이_아직_없으면_빈_세계로_시작한다()
		{
			WorldStore store = new WorldStore(path);

			Assert.IsNull(store.TryLoad());
		}

		[Test]
		public void 망가진_파일이어도_서버는_뜬다()
		{
			File.WriteAllText(path, "{ 이건 json 이 아니다");
			WorldStore store = new WorldStore(path);

			WorldSaveData loaded = store.TryLoad();

			// 못 읽는 건 어쩔 수 없지만, 못 읽었다고 서버가 안 뜨면 그게 더 나쁘다.
			Assert.IsNull(loaded);
			Assert.AreEqual(0, new WorldSim().Load(loaded));
		}

		[Test]
		public void 쓰다_만_임시파일은_남지_않는다()
		{
			WorldStore store = new WorldStore(path);
			store.TrySave(new WorldSaveData());

			Assert.IsTrue(File.Exists(path));
			Assert.IsFalse(File.Exists(path + ".tmp"));
		}

		[Test]
		public void 다시_저장하면_덮어쓴다()
		{
			WorldStore store = new WorldStore(path);
			WorldSim world = new WorldSim();
			world.TryPlaceBuilding(new Vector3Int(0, 0, 0), new Vector2Int(1, 1), 1);
			store.TrySave(world.Save());

			world.TryPlaceBuilding(new Vector3Int(5, 0, 5), new Vector2Int(1, 1), 2);
			store.TrySave(world.Save());

			WorldSim reborn = new WorldSim();
			Assert.AreEqual(2, reborn.Load(store.TryLoad()));
		}
	}
}
