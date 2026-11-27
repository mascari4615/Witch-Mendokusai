using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 갱도 봇. 곡괭이 등급마다 1층부터 30층까지 내려가며 규칙의 구멍을 찾음
	// 봇은 어느 바위에 내리막이 숨었는지 모르고, 깰 수 있는 바위를 앞에서부터 깸
	public class HomesteadMineBotTest
	{
		private const int TRIALS = 200;
		private const int RUN_CAP = 300;
		private const float PERFECT = 1f;

		private sealed class DiveResult
		{
			public int Start;
			public int Deepest;
			public int Swings;
		}

		private static HomesteadSim MakeSim(int pickTier, int seed, int deepest)
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _, seed);
			sim.State.Tools[ToolKind.Pickaxe] = pickTier;
			sim.State.MineDeepest = deepest;
			return sim;
		}

		private static int PickTiers(HomesteadSim sim) => sim.Catalog.Tool(ToolKind.Pickaxe).Tiers.Count;

		// 등불 하나로 내려갈 수 있는 데까지. glintChance 는 빛나는 순간에 칠 확률
		private static DiveResult Dive(HomesteadSim sim, int start, Random hand, float glintChance)
		{
			MineRun run = sim.EnterMine(start);
			DiveResult result = new DiveResult { Start = start, Deepest = start };
			while (true)
			{
				if (run.PassageOpen)
				{
					if (run.Descend().Ok == false)
					{
						break;
					}
					result.Deepest = run.Depth;
					continue;
				}
				int target = NextTarget(sim, run);
				Assert.That(target, Is.GreaterThanOrEqualTo(0), $"깊이 {run.Depth}: 내리막이 닫혔는데 깰 바위가 없음 (막다른 층)");
				if (run.Lantern <= 0)
				{
					break;
				}
				float phase = hand.NextDouble() < glintChance ? PERFECT : 0f;
				Assert.That(run.Strike(target, phase).Result.Ok, $"깊이 {run.Depth}: 깰 수 있는 바위를 못 침");
				result.Swings++;
			}
			return result;
		}

		// 남은 타수가 적은 바위부터. 내리막은 깰 수 있는 바위에 고르게 숨으니 싼 것부터가 가장 빠름
		private static int NextTarget(HomesteadSim sim, MineRun run)
		{
			int best = -1;
			for (int i = 0; i < run.Nodes.Count; i++)
			{
				MineNode node = run.Nodes[i];
				if (node.IsBroken || node.Def.Tier > sim.ToolTier(ToolKind.Pickaxe))
				{
					continue;
				}
				if (best < 0 || node.HitsLeft < run.Nodes[best].HitsLeft)
				{
					best = i;
				}
			}
			return best;
		}

		// 승강기의 가장 깊은 곳에서 다시 들어가기를 반복. 30층에 닿을 때까지 판 수
		private static int RunsToBottom(HomesteadSim sim, Random hand, float glintChance)
		{
			for (int run = 1; run <= RUN_CAP; run++)
			{
				List<int> stops = sim.LiftStops();
				Dive(sim, stops[stops.Count - 1], hand, glintChance);
				if (sim.State.MineDeepest >= sim.MineMaxDepth)
				{
					return run;
				}
			}
			return RUN_CAP + 1;
		}

		private static int Percentile(List<int> values, float share)
		{
			List<int> sorted = values.OrderBy(value => value).ToList();
			return sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * share))];
		}

		// ---- 막다른 층

		[Test]
		public void No_floor_is_a_dead_end_for_any_pickaxe()
		{
			for (int tier = 0; tier < PickTiers(MakeSim(0, 1, 0)); tier++)
			{
				HomesteadSim sim = MakeSim(tier, 100 + tier, 30);
				for (int depth = 1; depth <= sim.MineMaxDepth; depth++)
				{
					for (int trial = 0; trial < 60; trial++)
					{
						MineRun run = new MineRunProbe(sim).EnterAt(depth);
						bool reachable = run.PassageOpen || run.Nodes.Any(node => node.HidesPassage && node.Def.Tier <= tier);
						Assert.That(reachable, $"곡괭이 {tier}, 깊이 {depth}");
						Assert.That(run.Nodes.Any(node => node.HidesPassage && node.Def.Tier > tier), Is.False, $"곡괭이 {tier}, 깊이 {depth}: 못 깨는 바위에 내리막");
					}
				}
			}
		}

		// ---- 승강기

		[Test]
		public void Lift_stops_every_five_floors_up_to_where_you_have_been()
		{
			HomesteadSim sim = MakeSim(0, 3, 0);
			Assert.That(sim.LiftStops(), Is.EqualTo(new[] { 1 }));
			sim.State.MineDeepest = 14;
			Assert.That(sim.LiftStops(), Is.EqualTo(new[] { 1, 5, 10 }));
			sim.State.MineDeepest = 30;
			Assert.That(sim.LiftStops(), Is.EqualTo(new[] { 1, 5, 10, 15, 20, 25, 30 }));
			Assert.Throws<ArgumentException>(() => sim.EnterMine(7));
			MineRun bottom = sim.EnterMine(30);
			Assert.That(bottom.Band.Id, Is.EqualTo("b6"));
		}

		[Test]
		public void Bottom_floor_does_not_descend_further()
		{
			HomesteadSim sim = MakeSim(4, 5, 30);
			MineRun run = sim.EnterMine(30);
			while (run.PassageOpen == false)
			{
				Assert.That(run.Strike(NextTarget(sim, run), PERFECT).Result.Ok, "30층 내리막을 열기 전에 막힘");
			}
			Assert.That(run.Descend().Failure, Is.EqualTo(HomesteadFailure.AtMaxLevel));
			Assert.That(sim.State.MineDeepest, Is.EqualTo(30));
		}

		// ---- 구간별 바위와 광석

		[Test]
		public void Each_band_rolls_only_its_own_rocks_in_listed_proportion()
		{
			HomesteadSim sim = MakeSim(4, 11, 30);
			foreach (MineBandDef band in sim.Catalog.MineBands)
			{
				Dictionary<string, int> seen = new Dictionary<string, int>();
				int total = 0;
				for (int trial = 0; trial < 300; trial++)
				{
					MineRun run = new MineRunProbe(sim).EnterAt(band.From + trial % (band.To - band.From + 1));
					Assert.That(run.Band.Id, Is.EqualTo(band.Id));
					Assert.That(run.Nodes.Count, Is.EqualTo(band.Nodes));
					foreach (MineNode node in run.Nodes)
					{
						seen[node.Def.Id] = seen.TryGetValue(node.Def.Id, out int count) ? count + 1 : 1;
						total++;
					}
				}
				float weightSum = band.Weights.Sum(weight => weight.Weight);
				foreach (string id in seen.Keys)
				{
					Assert.That(band.Weights.Any(weight => weight.Node == id), $"{band.Id}: 목록에 없는 바위 {id}");
				}
				foreach (MineWeightDef weight in band.Weights)
				{
					float expected = weight.Weight / weightSum;
					float actual = seen.TryGetValue(weight.Node, out int count) ? (float)count / total : 0f;
					Assert.That(actual, Is.EqualTo(expected).Within(0.04f), $"{band.Id} {weight.Node}");
				}
			}
		}

		[Test]
		public void Broken_rock_gives_one_of_its_own_drops()
		{
			HomesteadSim sim = MakeSim(4, 13, 30);
			foreach (MineBandDef band in sim.Catalog.MineBands)
			{
				for (int trial = 0; trial < 20; trial++)
				{
					MineRun run = new MineRunProbe(sim).EnterAt(band.From);
					for (int i = 0; i < run.Nodes.Count; i++)
					{
						MineNodeDef def = run.Nodes[i].Def;
						Dictionary<string, int> before = def.Drops.ToDictionary(drop => drop.Goods, drop => sim.Count(drop.Goods));
						MineStrike strike;
						do
						{
							strike = run.Strike(i, PERFECT);
							Assert.That(strike.Result.Ok, $"{band.Id} {def.Id}: 깨기 전에 막힘");
						}
						while (strike.Broke == false);
						int gained = def.Drops.Sum(drop => sim.Count(drop.Goods) - before[drop.Goods]);
						Assert.That(gained, Is.GreaterThan(0), $"{band.Id} {def.Id}");
					}
				}
			}
		}

		// ---- 균열 빛

		[Test]
		public void Glint_strike_hits_about_three_times_harder_for_every_pickaxe()
		{
			// 설계: 균열 빛이 가장 밝을 때 치면 세 배쯤. 더하기면 낡은 곡괭이 4배, 흑요 1.5배로 갈림
			for (int tier = 0; tier < PickTiers(MakeSim(0, 1, 0)); tier++)
			{
				HomesteadSim sim = MakeSim(tier, 21 + tier, 30);
				foreach (MineNodeDef def in sim.Catalog.MineNodes)
				{
					def.Tier = 0;
					def.Hits = 1000;
				}
				MineRun run = new MineRunProbe(sim).EnterAt(1);
				int plain = 1000 - HitsAfter(run, 0, 0f);
				int glint = 1000 - HitsAfter(run, 1, PERFECT);
				Assert.That(run.IsGlint(PERFECT), Is.True);
				Assert.That((float)glint / plain, Is.InRange(2.5f, 3.5f), $"곡괭이 {tier}: 보통 {plain}, 빛 {glint}");
			}
		}

		private static int HitsAfter(MineRun run, int index, float phase)
		{
			Assert.That(run.Strike(index, phase).Result.Ok, $"바위 {index} 를 못 침");
			return run.Nodes[index].HitsLeft;
		}

		// ---- 등불

		[Test]
		public void Lantern_out_run_cannot_go_deeper()
		{
			// 설계: 등불이 다하면 올라올 뿐. 마지막 한 번에 연 내리막이나 바위 없이 열린 층으로 더 내려가면 안 됨
			int checkedRuns = 0;
			for (int seed = 0; seed < 300; seed++)
			{
				HomesteadSim sim = MakeSim(0, seed, 0);
				MineRun run = sim.EnterMine(1);
				Random hand = new Random(seed);
				while (run.Lantern > 0)
				{
					if (run.PassageOpen)
					{
						Assert.That(run.Descend().Ok, $"씨앗 {seed}: 등불이 남았는데 못 내려감");
						continue;
					}
					Assert.That(run.Strike(NextTarget(sim, run), hand.NextDouble() < 0.3 ? PERFECT : 0f).Result.Ok, $"씨앗 {seed}: 깊이 {run.Depth} 에서 칠 바위가 없음");
				}
				if (run.PassageOpen == false)
				{
					continue;
				}
				checkedRuns++;
				int depth = run.Depth;
				Assert.That(run.Descend().Failure, Is.EqualTo(HomesteadFailure.LanternOut), $"씨앗 {seed}: 등불 0 으로 {depth} 층에서 내려감");
				Assert.That(sim.State.MineDeepest, Is.EqualTo(depth));
			}
			Assert.That(checkedRuns, Is.GreaterThan(0), "마지막 한 번에 내리막을 연 판이 하나도 없어 아무것도 안 잼");
			TestContext.WriteLine($"300 판 중 마지막 한 번에 내리막을 연 판 {checkedRuns}");
		}

		// ---- 실측 (값을 찍고, 설계가 지키는 선만 잠금)

		[Test]
		public void Measure_depth_per_pickaxe()
		{
			HomesteadSim probe = MakeSim(0, 1, 0);
			TestContext.WriteLine("곡괭이 | 등불 | 1층 한 판 (빛 30%) 중앙/90% | 1층 한 판 (빛 매번) 중앙 | 30층까지 판 수 중앙/90% | 층당 휘두름");
			for (int tier = 0; tier < PickTiers(probe); tier++)
			{
				List<int> deepRandom = new List<int>();
				List<int> deepPerfect = new List<int>();
				List<int> runs = new List<int>();
				int swings = 0;
				int floors = 0;
				int lantern = 0;
				for (int trial = 0; trial < TRIALS; trial++)
				{
					HomesteadSim sim = MakeSim(tier, 1000 + trial, 0);
					lantern = sim.LanternCapacity;
					DiveResult dive = Dive(sim, 1, new Random(trial), 0.3f);
					deepRandom.Add(dive.Deepest);
					swings += dive.Swings;
					floors += dive.Deepest - dive.Start + 1;
					deepPerfect.Add(Dive(MakeSim(tier, 5000 + trial, 0), 1, new Random(trial), 1f).Deepest);
					runs.Add(RunsToBottom(MakeSim(tier, 9000 + trial, 0), new Random(trial), 0.3f));
				}
				TestContext.WriteLine($"{tier} | {lantern} | {Percentile(deepRandom, 0.5f)}/{Percentile(deepRandom, 0.9f)} | {Percentile(deepPerfect, 0.5f)} | {Percentile(runs, 0.5f)}/{Percentile(runs, 0.9f)} | {(float)swings / floors:0.0}");
				Assert.That(Percentile(runs, 0.9f), Is.LessThanOrEqualTo(RUN_CAP), $"곡괭이 {tier}: 30층에 못 닿음");
			}
		}

		[Test]
		public void Measure_floor_cost_per_band()
		{
			HomesteadSim probe = MakeSim(0, 1, 0);
			TestContext.WriteLine("구간 | 곡괭이별 내리막까지 휘두름 중앙 (빛 30%) | 바위 없이 열린 층 비율");
			foreach (MineBandDef band in probe.Catalog.MineBands)
			{
				List<string> costs = new List<string>();
				List<string> open = new List<string>();
				for (int tier = 0; tier < PickTiers(probe); tier++)
				{
					HomesteadSim sim = MakeSim(tier, 77 + tier, 30);
					Random hand = new Random(tier);
					List<int> swings = new List<int>();
					int freeOpen = 0;
					for (int trial = 0; trial < TRIALS; trial++)
					{
						MineRun run = new MineRunProbe(sim).EnterAt(band.From);
						if (run.PassageOpen)
						{
							freeOpen++;
						}
						int count = 0;
						while (run.PassageOpen == false)
						{
							Assert.That(run.Strike(NextTarget(sim, run), hand.NextDouble() < 0.3 ? PERFECT : 0f).Result.Ok, $"{band.Id} 곡괭이 {tier}: 내리막을 열기 전에 등불이 다함");
							count++;
						}
						swings.Add(count);
					}
					costs.Add($"{tier}:{Percentile(swings, 0.5f)}");
					open.Add($"{tier}:{100f * freeOpen / TRIALS:0}%");
				}
				TestContext.WriteLine($"{band.Id} {band.From}-{band.To} | {string.Join(" ", costs)} | {string.Join(" ", open)}");
			}
		}

		// 승강기를 거치지 않고 아무 깊이에 바로 서 봄. 승강기 간격을 1 로 잠시 줄여 들어감. 시험 전용
		private sealed class MineRunProbe
		{
			private readonly HomesteadSim sim;

			public MineRunProbe(HomesteadSim sim)
			{
				this.sim = sim;
			}

			public MineRun EnterAt(int depth)
			{
				HomesteadTuning tuning = sim.Catalog.Tuning;
				int step = tuning.LiftStep;
				int deepest = sim.State.MineDeepest;
				tuning.LiftStep = 1;
				sim.State.MineDeepest = Math.Max(deepest, depth);
				try
				{
					return sim.EnterMine(depth);
				}
				finally
				{
					tuning.LiftStep = step;
					sim.State.MineDeepest = deepest;
				}
			}
		}
	}
}
