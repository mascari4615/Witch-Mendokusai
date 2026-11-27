using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 생활 전체의 진행 곡선. HomesteadPlayTest 의 봇을 씨앗 여럿으로 돌려
	// 타운홀 단계, 도구 등급, 길드 레벨이 몇 판째에 오는지와 승급을 막는 재료를 잼
	public class HomesteadCurveTest
	{
		private const int SEEDS = 12;
		private const int MAX_RUNS = 120;
		private const string NYANG = "nyang";

		private sealed class Curve
		{
			public readonly Dictionary<string, int> FirstRun = new();
			public readonly Dictionary<int, Dictionary<string, int>> Blockers = new();
		}

		private static Curve Play(int seed)
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse, seed);
			purse.Nyang = 300;
			Random random = new(seed * 31 + 5);
			Curve curve = new();
			for (int run = 0; run < MAX_RUNS && sim.State.TownHall < sim.Catalog.TownHalls.Count; run++)
			{
				HomesteadPlayTest.PlayDungeon(sim, purse, random, run);
				HomesteadPlayTest.PlayTown(sim, random);
				while (sim.CanUpgradeTownHall().Ok)
				{
					sim.UpgradeTownHall();
				}
				Mark(curve, "th" + sim.State.TownHall, run + 1);
				Mark(curve, "guild" + sim.GuildLevel, run + 1);
				foreach (ToolDef tool in sim.Catalog.Tools)
				{
					Mark(curve, tool.Id + (sim.ToolTier(tool.Kind) + 1), run + 1);
				}
				if (sim.TryGetNextTownHall(out TownHallDef next))
				{
					string blocker = Blocker(sim, next.Cost);
					if (curve.Blockers.TryGetValue(next.Tier, out Dictionary<string, int> counts) == false)
					{
						curve.Blockers[next.Tier] = counts = new Dictionary<string, int>();
					}
					counts[blocker] = counts.TryGetValue(blocker, out int count) ? count + 1 : 1;
				}
			}
			return curve;
		}

		// 처음 닿은 판만 적음. 단계를 건너뛰어도 아래 단계는 같은 판으로 채움
		private static void Mark(Curve curve, string key, int run)
		{
			string prefix = key.TrimEnd("0123456789".ToCharArray());
			int level = int.Parse(key.Substring(prefix.Length));
			for (int i = 1; i <= level; i++)
			{
				if (curve.FirstRun.ContainsKey(prefix + i) == false)
				{
					curve.FirstRun[prefix + i] = run;
				}
			}
		}

		// 가진 양 / 필요량이 가장 낮은 칸
		private static string Blocker(HomesteadSim sim, CostDef cost)
		{
			string worst = NYANG;
			double worstRatio = cost.Nyang > 0 ? sim.Purse.Nyang / (double)cost.Nyang : double.MaxValue;
			foreach (KeyValuePair<string, int> pair in cost.Goods)
			{
				double ratio = sim.Count(pair.Key) / (double)pair.Value;
				if (ratio < worstRatio)
				{
					worst = pair.Key;
					worstRatio = ratio;
				}
			}
			return worst;
		}

		private static string Median(List<Curve> curves, string key)
		{
			List<int> runs = curves.Where(curve => curve.FirstRun.ContainsKey(key)).Select(curve => curve.FirstRun[key]).OrderBy(run => run).ToList();
			if (runs.Count == 0)
			{
				return "-";
			}
			string reached = runs.Count < curves.Count ? $" ({runs.Count}/{curves.Count})" : "";
			return $"{runs[runs.Count / 2]} [{runs[0]}~{runs[runs.Count - 1]}]{reached}";
		}

		[Test]
		public void Progress_curve_across_seeds()
		{
			List<Curve> curves = new();
			for (int seed = 0; seed < SEEDS; seed++)
			{
				curves.Add(Play(seed));
			}
			HomesteadSim reference = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			TestContext.WriteLine($"씨앗 {SEEDS}개. 칸은 중앙 판 [최소~최대]");
			for (int tier = 2; tier <= reference.Catalog.TownHalls.Count; tier++)
			{
				Dictionary<string, int> blockers = new();
				foreach (Curve curve in curves)
				{
					if (curve.Blockers.TryGetValue(tier, out Dictionary<string, int> counts))
					{
						foreach (KeyValuePair<string, int> pair in counts)
						{
							blockers[pair.Key] = (blockers.TryGetValue(pair.Key, out int sum) ? sum : 0) + pair.Value;
						}
					}
				}
				string top = string.Join(", ", blockers.OrderByDescending(pair => pair.Value).Take(3).Select(pair => $"{pair.Key} {pair.Value}"));
				TestContext.WriteLine($"타운홀 {tier}: {Median(curves, "th" + tier)}  막은 칸(판 수 합): {top}");
			}
			for (int level = 2; level <= reference.Catalog.Tuning.GuildSteps.Count; level++)
			{
				TestContext.WriteLine($"길드 {level}: {Median(curves, "guild" + level)}");
			}
			foreach (ToolDef tool in reference.Catalog.Tools)
			{
				for (int tier = 2; tier <= tool.Tiers.Count; tier++)
				{
					TestContext.WriteLine($"{tool.Id} {tier}등급 (타운홀 {tool.Tiers[tier - 1].TownHall}): {Median(curves, tool.Id + tier)}");
				}
			}
			Assert.That(curves.All(curve => curve.FirstRun.ContainsKey("th3")), "모든 씨앗이 데모 끝 (타운홀 3) 에 닿음");
			Assert.That(curves.All(curve => curve.FirstRun.ContainsKey("th" + reference.Catalog.TownHalls.Count)), "모든 씨앗이 마지막 타운홀에 닿음");
		}

		// 타운홀은 재료만 (사용자 결정 2026-11-23). 냥이 승급을 막으면 던전이 주는 재료가 아니라 저금이 병목이 됨
		[Test]
		public void Town_hall_costs_goods_only()
		{
			foreach (TownHallDef hall in HomesteadTestKit.LoadCatalog().TownHalls)
			{
				Assert.That(hall.Cost.Nyang, Is.Zero, $"타운홀 {hall.Tier} 은 냥 없이 재료만");
			}
		}
	}
}
