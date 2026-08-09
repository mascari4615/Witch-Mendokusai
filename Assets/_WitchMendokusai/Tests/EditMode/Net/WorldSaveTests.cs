using NUnit.Framework;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Tests.EditMode.Net
{
	/// <summary>
	/// 「내가 없을 때도 세계가 있다」 — 껐다 켜도 지은 게 남는지 (TASK-WM-217 단계 5).
	/// </summary>
	public sealed class WorldSaveTests
	{
		private static WorldSim WithBuildings()
		{
			WorldSim world = new WorldSim();
			world.TryPlaceBuilding(new Vector3Int(0, 0, 0), new Vector2Int(2, 2), 7);
			world.TryPlaceBuilding(new Vector3Int(10, 0, 10), new Vector2Int(1, 1), 3);
			return world;
		}

		[Test]
		public void 지은_건물은_뜬_기억에_그대로_들어간다()
		{
			WorldSaveData saved = WithBuildings().Save();

			Assert.That(saved.buildings.Length, Is.EqualTo(2));
			Assert.That(saved.buildings[0].w, Is.EqualTo(2));
			Assert.That(saved.buildings[1].buildingId, Is.EqualTo(3));
		}

		[Test]
		public void 새_세계에_기억을_넣으면_건물이_되살아난다()
		{
			WorldSaveData saved = WithBuildings().Save();

			WorldSim reborn = new WorldSim();
			int restored = reborn.Load(saved);

			Assert.That(restored, Is.EqualTo(2));
			Assert.That(reborn.Buildings().Length, Is.EqualTo(2));
			Assert.That(reborn.CountBuildings(7), Is.EqualTo(1));
		}

		[Test]
		public void 되살린_자리에는_다시_못_짓는다()
		{
			WorldSim reborn = new WorldSim();
			reborn.Load(WithBuildings().Save());

			// 겹침 판정이 같이 되살아나야 한다 — 안 그러면 되살린 건물 위에 또 지어진다.
			Assert.That(reborn.TryPlaceBuilding(new Vector3Int(0, 0, 0), new Vector2Int(1, 1), 9), Is.False);
			Assert.That(reborn.TryPlaceBuilding(new Vector3Int(50, 0, 50), new Vector2Int(1, 1), 9), Is.True);
		}

		[Test]
		public void 겹친_기억은_버린다()
		{
			WorldSaveData broken = new WorldSaveData
			{
				buildings = new[]
				{
					new BuildingSaveData { x = 0, y = 0, z = 0, w = 2, l = 2, buildingId = 1 },
					new BuildingSaveData { x = 0, y = 0, z = 0, w = 1, l = 1, buildingId = 2 },
				},
			};

			WorldSim world = new WorldSim();
			int restored = world.Load(broken);

			Assert.That(restored, Is.EqualTo(1));
			Assert.That(world.Buildings().Length, Is.EqualTo(1));
		}

		[Test]
		public void 되살리기는_지금_있는_건물을_갈아끼운다()
		{
			WorldSim world = WithBuildings();

			int restored = world.Load(new WorldSaveData());

			Assert.That(restored, Is.EqualTo(0));
			Assert.That(world.Buildings().Length, Is.EqualTo(0));
			// 비운 자리에는 다시 지어져야 한다(칸 점유도 같이 비워졌다는 뜻).
			Assert.That(world.TryPlaceBuilding(new Vector3Int(0, 0, 0), new Vector2Int(2, 2), 7), Is.True);
		}

		[Test]
		public void 기억이_없어도_터지지_않는다()
		{
			WorldSim world = WithBuildings();

			Assert.That(world.Load(null), Is.EqualTo(0));
			Assert.That(world.Buildings().Length, Is.EqualTo(0));
		}

		[Test]
		public void 사람은_저장하지_않는다()
		{
			WorldSim world = new WorldSim();
			world.Join();

			WorldSaveData saved = world.Save();
			WorldSim reborn = new WorldSim();
			reborn.Load(saved);

			// 인형 번호는 접속마다 새로 준다 — 되살린 세계에 사람이 남아 있으면 유령이다.
			Assert.That(reborn.Snapshot().Length, Is.EqualTo(0));
		}
	}
}
