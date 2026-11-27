using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 옷장 15종이 몇 판째에 모이는지 잼. 마을 한 바퀴는 HomesteadPlayTest 의 봇을 그대로 빌림
	// 그 위에 낚시, 유물 손질, 선물, 시설 이용, 이정표 받기, 옷 짓기를 더함
	public class HomesteadCostumeBotTest
	{
		private const int RUNS = 120;
		private const int CASTS_PER_RUN = 6;
		private const int SEEDS = 5;
		// 2026-11-27 실측 마지막 옷 37판에 여유. 꿀 물들임이 꿀 2개일 때 73판까지 늘어져 잡힘
		private const int LAST_COSTUME_RUN = 45;
		// 물 날개 마지막 이정표는 16종 전부. 안개메기가 달빛 낚싯대 (고운 양털, 목장 정) 를 요구하는데 이 봇은 목장을 안 돌림
		private const string NEEDS_RANCH = "hat_fisher";

		private static readonly MethodInfo PlayDungeonMethod = PlayTestMethod("PlayDungeon");
		private static readonly MethodInfo PlayTownMethod = PlayTestMethod("PlayTown");
		private static readonly MethodInfo KeepsMethod = typeof(HomesteadPlayTest).GetMethod("KeepsTownHall",
			BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(HomesteadSim), typeof(CostDef) }, null);

		// 시험끼리 봇을 나눠 쓰려고 반사로 부름. 다른 피처 세션이 같은 파일을 고치는 중이라 그 파일은 안 건드림
		private static MethodInfo PlayTestMethod(string name) =>
			typeof(HomesteadPlayTest).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);

		private static bool Keeps(HomesteadSim sim, CostDef cost) => (bool)KeepsMethod.Invoke(null, new object[] { sim, cost });

		// 옷장 id 마다 처음 가진 판. 끝까지 못 얻으면 빠짐
		internal static Dictionary<string, int> CostumeRuns(int seed, out int tailorRun)
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse, seed);
			purse.Nyang = 300;
			Random random = new(seed * 31 + 5);
			Dictionary<string, int> got = new();
			tailorRun = -1;
			for (int run = 0; run < RUNS && got.Count < sim.Catalog.Costumes.Count; run++)
			{
				PlayDungeonMethod.Invoke(null, new object[] { sim, purse, random, run });
				PlayTownMethod.Invoke(null, new object[] { sim, random });
				while (sim.CanUpgradeTownHall().Ok)
				{
					sim.UpgradeTownHall();
				}
				PlantMissing(sim);
				Fish(sim, random);
				Restore(sim);
				Sew(sim);
				foreach (GoodsDef goods in sim.Catalog.Goods)
				{
					sim.Donate(goods.Id);
				}
				Befriend(sim);
				foreach (WingDef wing in sim.Catalog.Wings)
				{
					while (sim.ClaimMilestone(wing.Id, out RewardDef _).Ok)
					{
					}
				}
				if (tailorRun < 0 && sim.IsBuilt(HomesteadSim.TAILOR))
				{
					tailorRun = run + 1;
				}
				foreach (CostumeDef costume in sim.Catalog.Costumes)
				{
					if (sim.CanMakeCostume(costume.Id).Ok && Keeps(sim, costume.Cost))
					{
						sim.MakeCostume(costume.Id);
					}
				}
				foreach (CostumeDef costume in sim.Catalog.Costumes)
				{
					if (sim.OwnsCostume(costume.Id) && got.ContainsKey(costume.Id) == false)
					{
						got[costume.Id] = run + 1;
					}
				}
			}
			string wings = string.Join(", ", sim.Catalog.Wings.Select(wing => $"{wing.Id} {sim.DonatedCount(wing)}/{wing.Milestones[wing.Milestones.Count - 1].Count}"));
			TestContext.WriteLine($"씨앗 {seed}: 타운홀 {sim.State.TownHall}, 의상실 {sim.BuildingLevel(HomesteadSim.TAILOR)}, 낚싯대 {sim.ToolTier(ToolKind.Rod)}, 기증 {wings}, 천 {sim.Count("cloth")}, 유리 {sim.Count("glass")}");
			return got;
		}

		// 아직 안 기른 작물을 뒤쪽 밭에 하나씩. 다음 바퀴에 마을 봇이 거둠
		private static void PlantMissing(HomesteadSim sim)
		{
			int plot = sim.State.Plots.Count - 1;
			foreach (CropDef crop in sim.Catalog.Crops)
			{
				if (plot < 0 || sim.IsDonated(crop.Harvest) || sim.Count(crop.Harvest) > 0 || sim.BuySeed(crop.Id, 1).Ok == false)
				{
					continue;
				}
				sim.Uproot(plot);
				sim.Till(plot);
				sim.Plant(plot, crop.Id);
				sim.Water(plot);
				plot--;
			}
		}

		// 의상실 다음 단계와 아직 없는 옷에 모자란 재료부터 만들고, 의상실을 올림
		private static void Sew(HomesteadSim sim)
		{
			Dictionary<string, int> wanted = new();
			void Want(CostDef cost)
			{
				foreach (KeyValuePair<string, int> pair in cost.Goods)
				{
					wanted[pair.Key] = (wanted.TryGetValue(pair.Key, out int had) ? had : 0) + pair.Value;
				}
			}
			if (sim.TryGetNextLevel(HomesteadSim.TAILOR, out BuildingLevelDef level))
			{
				Want(level.Cost);
			}
			foreach (CostumeDef costume in sim.Catalog.Costumes)
			{
				if (sim.OwnsCostume(costume.Id) == false)
				{
					Want(costume.Cost);
				}
			}
			for (int round = 0; round < 6; round++)
			{
				foreach (RecipeDef recipe in sim.Catalog.Recipes)
				{
					if (wanted.TryGetValue(recipe.Output, out int need) && sim.Count(recipe.Output) < need && Keeps(sim, new CostDef { Goods = recipe.Inputs }))
					{
						sim.StartCraft(recipe.Id);
					}
				}
				sim.AdvanceMinutes(60);
				sim.CollectCrafts();
			}
			if (level != null && sim.IsBuilt(HomesteadSim.TAILOR) && Keeps(sim, level.Cost))
			{
				sim.StartBuild(HomesteadSim.TAILOR);
			}
		}

		// 낚싯대에 맞는 것만 문다고 보고 다 낚음 (손이 좋은 사람)
		private static void Fish(HomesteadSim sim, Random random)
		{
			List<string> waters = sim.Catalog.Fishes.SelectMany(fish => fish.Waters).Distinct().ToList();
			for (int i = 0; i < CASTS_PER_RUN; i++)
			{
				string water = waters[random.Next(waters.Count)];
				FishDef fish = sim.PickFish(water, random.Next(24), random.Next(4));
				sim.Land(fish, true, out bool _);
			}
		}

		// 때를 틀리지 않고 닦음
		private static void Restore(HomesteadSim sim)
		{
			while (sim.BeginRestore().Ok)
			{
				bool finished = false;
				while (finished == false)
				{
					RelicDef relic = sim.Catalog.Get<RelicDef>(sim.State.Restoring.Relic);
					sim.Clean(relic.Grime[sim.State.Restoring.Cleaned], out bool _, out finished);
				}
			}
		}

		// 부탁을 들어주고, 하루 한 번 선물 (가장 좋아하는 것, 없으면 좋아하는 갈래), 시설 이용
		private static void Befriend(HomesteadSim sim)
		{
			foreach (ResidentDef resident in sim.Catalog.Residents)
			{
				if (sim.HasArrived(resident.Id) == false)
				{
					RecruitDef favor = resident.Recruits.FirstOrDefault(recruit => recruit.Kind == RecruitKind.Favor);
					if (favor != null && Keeps(sim, favor.Cost))
					{
						sim.PayFavor(resident.Id, favor);
					}
				}
				if (sim.HasArrived(resident.Id) == false)
				{
					continue;
				}
				string gift = resident.Favorites.FirstOrDefault(id => sim.Count(id) > 0)
					?? sim.Catalog.Goods.FirstOrDefault(goods => resident.Likes.Contains(goods.Kind) && sim.Count(goods.Id) > 0
						&& Keeps(sim, new CostDef { Goods = new Dictionary<string, int> { [goods.Id] = 1 } }))?.Id;
				if (gift != null && sim.CanGiftToday(resident.Id))
				{
					sim.Gift(resident.Id, gift);
				}
			}
			foreach (BuildingDef building in sim.Catalog.Buildings)
			{
				sim.UseFacility(building.Id);
			}
		}

		[Test]
		public void Every_costume_is_reachable_within_the_demo_length()
		{
			List<CostumeDef> costumes = HomesteadTestKit.LoadCatalog().Costumes;
			Dictionary<string, List<int>> all = costumes.ToDictionary(costume => costume.Id, _ => new List<int>());
			List<int> tailors = new();
			for (int seed = 0; seed < SEEDS; seed++)
			{
				Dictionary<string, int> got = CostumeRuns(seed, out int tailorRun);
				foreach (KeyValuePair<string, int> pair in got)
				{
					all[pair.Key].Add(pair.Value);
				}
				tailors.Add(tailorRun);
			}
			TestContext.WriteLine($"의상실 지은 판: {string.Join(", ", tailors)}");
			foreach (CostumeDef costume in costumes.OrderBy(costume => all[costume.Id].DefaultIfEmpty(999).Max()))
			{
				List<int> runs = all[costume.Id];
				string text = runs.Count == 0 ? "못 얻음" : $"{runs.Min()}~{runs.Max()}판 ({runs.Count}/{SEEDS})";
				TestContext.WriteLine($"{costume.Name,-8} {costume.Source,-8} {text}");
			}
			foreach (CostumeDef costume in costumes)
			{
				if (costume.Id == NEEDS_RANCH)
				{
					continue;
				}
				Assert.That(all[costume.Id].Count, Is.EqualTo(SEEDS), $"{costume.Name} 을 {RUNS}판 안에 못 얻는 씨앗이 있음");
				Assert.That(all[costume.Id].Max(), Is.LessThanOrEqualTo(LAST_COSTUME_RUN), $"{costume.Name} 이 너무 늦음");
			}		}
	}
}
