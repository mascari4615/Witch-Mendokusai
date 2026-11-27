using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 방 꾸미기 봇. 아늑함 단계에 닿는 가구 조합과 걸리는 판 수를 잼
	// 마을 한 바퀴는 HomesteadPlayTest 의 봇을 그대로 빌려 씀 (같은 판 흐름에서 집만 더함)
	public class HomesteadHousingBotTest
	{
		private const float STEP = 0.25f;
		private const int RUNS = 120;

		private static readonly string[][] SOURCE_SETS =
		{
			new[] { HomesteadSim.BENCH },
			new[] { HomesteadSim.BENCH, HomesteadSim.SHOP },
			new[] { HomesteadSim.BENCH, HomesteadSim.SHOP, HomesteadSim.WORKSHOP },
			new[] { HomesteadSim.BENCH, HomesteadSim.SHOP, HomesteadSim.WORKSHOP, HomesteadSim.REWARD_ONLY },
		};

		// ---- 빈 곳 찾기. 바닥과 깔개는 구석부터 훑고, 벽 가구는 네 벽을 따라 훑음

		internal static bool PlaceSomewhere(HomesteadSim sim, FurnitureDef furniture)
		{
			HouseTierDef room = sim.HouseTier;
			if (furniture.Layer == FurnitureLayer.Wall)
			{
				foreach ((float x, float z) in WallSamples(room))
				{
					float snappedX = x;
					float snappedZ = z;
					RoomRules.SnapToWall(furniture, room, ref snappedX, ref snappedZ, out float yaw);
					if (sim.Place(furniture.Id, snappedX, snappedZ, yaw, out PlacedFurniture _).Ok)
					{
						return true;
					}
				}
				return false;
			}
			foreach (float yaw in new[] { 0f, 90f })
			{
				for (float z = -room.Depth / 2f; z <= room.Depth / 2f; z += STEP)
				{
					for (float x = -room.Width / 2f; x <= room.Width / 2f; x += STEP)
					{
						if (sim.CanPlace(furniture.Id, x, z, yaw).Ok)
						{
							return sim.Place(furniture.Id, x, z, yaw, out PlacedFurniture _).Ok;
						}
					}
				}
			}
			return false;
		}

		private static IEnumerable<(float, float)> WallSamples(HouseTierDef room)
		{
			float halfWidth = room.Width / 2f - 0.05f;
			float halfDepth = room.Depth / 2f - 0.05f;
			for (float x = -halfWidth; x <= halfWidth; x += STEP)
			{
				yield return (x, halfDepth);
				yield return (x, -halfDepth);
			}
			for (float z = -halfDepth; z <= halfDepth; z += STEP)
			{
				yield return (halfWidth, z);
				yield return (-halfWidth, z);
			}
		}

		// 가진 것 중 아늑함이 가장 많이 오르는 것부터 놓음. 자리가 없는 종류는 건너뜀
		internal static void FillRoom(HomesteadSim sim)
		{
			HashSet<string> noRoom = new();
			while (sim.State.Placed.Count < sim.HouseTier.PieceLimit)
			{
				int now = sim.Comfort;
				FurnitureDef best = null;
				int bestGain = 0;
				foreach (FurnitureDef furniture in sim.Catalog.Furniture)
				{
					if (noRoom.Contains(furniture.Id) || sim.SpareCount(furniture.Id) < 1)
					{
						continue;
					}
					sim.State.Placed.Add(new PlacedFurniture { Id = furniture.Id });
					int gain = sim.Comfort - now;
					sim.State.Placed.RemoveAt(sim.State.Placed.Count - 1);
					if (gain > bestGain)
					{
						best = furniture;
						bestGain = gain;
					}
				}
				if (best == null)
				{
					return;
				}
				if (PlaceSomewhere(sim, best) == false)
				{
					noRoom.Add(best.Id);
				}
			}
		}

		// ---- 1. 얻는 길별, 집 단계별 최대 아늑함 (가구는 넉넉히 가졌다고 침)

		[Test]
		public void Comfort_ceiling_by_source_and_room()
		{
			int[] steps = HomesteadTestKit.LoadCatalog().Tuning.ComfortSteps.ToArray();
			int[,] ceiling = new int[SOURCE_SETS.Length, 4];
			for (int set = 0; set < SOURCE_SETS.Length; set++)
			{
				for (int tier = 1; tier <= 4; tier++)
				{
					HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
					sim.State.HouseTier = tier;
					foreach (FurnitureDef furniture in sim.Catalog.Furniture)
					{
						if (Array.IndexOf(SOURCE_SETS[set], furniture.Source) >= 0)
						{
							sim.AddFurniture(furniture.Id, 4);
						}
					}
					FillRoom(sim);
					ceiling[set, tier - 1] = sim.Comfort;
					TestContext.WriteLine($"{string.Join("+", SOURCE_SETS[set]),-30} {sim.HouseTier.Name,-8} 가구 {sim.State.Placed.Count,2}/{sim.HouseTier.PieceLimit,2} 종류 {Kinds(sim),2} 아늑함 {sim.Comfort,3} ({sim.ComfortRank}단)");
				}
			}
			Assert.That(ceiling[0, 0], Is.GreaterThanOrEqualTo(steps[2]), "첫 방, 제작대 가구만으로 2단");
			Assert.That(ceiling[SOURCE_SETS.Length - 1, 3], Is.GreaterThanOrEqualTo(steps[steps.Length - 1]), "다 모으면 끝 단계");
		}

		private static int Kinds(HomesteadSim sim)
		{
			HashSet<string> kinds = new();
			sim.State.Placed.ForEach(piece => kinds.Add(piece.Id));
			return kinds.Count;
		}

		// ---- 2. 마을 봇에 집을 더해 단계마다 걸리는 판 수

		private static readonly MethodInfo PLAY_DUNGEON = typeof(HomesteadPlayTest).GetMethod("PlayDungeon", BindingFlags.NonPublic | BindingFlags.Static);
		private static readonly MethodInfo PLAY_TOWN = typeof(HomesteadPlayTest).GetMethod("PlayTown", BindingFlags.NonPublic | BindingFlags.Static);
		private static readonly MethodInfo KEEPS = typeof(HomesteadPlayTest).GetMethod("KeepsTownHall", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(HomesteadSim), typeof(CostDef) }, null);

		internal sealed class HousingRun
		{
			public readonly List<int> RankRuns = new();
			public readonly List<int> TierRuns = new();
			public readonly List<int> HallRuns = new();
			public int TownHall;
			public int Comfort;
			public int Kinds;
			public int Pieces;
			public string Room = string.Empty;
		}

		// thrifty: 타운홀 승급 재료는 남김. eager: 낼 수 있으면 바로 집에 씀
		internal static HousingRun PlayHousing(int seed, bool thrifty)
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse, seed);
			purse.Nyang = 300;
			Random random = new(seed);
			HousingRun result = new();
			for (int run = 0; run < RUNS; run++)
			{
				PLAY_DUNGEON.Invoke(null, new object[] { sim, purse, random, run });
				PLAY_TOWN.Invoke(null, new object[] { sim, random });
				while (sim.CanUpgradeTownHall().Ok)
				{
					sim.UpgradeTownHall();
					result.HallRuns.Add(run + 1);
				}
				bool Keeps(CostDef cost) => thrifty == false || (bool)KEEPS.Invoke(null, new object[] { sim, cost });
				if (sim.TryGetNextHouseTier(out HouseTierDef next) && sim.CanExpandHouse().Ok && Keeps(next.Cost))
				{
					sim.ExpandHouse();
					result.TierRuns.Add(run + 1);
				}
				foreach (FurnitureDef furniture in sim.Catalog.Furniture)
				{
					if (sim.OwnedCount(furniture.Id) == 0)
					{
						BenchCraftFor(sim, furniture.Cost, Keeps);
					}
					if (sim.OwnedCount(furniture.Id) == 0 && sim.CanMakeFurniture(furniture.Id).Ok && Keeps(furniture.Cost))
					{
						sim.MakeFurniture(furniture.Id);
					}
				}
				// 방에 자리가 남으면 같은 것을 하나 더 (거듭 놓으면 절반씩이라 셋까지)
				for (int copies = 2; copies <= 3; copies++)
				{
					foreach (FurnitureDef furniture in sim.Catalog.Furniture)
					{
						if (TotalOwned(sim) >= sim.HouseTier.PieceLimit || sim.OwnedCount(furniture.Id) != copies - 1)
						{
							continue;
						}
						BenchCraftFor(sim, furniture.Cost, Keeps);
						if (sim.CanMakeFurniture(furniture.Id).Ok && Keeps(furniture.Cost))
						{
							sim.MakeFurniture(furniture.Id);
						}
					}
				}
				sim.PickUpAll();
				FillRoom(sim);
				while (result.RankRuns.Count < sim.ComfortRank)
				{
					result.RankRuns.Add(run + 1);
				}
			}
			result.TownHall = sim.State.TownHall;
			result.Kinds = Kinds(sim);
			result.Pieces = sim.State.Placed.Count;
			result.Comfort = sim.Comfort;
			result.Room = sim.HouseTier.Name;
			return result;
		}

		// 모자란 재료를 집 제작대에서 바로 가공 (판자, 실, 천). 두 단계까지
		private static void BenchCraftFor(HomesteadSim sim, CostDef cost, Func<CostDef, bool> keeps, int depth = 0)
		{
			if (depth > 2)
			{
				return;
			}
			foreach (KeyValuePair<string, int> pair in cost.Goods)
			{
				RecipeDef recipe = sim.Catalog.Recipes.Find(candidate => candidate.Output == pair.Key && candidate.Building == HomesteadSim.BENCH);
				if (recipe == null)
				{
					continue;
				}
				for (int guard = 0; guard < 20 && sim.Count(pair.Key) < pair.Value; guard++)
				{
					CostDef inputs = new CostDef { Goods = recipe.Inputs };
					BenchCraftFor(sim, inputs, keeps, depth + 1);
					if (keeps(inputs) == false || sim.StartCraft(recipe.Id).Ok == false)
					{
						break;
					}
				}
			}
		}

		private static int TotalOwned(HomesteadSim sim)
		{
			int total = 0;
			foreach (int count in sim.State.OwnedFurniture.Values)
			{
				total += count;
			}
			return total;
		}

		[Test]
		public void Comfort_ranks_arrive_over_runs()
		{
			foreach (bool thrifty in new[] { true, false })
			{
				for (int seed = 11; seed < 14; seed++)
				{
					HousingRun run = PlayHousing(seed, thrifty);
					TestContext.WriteLine($"{(thrifty ? "아낌" : "바로")} seed {seed}: 단계별 판 [{string.Join(", ", run.RankRuns)}], 넓히기 판 [{string.Join(", ", run.TierRuns)}], {RUNS}판 뒤 {run.Room} 가구 {run.Pieces} 종류 {run.Kinds} 아늑함 {run.Comfort}, 타운홀 판 [{string.Join(", ", run.HallRuns)}]");
				}
			}
			// 집에 바로 쓰는 사람은 첫 단계가 처음 몇 판 안에, 끝 단계도 닿음
			HousingRun eager = PlayHousing(11, false);
			Assert.That(eager.RankRuns[0], Is.LessThanOrEqualTo(3), "첫 단계");
			Assert.That(eager.RankRuns.Count, Is.EqualTo(HomesteadTestKit.LoadCatalog().Tuning.ComfortSteps.Count - 1), "끝 단계");
		}
	}
}
