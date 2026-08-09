using NUnit.Framework;
using WitchMendokusai.DomainSDK.Alchemy;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 지은 솥과 젓던 자국이 껐다 켜도 남는다 (TASK-WM-217).
	///
	/// ★ 왜: 건물만 남고 솥이 안 남으면 <b>지은 솥에서 못 젓는 세계</b>가 된다 —
	///   화면엔 솥이 서 있으니 사람은 「고장」으로 읽는다.
	/// </summary>
	public sealed class WorldCauldronSaveTests
	{
		private const int CAULDRON = WorldSim.CAULDRON_BUILDING_ID;

		private static readonly Vector3Int Here = new Vector3Int(4, 0, 6);

		private static WorldBuildingCatalog Buildings()
		{
			return new WorldBuildingCatalog(new BuildingCatalogData
			{
				buildings = new[] { new BuildingCatalogEntry { id = CAULDRON, name = "솥", w = 1, l = 1 } },
			});
		}

		private static WorldSaveData SavedWorldWithStirredPot()
		{
			WorldSim world = new WorldSim { Buildables = Buildings() };
			world.TryPlaceBuilding(Here, CAULDRON, world.Buildables);
			world.Cauldrons.At(Here).AddStep(new BrewStep { Direction = new BrewVector(1f, 0f), Grind = 0.5f });
			world.Cauldrons.At(Here).AddStep(new BrewStep { Direction = new BrewVector(0f, 1f), Grind = 0.5f });

			return world.Save();
		}

		[Test]
		public void 지은_솥이_껐다_켜도_남는다()
		{
			WorldSim reborn = new WorldSim { Buildables = Buildings() };
			reborn.Load(SavedWorldWithStirredPot(), null);

			Assert.IsTrue(reborn.Cauldrons.Has(Here), "건물만 남고 솥이 없으면 못 젓는다");
		}

		[Test]
		public void 젓던_자국도_그대로다()
		{
			WorldSim reborn = new WorldSim { Buildables = Buildings() };
			reborn.Load(SavedWorldWithStirredPot(), null);

			BrewState state = reborn.Cauldrons.At(Here).State;
			Assert.AreEqual(2, state.StepCount);
			Assert.AreEqual(0.5f, state.Position.X, 0.0001f);
			Assert.AreEqual(0.5f, state.Position.Y, 0.0001f);
		}

		[Test]
		public void 다시_저장해도_안_잃는다()
		{
			WorldSim reborn = new WorldSim { Buildables = Buildings() };
			reborn.Load(SavedWorldWithStirredPot(), null);

			WorldSaveData again = reborn.Save();

			Assert.AreEqual(1, again.cauldrons.Length);
			Assert.AreEqual(2, again.cauldrons[0].path.Length, "저은 길이 정본이다 — 그게 사라지면 자국이 거짓이 된다");
		}
	}
}
