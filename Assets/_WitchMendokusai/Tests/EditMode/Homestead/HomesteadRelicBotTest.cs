using System;
using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 유물 손질을 끝까지 돌리는 봇. 다 보기까지 걸리는 손질 수의 분포를 잼
	public class HomesteadRelicBotTest
	{
		private const int SEEDS = 1000;
		private const int LIMIT = 400;

		private sealed class RelicRun
		{
			public int AllSeen;
			public int FirstSet;
			public int Duplicates;
			public int Nyang;
		}

		// 흙 묻은 유물을 하나씩 꺼내 손질. 다 보면 멈춤
		private static RelicRun PlayUntilAllSeen(int seed, float unownedBias)
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse, seed);
			HomesteadTestKit.BuildNow(sim, purse, HomesteadSim.MUSEUM);
			sim.Catalog.Tuning.UnownedRelicBias = unownedBias;
			int startNyang = purse.Nyang;
			RelicRun run = new();
			while (sim.Catalog.Relics.Exists(relic => sim.IsKnown(relic.Goods) == false) && run.AllSeen < LIMIT)
			{
				sim.State.DirtyRelics = 1;
				sim.BeginRestore();
				RelicDef relic = sim.Catalog.Get<RelicDef>(sim.State.Restoring.Relic);
				if (sim.IsKnown(relic.Goods))
				{
					run.Duplicates++;
				}
				foreach (int tool in relic.Grime)
				{
					sim.Clean(tool, out bool _, out bool _);
				}
				sim.Donate(relic.Goods);
				run.AllSeen++;
				if (run.FirstSet == 0 && HasFullSet(sim))
				{
					run.FirstSet = run.AllSeen;
				}
			}
			run.Nyang = purse.Nyang - startNyang;
			return run;
		}

		private static bool HasFullSet(HomesteadSim sim)
		{
			Dictionary<string, int> seen = new();
			Dictionary<string, int> total = new();
			foreach (RelicDef relic in sim.Catalog.Relics)
			{
				total[relic.Set] = total.TryGetValue(relic.Set, out int count) ? count + 1 : 1;
				if (sim.IsKnown(relic.Goods))
				{
					seen[relic.Set] = seen.TryGetValue(relic.Set, out int known) ? known + 1 : 1;
				}
			}
			foreach (KeyValuePair<string, int> pair in total)
			{
				if (seen.TryGetValue(pair.Key, out int known) && known == pair.Value)
				{
					return true;
				}
			}
			return false;
		}

		private static int Percentile(List<int> sorted, float share) => sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * share))];

		private static List<RelicRun> PlayMany(float unownedBias)
		{
			List<RelicRun> runs = new();
			for (int seed = 0; seed < SEEDS; seed++)
			{
				runs.Add(PlayUntilAllSeen(seed, unownedBias));
			}
			return runs;
		}

		private static string Describe(List<RelicRun> runs, Func<RelicRun, int> pick)
		{
			List<int> values = runs.ConvertAll(run => pick(run));
			values.Sort();
			return $"10% {Percentile(values, 0.1f)}, 가운데 {Percentile(values, 0.5f)}, 90% {Percentile(values, 0.9f)}, 99% {Percentile(values, 0.99f)}, 최대 {values[values.Count - 1]}";
		}

		[Test]
		public void Relic_collection_tail_stays_short()
		{
			float bias = HomesteadTestKit.LoadCatalog().Tuning.UnownedRelicBias;
			List<RelicRun> runs = PlayMany(bias);
			TestContext.WriteLine($"못 본 것 {bias}배. 다 보기: {Describe(runs, run => run.AllSeen)}");
			TestContext.WriteLine($"  첫 벌 완성: {Describe(runs, run => run.FirstSet)}");
			TestContext.WriteLine($"  겹침: {Describe(runs, run => run.Duplicates)}");
			TestContext.WriteLine($"  냥 (덤과 겹침): {Describe(runs, run => run.Nyang)}");
			List<int> allSeen = runs.ConvertAll(run => run.AllSeen);
			allSeen.Sort();
			Assert.That(Percentile(allSeen, 0.5f), Is.LessThan(45), "다 보기 가운데 값");
			Assert.That(Percentile(allSeen, 0.9f), Is.LessThan(70), "다 보기 90% 값");
		}

		[Test]
		public void Catalog_check_reports_a_grime_without_a_tool()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			catalog.Relics[0].Grime.Add(RelicDef.TOOLS);
			Assert.That(HomesteadCatalogCheck.Run(catalog), Has.Some.Contains(catalog.Relics[0].Id));
		}

		[Test]
		public void Catalog_check_reports_a_relic_that_never_rolls()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			catalog.Relics[1].Weight = 0f;
			catalog.Relics[2].Grime.Clear();
			List<string> problems = HomesteadCatalogCheck.Run(catalog);
			Assert.That(problems, Has.Some.Contains(catalog.Relics[1].Id));
			Assert.That(problems, Has.Some.Contains(catalog.Relics[2].Id));
		}

		[Test]
		public void Catalog_check_reports_a_relic_goods_of_another_kind()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			catalog.Relics[0].Goods = "wood";
			Assert.That(HomesteadCatalogCheck.Run(catalog), Has.Some.Contains(catalog.Relics[0].Id));
		}

		[Test]
		[Explicit("측정용. 수치를 고를 때만 돌림")]
		public void Unowned_bias_compare()
		{
			foreach (float bias in new[] { 1f, 2f, 3f, 4f, 8f, 16f })
			{
				List<RelicRun> runs = PlayMany(bias);
				TestContext.WriteLine($"못 본 것 {bias}배. 다 보기: {Describe(runs, run => run.AllSeen)} / 첫 벌: {Describe(runs, run => run.FirstSet)}");
			}
			Assert.Pass();
		}
	}
}
