using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 모으는 사람 흉내 봇. 관에 걸 물건마다 처음 얻은 판을 잼
	// 진행 (승급, 도구, 건물) 은 HomesteadPlayTest 의 봇과 같은 순서. 거기에 낚시, 손질, 목장, 못 본 작물 먼저 심기를 더함
	public class HomesteadCollectBotTest
	{
		private const float FRAME = 1f / 60f;
		private const float DUEL_LIMIT = 90f;
		private const float REACTION = 0.3f;
		private const int RUNS = 80;
		private const int CASTS_PER_RUN = 8;
		// 설계: 관마다 첫 보상은 2~3점. 박물관이 서고 몇 판 안에 닿아야 함
		private const int FIRST_MILESTONE_RUN_LIMIT = 20;
		private static readonly int[] SEEDS = { 1, 2, 3, 4, 5 };
		private static readonly string[] SPOTS = { "tree", "tree", "tree", "tree", "tree", "tree", "rock", "rock", "rock", "rock", "bush", "bush", "bush", "bush", "herb", "herb", "herb", "mushroompatch", "mushroompatch", "hardtree", "hardtree" };
		private static readonly string[] WATERS = { "pond", "stream" };

		internal sealed class Record
		{
			public readonly Dictionary<string, int> FirstRun = new();
			public readonly Dictionary<string, int> FirstMilestoneRun = new();
			public readonly List<int> TownHallRuns = new();
			public string End = string.Empty;
		}

		internal static Record Play(int seed, int runs)
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse, seed);
			purse.Nyang = 300;
			Random random = new(seed * 31 + 7);
			Record record = new();
			for (int run = 0; run < runs; run++)
			{
				PlayDungeon(sim, purse, random, run);
				PlayTown(sim, random, run);
				while (sim.CanUpgradeTownHall().Ok)
				{
					sim.UpgradeTownHall();
					record.TownHallRuns.Add(run + 1);
				}
				foreach (GoodsDef goods in sim.Catalog.Goods)
				{
					if (sim.IsKnown(goods.Id) && record.FirstRun.ContainsKey(goods.Id) == false)
					{
						record.FirstRun[goods.Id] = run + 1;
					}
				}
				foreach (WingDef wing in sim.Catalog.Wings)
				{
					if (record.FirstMilestoneRun.ContainsKey(wing.Id) == false && sim.ClaimedMilestones(wing) > 0)
					{
						record.FirstMilestoneRun[wing.Id] = run + 1;
					}
				}
			}
			record.End = $"타운홀 {sim.State.TownHall}, 곡괭이 {sim.ToolTier(ToolKind.Pickaxe)}, 낚싯대 {sim.ToolTier(ToolKind.Rod)}, 갱도 깊이 {sim.State.MineDeepest}, 목장 {sim.BuildingLevel("ranch")}, 기증 {sim.State.Donated.Count}";
			return record;
		}

		[Test]
		public void Collector_reaches_the_first_milestone_of_every_wing_and_reports_first_runs()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			List<Record> records = SEEDS.Select(seed => Play(seed, RUNS)).ToList();
			TestContext.WriteLine($"봇 {SEEDS.Length}명, {RUNS}판. 타운홀 승급 판 (가운데): {string.Join(", ", Median(records.Select(record => record.TownHallRuns)))}");
			records.ForEach(record => TestContext.WriteLine($"  끝: {record.End}"));
			foreach (WingDef wing in catalog.Wings)
			{
				TestContext.WriteLine($"[{wing.Name}] 첫 이정표 판 {string.Join(", ", records.Select(record => record.FirstMilestoneRun.TryGetValue(wing.Id, out int run) ? run.ToString() : "-"))}");
				foreach (GoodsDef goods in catalog.Goods.Where(candidate => wing.Kinds.Contains(candidate.Kind)))
				{
					List<int> firsts = records.Select(record => record.FirstRun.TryGetValue(goods.Id, out int run) ? run : -1).ToList();
					string cells = string.Join(" ", firsts.Select(run => run < 0 ? "-" : run.ToString()));
					TestContext.WriteLine($"  {goods.Name,-10} {cells}");
				}
			}
			foreach (WingDef wing in catalog.Wings)
			{
				foreach (Record record in records)
				{
					Assert.That(record.FirstMilestoneRun.ContainsKey(wing.Id), Is.True, $"{wing.Name} 첫 이정표");
					Assert.That(record.FirstMilestoneRun[wing.Id], Is.LessThanOrEqualTo(FIRST_MILESTONE_RUN_LIMIT), $"{wing.Name} 첫 이정표");
				}
			}
		}

		// ---- 봇 한 바퀴

		// 확정 f05-1, f05-2. 데모의 낚싯대로 안개메기 시각에 개울에서 던지면 낚임. 못 본 물고기 가중이 꼬리를 줄임
		[Test]
		public void Mist_catfish_is_caught_with_the_demo_rod_at_its_hour()
		{
			List<int> biased = new();
			List<int> plain = new();
			foreach (int seed in Enumerable.Range(1, MIST_SEEDS))
			{
				biased.Add(CastsUntilCaught("mistking", seed, null));
				plain.Add(CastsUntilCaught("mistking", seed, 1f));
			}
			TestContext.WriteLine($"안개메기 처음 낚기까지 던진 수 (가중): 가운데 {Middle(biased)}, 가장 긺 {biased.Max()}  [{string.Join(", ", biased)}]");
			TestContext.WriteLine($"안개메기 처음 낚기까지 던진 수 (가중 없음): 가운데 {Middle(plain)}, 가장 긺 {plain.Max()}  [{string.Join(", ", plain)}]");
			Assert.That(biased, Has.All.LessThanOrEqualTo(MIST_CAST_LIMIT), "시각을 맞추면 낚임");
			Assert.That(biased.Sum(), Is.LessThan(plain.Sum()), "못 본 물고기 가중이 도움");
		}

		private const int MIST_SEEDS = 30;
		private const int MIST_CAST_LIMIT = 250;

		private static int Middle(List<int> values) => values.OrderBy(value => value).ElementAt(values.Count / 2);

		private static int CastsUntilCaught(string fishId, int seed, float? unownedBias)
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _, seed);
			if (unownedBias.HasValue)
			{
				sim.Catalog.Tuning.UnownedFishBias = unownedBias.Value;
			}
			FishDef target = sim.Catalog.Get<FishDef>(fishId);
			sim.State.Tools[ToolKind.Rod] = target.RodTier;
			Random random = new(seed);
			int span = (target.HourTo - target.HourFrom + 24) % 24;
			for (int cast = 1; cast <= MIST_CAST_LIMIT * 4; cast++)
			{
				int hour = (target.HourFrom + random.Next(span)) % 24;
				FishDef fish = sim.PickFish(target.Waters[0], hour, 0);
				if (PlayDuel(sim.StartDuel(fish), random) == FishingPhase.Caught)
				{
					sim.Land(fish, false, out bool _);
					if (fish.Id == fishId)
					{
						return cast;
					}
				}
			}
			return int.MaxValue;
		}

		private static void PlayDungeon(HomesteadSim sim, HomesteadTestKit.FakePurse purse, Random random, int run)
		{
			sim.AdvanceMinutes(600);
			sim.NotifyExpedition();
			purse.Nyang += 350;
			foreach (int item in new[] { 90, 91, 92, 93 })
			{
				purse.Bag[item] = purse.CountItem(item) + random.Next(2, 6);
			}
			if (run > 5)
			{
				purse.Bag[18212] = purse.CountItem(18212) + random.Next(0, 3);
				purse.Bag[18213] = purse.CountItem(18213) + random.Next(0, 3);
			}
			if (run == 4 || run == 9)
			{
				sim.NotifyEvent("boss");
			}
		}

		private static void PlayTown(HomesteadSim sim, Random random, int run)
		{
			foreach (BuildingDef building in sim.Catalog.Buildings)
			{
				sim.Unveil(building.Id);
			}
			for (int i = 0; i < SPOTS.Length; i++)
			{
				sim.Gather("spot" + i, SPOTS[i]);
			}
			Farm(sim);
			if (sim.IsBuilt("dock"))
			{
				Fish(sim, random);
			}
			if (sim.IsBuilt("quarry"))
			{
				Dig(sim, random);
			}
			Ranch(sim);
			Restore(sim);
			sim.CollectAllJobs();
			int doll = 0;
			foreach (JobDef job in sim.Catalog.Jobs)
			{
				if (sim.IsBuilt(job.Building) && sim.IsStaffed(job.Id) == false)
				{
					sim.Assign(job.Id, doll);
				}
				doll++;
			}
			sim.RefreshRequests();
			foreach (RequestState request in sim.State.Requests.ToArray())
			{
				if (KeepsTownHall(sim, request.Goods, request.Amount))
				{
					sim.Deliver(request.Uid);
				}
			}
			Produce(sim);
			foreach (ToolDef tool in sim.Catalog.Tools)
			{
				if (sim.TryGetNextToolTier(tool.Kind, out ToolTierDef nextTool) && KeepsTownHall(sim, nextTool.Cost))
				{
					sim.UpgradeTool(tool.Kind);
				}
			}
			foreach (BuildingDef building in sim.Catalog.Buildings)
			{
				if (sim.TryGetNextLevel(building.Id, out BuildingLevelDef level) && (sim.IsBuilt(building.Id) == false || KeepsTownHall(sim, level.Cost)))
				{
					sim.StartBuild(building.Id);
				}
			}
			foreach (GoodsDef goods in sim.Catalog.Goods)
			{
				sim.Donate(goods.Id);
			}
			foreach (WingDef wing in sim.Catalog.Wings)
			{
				while (sim.ClaimMilestone(wing.Id, out RewardDef _).Ok)
				{
				}
			}
		}

		// 못 본 작물부터. 씨앗을 못 사면 열린 것 중 아무거나
		private static void Farm(HomesteadSim sim)
		{
			for (int i = 0; i < sim.State.Plots.Count; i++)
			{
				sim.Till(i);
				sim.Harvest(i, 0);
				PlotState plot = sim.State.Plots[i];
				// 다시 열리는 작물은 칸을 계속 차지함. 거둔 적이 있고 못 본 작물이 열렸으면 걷어 냄
				if (plot.Crop.Length > 0 && plot.Harvests > 0 && HasUnknownCrop(sim))
				{
					sim.Uproot(i);
				}
				if (plot.Crop.Length > 0)
				{
					sim.Water(i);
					continue;
				}
				foreach (CropDef crop in sim.Catalog.Crops.OrderBy(candidate => sim.IsKnown(candidate.Harvest) ? 1 : 0))
				{
					if (sim.Count(crop.Seed) > 0 || sim.BuySeed(crop.Id, 1).Ok)
					{
						sim.Plant(i, crop.Id);
						break;
					}
				}
				sim.Water(i);
			}
		}

		private static bool HasUnknownCrop(HomesteadSim sim) => sim.Catalog.Crops.Any(crop => crop.TownHall <= sim.State.TownHall && sim.IsKnown(crop.Harvest) == false);

		// 시각을 골고루. 물은 번갈아
		private static void Fish(HomesteadSim sim, Random random)
		{
			for (int cast = 0; cast < CASTS_PER_RUN; cast++)
			{
				FishDef fish = sim.PickFish(WATERS[cast % WATERS.Length], random.Next(24), 0);
				if (PlayDuel(sim.StartDuel(fish), random) == FishingPhase.Caught)
				{
					sim.Land(fish, false, out bool _);
				}
			}
		}

		private static FishingPhase PlayDuel(FishingDuel duel, Random random)
		{
			float seconds = 0f;
			Queue<bool> seen = new();
			int delayFrames = Math.Max(1, (int)(REACTION / FRAME));
			bool reeling = false;
			while (duel.IsOver == false && seconds < DUEL_LIMIT)
			{
				if (duel.Phase == FishingPhase.Bite)
				{
					duel.Press();
				}
				if (duel.Phase == FishingPhase.Duel)
				{
					bool wantReel = (duel.IsSurging || duel.IsSurgeNear) == false && duel.Tension < duel.TensionCeiling;
					seen.Enqueue(wantReel);
					if (seen.Count > delayFrames)
					{
						reeling = seen.Dequeue();
					}
					seconds += FRAME;
				}
				duel.Step(FRAME, reeling);
			}
			return duel.Phase;
		}

		private static void Ranch(HomesteadSim sim)
		{
			foreach (AnimalDef animal in sim.Catalog.Animals)
			{
				sim.BuyAnimal(animal.Id);
			}
			foreach (AnimalState animal in sim.State.Animals.ToArray())
			{
				sim.Care(animal.Uid, out bool _);
			}
		}

		// 손질은 늘 맞는 도구로
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

		// 지금 곡괭이로 못 본 것을 캘 수 있는 가장 깊은 승강기. 없으면 아무 데나
		private static void Dig(HomesteadSim sim, Random random)
		{
			List<int> stops = sim.LiftStops();
			List<int> useful = stops.Where(stop => HasUnknownOre(sim, sim.BandAt(stop))).ToList();
			MineRun run = sim.EnterMine(useful.Count > 0 ? useful[useful.Count - 1] : stops[random.Next(stops.Count)]);
			while (run.Lantern > 0)
			{
				int target = -1;
				for (int i = 0; i < run.Nodes.Count; i++)
				{
					if (run.Nodes[i].IsBroken == false && run.Nodes[i].Def.Tier <= sim.ToolTier(ToolKind.Pickaxe))
					{
						target = i;
						break;
					}
				}
				if (target < 0 || run.PassageOpen)
				{
					if (run.Descend().Ok == false)
					{
						return;
					}
					continue;
				}
				run.Strike(target, (float)random.NextDouble());
			}
		}

		private static bool HasUnknownOre(HomesteadSim sim, MineBandDef band)
		{
			foreach (MineWeightDef weight in band.Weights)
			{
				MineNodeDef node = sim.Catalog.Get<MineNodeDef>(weight.Node);
				if (node.Tier <= sim.ToolTier(ToolKind.Pickaxe) && node.Drops.Any(drop => sim.IsKnown(drop.Goods) == false))
				{
					return true;
				}
			}
			return false;
		}

		// ---- 진행 (HomesteadPlayTest 와 같은 규칙)

		private const int SPARE_STOCK = 20;

		private static bool KeepsTownHall(HomesteadSim sim, string goodsId, int amount)
		{
			if (sim.TryGetNextTownHall(out TownHallDef next) == false || next.Cost.Goods.TryGetValue(goodsId, out int need) == false)
			{
				return true;
			}
			return sim.Count(goodsId) - amount >= need;
		}

		private static bool KeepsTownHall(HomesteadSim sim, CostDef cost)
		{
			foreach (KeyValuePair<string, int> pair in cost.Goods)
			{
				if (KeepsTownHall(sim, pair.Key, pair.Value) == false)
				{
					return false;
				}
			}
			if (sim.TryGetNextTownHall(out TownHallDef next) == false)
			{
				return true;
			}
			foreach (KeyValuePair<string, int> pair in next.Cost.Goods)
			{
				if (sim.Count(pair.Key) < pair.Value)
				{
					return true;
				}
			}
			return sim.Purse.Nyang - cost.Nyang >= next.Cost.Nyang;
		}

		private static void Produce(HomesteadSim sim)
		{
			sim.CollectCrafts();
			Dictionary<string, int> wanted = new();
			if (sim.TryGetNextTownHall(out TownHallDef next))
			{
				foreach (KeyValuePair<string, int> pair in next.Cost.Goods)
				{
					wanted[pair.Key] = pair.Value + SPARE_STOCK;
				}
			}
			for (int round = 0; round < 6; round++)
			{
				foreach (RecipeDef recipe in sim.Catalog.Recipes)
				{
					if (wanted.TryGetValue(recipe.Output, out int need) && sim.Count(recipe.Output) < need)
					{
						sim.StartCraft(recipe.Id);
					}
				}
				foreach (RecipeDef recipe in sim.Catalog.Recipes)
				{
					if (sim.Count(recipe.Output) < 8 && KeepsTownHall(sim, new CostDef { Goods = recipe.Inputs }))
					{
						sim.StartCraft(recipe.Id);
					}
				}
				sim.AdvanceMinutes(60);
				sim.CollectCrafts();
			}
		}

		private static List<int> Median(IEnumerable<List<int>> lists)
		{
			List<List<int>> all = lists.ToList();
			int length = all.Max(list => list.Count);
			List<int> medians = new();
			for (int i = 0; i < length; i++)
			{
				List<int> column = all.Where(list => list.Count > i).Select(list => list[i]).OrderBy(run => run).ToList();
				medians.Add(column[column.Count / 2]);
			}
			return medians;
		}
	}
}
