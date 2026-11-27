using System;
using System.Collections.Generic;
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
				if (sim.Harvest(0, NO_SEASON).Ok)
				{
					net += average * price;
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
		// 2026-11-27 여유: 토마토 45.5 대 감자 43.0, 달콩 61.3 대 호박 59.5. 달콩과 호박은
		// WetRefreshMinutes 가 한 판 뒤 남는 물기 120분 (720 빼기 600) 보다 커서 매번 물이 들 때만 이 값
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
	}
}
