using NUnit.Framework;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Tests
{
	/// <summary>「그건 몇 칸짜리인가」는 세계가 안다 (TASK-WM-217).</summary>
	public sealed class WorldBuildingCatalogTests
	{
		private const int HOUSE = 4000;
		private const int POST = 4001;

		private static WorldBuildingCatalog Catalog()
		{
			return new WorldBuildingCatalog(new BuildingCatalogData
			{
				buildings = new[]
				{
					new BuildingCatalogEntry { id = HOUSE, name = "집", w = 3, l = 3 },
					new BuildingCatalogEntry { id = POST, name = "말뚝", w = 1, l = 1 },
				},
			});
		}

		[Test]
		public void 크기는_목록이_정한다()
		{
			Assert.IsTrue(Catalog().TrySize(HOUSE, out int width, out int length));
			Assert.AreEqual(3, width);
			Assert.AreEqual(3, length);
		}

		[Test]
		public void 모르는_건물은_안_선다()
		{
			WorldSim world = new WorldSim { Buildables = Catalog() };

			Assert.IsFalse(world.TryPlaceBuilding(new Vector3Int(0, 0, 0), 9999, world.Buildables),
				"세계가 모르는 것을 지어 주면 창이 아무 번호나 지어 낸다");
		}

		[Test]
		public void 창이_작다고_우겨도_제_크기로_선다()
		{
			// 3×3 집을 짓고, 그 옆 한 칸(원래대로면 겹치는 자리)에 또 지으려 한다.
			WorldSim world = new WorldSim { Buildables = Catalog() };

			Assert.IsTrue(world.TryPlaceBuilding(new Vector3Int(0, 0, 0), HOUSE, world.Buildables));
			Assert.IsFalse(world.TryPlaceBuilding(new Vector3Int(-1, 0, 1), HOUSE, world.Buildables),
				"작다고 우겨도 3×3 자리를 먹으므로 겹친다");
		}

		[Test]
		public void 목록이_비면_아무것도_못_짓는다()
		{
			WorldSim world = new WorldSim();

			Assert.AreEqual(0, world.Buildables.Count);
			Assert.IsFalse(world.TryPlaceBuilding(new Vector3Int(0, 0, 0), HOUSE, world.Buildables));
		}

		[Test]
		public void 이름이_없으면_번호로_부른다()
		{
			WorldBuildingCatalog catalog = new WorldBuildingCatalog(new BuildingCatalogData
			{
				buildings = new[] { new BuildingCatalogEntry { id = 7, name = "", w = 1, l = 1 } },
			});

			Assert.AreEqual("#7", catalog.NameOf(7));
			Assert.AreEqual("#123", catalog.NameOf(123), "모르는 번호도 빈칸으로 두지 않는다");
		}
	}
}
