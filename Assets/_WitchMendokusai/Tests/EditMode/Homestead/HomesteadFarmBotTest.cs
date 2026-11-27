using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 농사 봇. 던전 한 판마다 마을에 들러 거두고, 비면 심고, 물을 주는 사람을 흉내 냄.
	// 수확량은 자료의 평균으로 셈 (무작위 대신 기대값). 성장, 물, 재수확은 실제 규칙을 거침
	public class HomesteadFarmBotTest
	{
		// HomesteadPlayTest 의 던전 한 판 길이와 같음
		private const int RUN_MINUTES = 600;
		private const int VISITS = 20;
		private const int NO_SEASON = -1;

		// 들를 때마다 얻는 순이익 (판매가 x 평균 수확 - 씨앗값), 한 칸 기준
		internal static float NetPerVisit(HomesteadSim sim, CropDef crop, int runMinutes, bool water)
		{
			sim.State.TownHall = Math.Max(sim.State.TownHall, crop.TownHall);
			if (sim.State.Plots.Count == 0)
			{
				sim.State.Plots.Add(new PlotState());
			}
			sim.Till(0);
			int price = sim.Catalog.Get<GoodsDef>(crop.Harvest).Price;
			float average = (crop.YieldMin + crop.YieldMax) / 2f;
			float net = 0f;
			for (int visit = 0; visit < VISITS; visit++)
			{
				// 재수확 작물은 한 판 동안 열매가 쌓임. 거둘 때 쌓인 수만큼 받음
				int loads = sim.RipeLoads(sim.State.Plots[0]);
				if (sim.Harvest(0, NO_SEASON).Ok)
				{
					net += average * price * loads;
				}
				if (sim.State.Plots[0].Crop.Length == 0)
				{
					Assert.That(sim.BuySeed(crop.Id, 1).Ok, crop.Id);
					Assert.That(sim.Plant(0, crop.Id).Ok, crop.Id);
					net -= crop.SeedPrice;
				}
				if (water)
				{
					sim.Water(0);
				}
				sim.AdvanceMinutes(runMinutes);
			}
			return net / VISITS;
		}

		private static Dictionary<string, float> Measure(int runMinutes, bool water)
		{
			Dictionary<string, float> result = new();
			foreach (CropDef crop in HomesteadTestKit.LoadCatalog().Crops)
			{
				HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse);
				purse.Nyang = 100000;
				result[crop.Id] = NetPerVisit(sim, crop, runMinutes, water);
			}
			return result;
		}

		[Test]
		public void Report_net_per_visit_for_every_crop()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			int[] cadences = { 300, RUN_MINUTES, 1200 };
			List<Dictionary<string, float>> wet = new();
			foreach (int cadence in cadences)
			{
				wet.Add(Measure(cadence, true));
			}
			Dictionary<string, float> dry = Measure(RUN_MINUTES, false);
			TestContext.WriteLine("crop | townhall | wet300 | wet600 | wet1200 | dry600");
			foreach (CropDef crop in catalog.Crops)
			{
				TestContext.WriteLine($"{crop.Id} | {crop.TownHall} | {wet[0][crop.Id]:0.0} | {wet[1][crop.Id]:0.0} | {wet[2][crop.Id]:0.0} | {dry[crop.Id]:0.0}");
			}
			Assert.That(wet[1].Count, Is.EqualTo(catalog.Crops.Count));
		}

		// 타운홀을 올려 연 작물이 앞 단계에서 이미 가진 작물보다 못하면 열 이유가 없음.
		// 2026-11-27 재수확 쌓기 뒤 여유: 타운홀 2 최고 고추 66.6 대 감자 43.0, 타운홀 3 최저 달콩 72.5 대 고추 66.6.
		// 달콩, 눈냉이, 호박은 WetRefreshMinutes 가 한 판 뒤 남는 물기 120분 (720 빼기 600) 보다 커서 매번 물이 들 때만 이 값
		// 작물 수치를 바꾸다 여기서 깨지면 곡선이 꺾인 것. 시험이 아니라 수치를 다시 봄
		[Test]
		public void A_newly_unlocked_crop_is_never_worse_than_the_best_older_one()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			Dictionary<string, float> net = Measure(RUN_MINUTES, true);
			foreach (CropDef crop in catalog.Crops)
			{
				float bestOlder = 0f;
				string bestOlderId = "-";
				foreach (CropDef older in catalog.Crops)
				{
					if (older.TownHall < crop.TownHall && net[older.Id] > bestOlder)
					{
						bestOlder = net[older.Id];
						bestOlderId = older.Id;
					}
				}
				Assert.That(net[crop.Id], Is.GreaterThanOrEqualTo(bestOlder), $"{crop.Id} {net[crop.Id]:0.0} < {bestOlderId} {bestOlder:0.0}");
			}
		}

		// 던전 한 판 (600분) 뒤에 들르면 물기가 120분 남음. 그때 다시 줄 수 있어야 물이 한 판 내내 듦
		[Test]
		public void Water_can_be_topped_up_when_little_is_left()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.Till(0);
			Assert.That(sim.Water(0).Ok);
			Assert.That(sim.Water(0).Failure, Is.EqualTo(HomesteadFailure.AlreadyDone), "방금 준 칸은 거절");
			sim.AdvanceMinutes(RUN_MINUTES);
			Assert.That(sim.IsWet(sim.State.Plots[0]), "아직 젖어 있음");
			Assert.That(sim.CanWater(sim.State.Plots[0]));
			Assert.That(sim.Water(0).Ok);
			Assert.That(sim.State.Plots[0].WetUntil, Is.EqualTo(sim.State.Minutes + sim.Catalog.Tuning.WetMinutes));
		}

		[Test]
		public void Watering_every_visit_ripens_pumpkin_every_run()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse);
			purse.Nyang = 100000;
			CropDef pumpkin = sim.Catalog.Get<CropDef>("pumpkin");
			float net = NetPerVisit(sim, pumpkin, RUN_MINUTES, true);
			float everyRun = ((VISITS - 1) * sim.Catalog.Get<GoodsDef>("pumpkin").Price - VISITS * pumpkin.SeedPrice) / (float)VISITS;
			Assert.That(net, Is.EqualTo(everyRun).Within(0.01f));
		}

		// 벌 없음. 물을 안 줘도 거둘 수 있고 손해를 보지 않음
		[Test]
		public void Every_crop_pays_back_its_seed_without_water()
		{
			Dictionary<string, float> dry = Measure(RUN_MINUTES, false);
			foreach (KeyValuePair<string, float> pair in dry)
			{
				Assert.That(pair.Value, Is.GreaterThan(0f), pair.Key);
			}
		}

		// ---- 확정 반영 2026-11-27 (f01-1, f01-2)

		// 재수확 작물은 한 판 (600분) 동안 두 번 익을 수 있음. 전에는 한 번만 거둬 나머지를 버렸음.
		// 기준: 처음 익은 뒤로 들를 때마다 받는 열매가 평균 1.8 이상
		[Test]
		public void Regrow_crop_banks_fruit_over_a_dungeon_run()
		{
			foreach (string cropId in new[] { "tomato", "chili", "strawberry" })
			{
				foreach (bool water in new[] { true, false })
				{
					HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
					sim.Give(sim.Catalog.Get<CropDef>(cropId).Seed, 1);
					sim.Till(0);
					Assert.That(sim.Plant(0, cropId).Ok);
					int loads = 0;
					int harvests = 0;
					for (int visit = 0; visit < VISITS; visit++)
					{
						int ripe = sim.RipeLoads(sim.State.Plots[0]);
						if (sim.Harvest(0, NO_SEASON).Ok && sim.State.Plots[0].Harvests > 1)
						{
							loads += ripe;
							harvests++;
						}
						if (water)
						{
							sim.Water(0);
						}
						sim.AdvanceMinutes(RUN_MINUTES);
					}
					float average = loads / (float)Math.Max(1, harvests);
					Assert.That(average, Is.GreaterThanOrEqualTo(1.8f), $"{cropId} 물 {water}: 거둘 때 열매 {average:0.00}");
				}
			}
		}

		[Test]
		public void Banked_fruit_stops_at_the_cap_and_comes_out_in_one_harvest()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.Give("seed_tomato", 1);
			sim.Till(0);
			Assert.That(sim.Plant(0, "tomato").Ok);
			PlotState plot = sim.State.Plots[0];
			sim.AdvanceMinutes(100000);
			Assert.That(sim.PhaseOf(plot), Is.EqualTo(PlotPhase.Ripe));
			Assert.That(sim.RipeLoads(plot), Is.EqualTo(sim.Catalog.Tuning.RegrowStockCap), "오래 둬도 상한까지만");
			Assert.That(sim.MinutesLeft(plot), Is.EqualTo(0));
			Assert.That(sim.Ripeness(plot), Is.EqualTo(1f));
			Assert.That(sim.Harvest(0, NO_SEASON).Ok);
			Assert.That(sim.RipeLoads(plot), Is.EqualTo(0));
			Assert.That(sim.PhaseOf(plot), Is.EqualTo(PlotPhase.Growing), "다시 자람");
			Assert.That(plot.Harvests, Is.EqualTo(1), "손 동작은 한 번");
			int store = sim.State.Store.TryGetValue("tomato", out int amount) ? amount : 0;
			CropDef tomato = sim.Catalog.Get<CropDef>("tomato");
			Assert.That(store, Is.InRange(tomato.YieldMin * 2, tomato.YieldMax * 2));
		}

		// 단발 작물은 쌓이지 않음. 다 익으면 기다릴 뿐
		[Test]
		public void Single_harvest_crop_does_not_bank()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.Give("seed_turnip", 1);
			sim.Till(0);
			sim.Plant(0, "turnip");
			sim.AdvanceMinutes(100000);
			Assert.That(sim.RipeLoads(sim.State.Plots[0]), Is.EqualTo(1));
		}

		// 저장 필드 Stock 이 없던 옛 세이브. 다 익은 재수확 작물은 열매 하나로 읽히고 거둘 수 있음
		[Test]
		public void Old_save_without_stock_reads_as_one_ripe_load()
		{
			HomesteadSim maker = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			maker.State.Plots.Clear();
			maker.State.Plots.Add(new PlotState { Crop = "tomato", Tilled = true, Growth = 240f, Harvests = 2 });
			JObject json = JObject.Parse(JsonConvert.SerializeObject(maker.State));
			foreach (JObject plot in json["Plots"])
			{
				plot.Remove("Stock");
			}
			StringAssert.DoesNotContain("Stock", json["Plots"].ToString());
			HomesteadState old = json.ToObject<HomesteadState>();
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _, state: old);
			PlotState loaded = sim.State.Plots[0];
			Assert.That(loaded.Stock, Is.EqualTo(0));
			Assert.That(sim.PhaseOf(loaded), Is.EqualTo(PlotPhase.Ripe));
			Assert.That(sim.RipeLoads(loaded), Is.EqualTo(1));
			Assert.That(sim.Harvest(0, NO_SEASON).Ok);
			Assert.That(sim.RipeLoads(loaded), Is.EqualTo(0));
			Assert.That(loaded.Growth, Is.LessThan(sim.GrowthNeeded(loaded)));
		}

		// 거둘 때 그 작물 씨앗 하나가 덤. 여러 번 거둔 평균이 확률 근처
		[Test]
		public void Harvest_sometimes_gives_back_a_seed()
		{
			const int HARVESTS = 4000;
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.Till(0);
			for (int i = 0; i < HARVESTS; i++)
			{
				sim.Give("seed_turnip", 1);
				sim.Plant(0, "turnip");
				sim.AdvanceMinutes(10000);
				Assert.That(sim.Harvest(0, NO_SEASON).Ok);
			}
			float rate = sim.Count("seed_turnip") / (float)HARVESTS;
			float chance = sim.Catalog.Tuning.HarvestSeedChance;
			TestContext.WriteLine($"씨앗 덤 {rate:P2} (확률 {chance:P0})");
			Assert.That(rate, Is.InRange(chance - 0.01f, chance + 0.01f));
		}

		// 던전에서 돌아오면 가끔 씨앗. 지금 타운홀이나 한 단계 위까지 (다음 단계 맛보기). 전투 규칙은 그대로
		[Test]
		public void Dungeon_return_sometimes_brings_a_seed_up_to_one_town_hall_ahead()
		{
			const int RETURNS = 2000;
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			int seeds = 0;
			HashSet<string> seen = new();
			sim.Gained += gain =>
			{
				if (gain.GoodsId.StartsWith("seed_", StringComparison.Ordinal))
				{
					seeds += gain.Amount;
					seen.Add(gain.GoodsId);
				}
			};
			for (int i = 0; i < RETURNS; i++)
			{
				sim.NotifyExpedition();
			}
			foreach (string seed in seen)
			{
				CropDef crop = sim.Catalog.Crops.Find(candidate => candidate.Seed == seed);
				Assert.That(crop.TownHall, Is.LessThanOrEqualTo(sim.State.TownHall + 1), seed);
			}
			Assert.That(seen.Count, Is.GreaterThan(1), "여러 씨앗");
			Assert.That(seen.Contains("seed_tomato") || seen.Contains("seed_chili") || seen.Contains("seed_pumpkin"), "한 단계 위 씨앗도");
			float rate = seeds / (float)RETURNS;
			float chance = sim.Catalog.Tuning.ExpeditionSeedChance;
			TestContext.WriteLine($"귀환 씨앗 {rate:P1} (확률 {chance:P0}), 종류 {seen.Count}");
			Assert.That(rate, Is.InRange(chance - 0.04f, chance + 0.04f));
		}
	}
}
