using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 마을 관리 봇. 건물 열둘이 언제 서는지, 못 지은 판에 무엇이 막았는지 잼
	public class HomesteadVillageBotTest
	{
		private const int SEEDS = 5;
		private const int RUN_LIMIT = 80;

		private sealed class Record
		{
			public readonly Dictionary<string, int> BuiltRun = new();
			public readonly Dictionary<string, Dictionary<HomesteadFailure, int>> Blocked = new();
			public readonly List<int> TierRuns = new();
			public int AllBuiltRun = -1;
		}

		private static Record Play(int seed)
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse, 11 + seed);
			purse.Nyang = 300;
			Random random = new(5 + seed);
			Record record = new();
			for (int run = 0; run < RUN_LIMIT && record.AllBuiltRun < 0; run++)
			{
				HomesteadPlayTest.PlayDungeon(sim, purse, random, run);
				CountBlocks(sim, record);
				HomesteadPlayTest.PlayTown(sim, random);
				while (sim.CanUpgradeTownHall().Ok)
				{
					sim.UpgradeTownHall();
					record.TierRuns.Add(run + 1);
				}
				foreach (BuildingDef building in sim.Catalog.Buildings)
				{
					if (sim.IsBuilt(building.Id) && record.BuiltRun.ContainsKey(building.Id) == false)
					{
						record.BuiltRun[building.Id] = run + 1;
					}
				}
				if (record.BuiltRun.Count == sim.Catalog.Buildings.Count)
				{
					record.AllBuiltRun = run + 1;
				}
			}
			return record;
		}

		// 아직 안 서고 공사도 없는 건물이 판 시작에 무엇에 막혔나
		private static void CountBlocks(HomesteadSim sim, Record record)
		{
			foreach (BuildingDef building in sim.Catalog.Buildings)
			{
				if (sim.IsBuilt(building.Id) || sim.WorkOn(building.Id) != null)
				{
					continue;
				}
				HomesteadResult result = sim.CanBuild(building.Id);
				if (result.Ok)
				{
					continue;
				}
				if (record.Blocked.TryGetValue(building.Id, out Dictionary<HomesteadFailure, int> counts) == false)
				{
					counts = new Dictionary<HomesteadFailure, int>();
					record.Blocked[building.Id] = counts;
				}
				counts[result.Failure] = counts.TryGetValue(result.Failure, out int count) ? count + 1 : 1;
			}
		}

		private static int Median(List<int> values)
		{
			List<int> sorted = values.OrderBy(value => value).ToList();
			return sorted[sorted.Count / 2];
		}

		[Test]
		public void Every_building_stands_before_the_town_runs_dry()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			List<Record> records = new();
			for (int seed = 0; seed < SEEDS; seed++)
			{
				records.Add(Play(seed));
			}

			foreach (BuildingDef building in catalog.Buildings.OrderBy(building => Median(records.Select(record => record.BuiltRun.TryGetValue(building.Id, out int run) ? run : RUN_LIMIT).ToList())))
			{
				List<int> runs = records.Select(record => record.BuiltRun.TryGetValue(building.Id, out int run) ? run : RUN_LIMIT).ToList();
				Dictionary<HomesteadFailure, int> blocks = new();
				foreach (Record record in records)
				{
					if (record.Blocked.TryGetValue(building.Id, out Dictionary<HomesteadFailure, int> counts))
					{
						foreach (KeyValuePair<HomesteadFailure, int> pair in counts)
						{
							blocks[pair.Key] = (blocks.TryGetValue(pair.Key, out int sum) ? sum : 0) + pair.Value;
						}
					}
				}
				string why = string.Join(", ", blocks.OrderByDescending(pair => pair.Value).Select(pair => $"{pair.Key} {pair.Value}"));
				TestContext.WriteLine($"{building.Id,-9} 회관 {building.TownHall} 중앙 {Median(runs),2}판 ({string.Join("/", runs)}) 막힘 {why}");
			}
			List<int> allBuilt = records.Select(record => record.AllBuiltRun < 0 ? RUN_LIMIT : record.AllBuiltRun).ToList();
			TestContext.WriteLine($"열둘 다 섬: 중앙 {Median(allBuilt)}판 ({string.Join("/", allBuilt)}). 회관 승급 판: {string.Join(" | ", records.Select(record => string.Join(",", record.TierRuns)))}");

			Assert.That(records.All(record => record.AllBuiltRun > 0), "모든 씨앗에서 열둘이 다 섬");
			Assert.That(Median(allBuilt), Is.LessThanOrEqualTo(30), "열둘 다 짓기는 데모 끝 (회관 3단) 뒤 한 구간 안");
		}
	}
}
