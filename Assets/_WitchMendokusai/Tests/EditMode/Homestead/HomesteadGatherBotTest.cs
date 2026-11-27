using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 채집을 봇으로 잼. 도구 등급마다 치는 횟수, 들를 때마다 얻는 양
	public class HomesteadGatherBotTest
	{
		// 마을 배치 (VillageStage.Fields 의 gatherSites) 와 같은 수
		private static readonly (string Id, int Count)[] Spots =
		{
			("tree", 7), ("hardtree", 2), ("bush", 5), ("herb", 3), ("mushroompatch", 2), ("rock", 4),
		};

		// 던전 한 판 뒤 마을에 들르는 간격 (봇 가정. HomesteadPlayTest 와 같음)
		private const int VISIT_MINUTES = 600;
		private const int VISITS = 30;

		private static int HitsAt(HomesteadSim sim, ToolKind kind, int tier, int hits)
		{
			sim.State.Tools[kind] = tier;
			return Math.Max(1, (int)Math.Ceiling(hits / (float)sim.ToolPower(kind)));
		}

		// 등급을 올렸는데 채집에서 아무것도 안 바뀌면 산 값이 헛것
		[Test]
		public void Every_gathering_tool_tier_changes_something()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			foreach (ToolKind kind in sim.Catalog.Gathers.Select(gather => gather.Tool).Distinct())
			{
				List<(int Tier, int Hits)> targets = sim.Catalog.Gathers.Where(gather => gather.Tool == kind).Select(gather => (gather.Tier, gather.Hits)).ToList();
				if (kind == ToolKind.Pickaxe)
				{
					targets.AddRange(sim.Catalog.MineNodes.Select(node => (node.Tier, node.Hits)));
				}
				int tiers = sim.Catalog.Tool(kind).Tiers.Count;
				for (int tier = 1; tier < tiers; tier++)
				{
					int now = tier;
					bool helps = targets.Any(target => target.Tier == now
						|| (target.Tier < now && HitsAt(sim, kind, now, target.Hits) < HitsAt(sim, kind, now - 1, target.Hits)));
					TestContext.WriteLine($"{kind} {tier}: {(helps ? "바뀜" : "그대로")}");
					Assert.That(helps, Is.True, $"{kind} {tier} 단계가 채집에서 바꾸는 것이 없음");
				}
			}
		}

		[Test]
		public void Stronger_axe_takes_a_tree_from_three_hits_to_one()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			GatherDef tree = sim.Catalog.Get<GatherDef>("tree");
			int[] hits = Enumerable.Range(0, sim.Catalog.Tool(ToolKind.Axe).Tiers.Count).Select(tier => HitsAt(sim, ToolKind.Axe, tier, tree.Hits)).ToArray();
			Assert.That(hits, Is.EqualTo(new[] { 3, 2, 1 }));
		}

		[Test]
		public void Weak_tool_only_scratches_and_keeps_the_spot()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			HomesteadResult result = sim.Gather("hardtree:0", "hardtree");
			Assert.That(result.Ok, Is.False);
			Assert.That(sim.IsGatherReady("hardtree:0"), Is.True, "약한 도구로 친 자리는 그대로 남음");
			Assert.That(sim.State.Store.Count, Is.EqualTo(0));
		}

		// 실측표. 수치를 바꾸면 이 출력으로 비교
		[Test]
		public void Measure_supply_per_visit()
		{
			foreach (bool maxTools in new[] { false, true })
			{
				Dictionary<string, double> total = new();
				double taps = 0;
				double gathers = 0;
				const int SEEDS = 20;
				for (int seed = 0; seed < SEEDS; seed++)
				{
					HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _, seed);
					if (maxTools)
					{
						foreach (ToolDef tool in sim.Catalog.Tools)
						{
							sim.State.Tools[tool.Kind] = tool.Tiers.Count - 1;
						}
					}
					for (int visit = 0; visit < VISITS; visit++)
					{
						foreach ((string id, int count) in Spots)
						{
							GatherDef gather = sim.Catalog.Get<GatherDef>(id);
							for (int i = 0; i < count; i++)
							{
								string spot = $"{id}:{i}";
								if (sim.IsGatherReady(spot) && sim.CanGather(gather))
								{
									taps += sim.GatherHits(gather);
									gathers++;
									sim.Gather(spot, id);
								}
							}
						}
						sim.AdvanceMinutes(VISIT_MINUTES);
					}
					foreach (KeyValuePair<string, int> pair in sim.State.Store)
					{
						total[pair.Key] = (total.TryGetValue(pair.Key, out double sum) ? sum : 0) + pair.Value;
					}
				}
				double runs = SEEDS * (double)VISITS;
				string goods = string.Join(", ", total.OrderByDescending(pair => pair.Value).Select(pair => $"{pair.Key} {pair.Value / runs:F1}"));
				TestContext.WriteLine($"{(maxTools ? "끝 도구" : "처음 도구")}: 들를 때 채집 {gathers / runs:F1}곳, 치기 {taps / runs:F1}번. {goods}");
				Assert.That(total.TryGetValue("wood", out double wood) ? wood / runs : 0, Is.GreaterThan(3), "나무는 들를 때마다 손에 쥠");
			}
		}

		// 되돌아오는 분이 들르는 간격에 눌려 같은 값이 되는지
		[Test]
		public void Measure_effective_respawn()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			foreach (GatherDef gather in catalog.Gathers)
			{
				int visits = (int)Math.Ceiling(gather.RespawnMinutes / (double)VISIT_MINUTES);
				TestContext.WriteLine($"{gather.Id,-14} {gather.RespawnMinutes,5}분 -> {visits}번째 들를 때마다 ({visits * VISIT_MINUTES}분)");
			}
			Assert.Pass();
		}
	}
}
