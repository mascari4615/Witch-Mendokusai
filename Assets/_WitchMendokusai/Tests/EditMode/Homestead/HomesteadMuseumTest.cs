using System;
using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 박물관 이정표 간격. 자료만 보는 검사와 봇이 실제로 모으는 곡선
	public class HomesteadMuseumTest
	{
		private const int FIRST_REWARD_MAX = 3;
		private const int GAP_MAX = 6;
		private const int MILESTONE_TOTAL = 15;
		private const int CASTS_PER_RUN = 6;
		private const int RUNS = 60;

		[Test]
		public void Milestones_start_early_end_at_full_wing_and_never_gap_wide()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			int total = 0;
			foreach (WingDef wing in sim.Catalog.Wings)
			{
				int exhibits = sim.ExhibitsOf(wing).Count;
				Assert.That(wing.Milestones[0].Count, Is.InRange(1, FIRST_REWARD_MAX), $"{wing.Id} 첫 보상");
				Assert.That(wing.Milestones[wing.Milestones.Count - 1].Count, Is.EqualTo(exhibits), $"{wing.Id} 마지막 이정표는 관을 다 채운 때");
				int previous = 0;
				foreach (MilestoneDef milestone in wing.Milestones)
				{
					Assert.That(milestone.Count - previous, Is.InRange(1, GAP_MAX), $"{wing.Id} {milestone.Count} 점 이정표 간격");
					previous = milestone.Count;
				}
				total += wing.Milestones.Count;
			}
			Assert.That(total, Is.EqualTo(MILESTONE_TOTAL));
		}

		// 기증 순서가 무작위일 때 박물관 전체에서 보상이 생기기까지 몇 점을 더 내야 하나
		[Test]
		public void Random_donation_order_reaches_some_reward_within_few_donations()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			int worstFirst = 0;
			int worstGap = 0;
			for (int seed = 0; seed < 500; seed++)
			{
				HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _, seed, null);
				List<string> order = AllExhibits(sim);
				Shuffle(order, new Random(seed));
				Dictionary<string, int> wingOf = WingOfGoods(sim);
				Dictionary<string, int> donated = new();
				Dictionary<string, int> reached = new();
				int sinceReward = 0;
				int first = -1;
				for (int i = 0; i < order.Count; i++)
				{
					WingDef wing = catalog.Wings[wingOf[order[i]]];
					donated[wing.Id] = (donated.TryGetValue(wing.Id, out int count) ? count : 0) + 1;
					sinceReward++;
					int done = reached.TryGetValue(wing.Id, out int got) ? got : 0;
					if (done < wing.Milestones.Count && donated[wing.Id] >= wing.Milestones[done].Count)
					{
						reached[wing.Id] = done + 1;
						worstGap = Math.Max(worstGap, sinceReward);
						sinceReward = 0;
						if (first < 0)
						{
							first = i + 1;
						}
					}
				}
				worstFirst = Math.Max(worstFirst, first);
			}
			TestContext.WriteLine($"무작위 순서 500판: 첫 보상까지 최악 {worstFirst} 점, 보상 사이 최악 {worstGap} 점");
			// 관마다 첫 이정표 하나 모자라게 흩어 내는 것이 최악. 그 합 더하기 하나
			int bound = 1;
			foreach (WingDef wing in catalog.Wings)
			{
				bound += wing.Milestones[0].Count - 1;
			}
			Assert.That(worstFirst, Is.LessThanOrEqualTo(bound));
		}

		// 봇: 던전과 마을 한 바퀴, 낚시 몇 번, 유물 손질, 가진 것 전부 기증. 씨앗 다섯 개로 판 수를 잼
		[Test]
		public void Bot_fills_the_museum_over_runs()
		{
			for (int seed = 0; seed < 5; seed++)
			{
				BotRun result = PlayMuseum(seed, seed == 0);
				TestContext.WriteLine($"씨앗 {seed}: 박물관 {result.OpenRun}판에 열림, 그때 기증 {result.DonatedAtOpen} 이정표 {result.ReachedAtOpen}. 열린 뒤 보상 없는 최장 {result.LongestDry}판, {RUNS}판 끝 기증 {result.Donated} 이정표 {result.Reached} [{result.Wings}]");
				Assert.That(result.OpenRun, Is.GreaterThan(0), "박물관이 열림");
				Assert.That(result.ReachedAtOpen, Is.GreaterThan(0), "열자마자 받을 보상이 있음");
			}
		}

		private sealed class BotRun
		{
			public int OpenRun = -1;
			public int DonatedAtOpen;
			public int ReachedAtOpen;
			public int LongestDry;
			public int Donated;
			public int Reached;
			public string Wings;
		}

		private static BotRun PlayMuseum(int seed, bool log)
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse, 11 + seed);
			purse.Nyang = 300;
			Random random = new(5 + seed);
			BotRun result = new();
			int lastRewardRun = 0;
			int reachedBefore = 0;
			for (int run = 0; run < RUNS; run++)
			{
				HomesteadPlayTest.PlayDungeon(sim, purse, random, run);
				HomesteadPlayTest.PlayTown(sim, random);
				while (sim.CanUpgradeTownHall().Ok)
				{
					sim.UpgradeTownHall();
				}
				if (sim.IsOpen(HomesteadSim.MUSEUM) == false)
				{
					continue;
				}
				Fish(sim, random, run);
				RestoreAll(sim);
				foreach (GoodsDef goods in sim.Catalog.Goods)
				{
					sim.Donate(goods.Id);
				}
				int reached = ReachedMilestones(sim);
				if (result.OpenRun < 0)
				{
					result.OpenRun = run + 1;
					result.DonatedAtOpen = sim.State.Donated.Count;
					result.ReachedAtOpen = reached;
					lastRewardRun = run + 1;
				}
				else if (reached > reachedBefore)
				{
					result.LongestDry = Math.Max(result.LongestDry, run + 1 - lastRewardRun);
					lastRewardRun = run + 1;
				}
				reachedBefore = reached;
				if (log)
				{
					TestContext.WriteLine($"{run + 1,3}판 기증 {sim.State.Donated.Count,2} 이정표 {reached,2} [{WingCounts(sim)}]");
				}
			}
			result.Donated = sim.State.Donated.Count;
			result.Reached = reachedBefore;
			result.Wings = WingCounts(sim);
			return result;
		}

		private static void Fish(HomesteadSim sim, Random random, int run)
		{
			int season = run % 4;
			for (int cast = 0; cast < CASTS_PER_RUN; cast++)
			{
				string water = cast % 2 == 0 ? "pond" : "stream";
				FishDef fish = sim.PickFish(water, random.Next(24), season);
				sim.Land(fish, false, out bool _);
			}
		}

		private static void RestoreAll(HomesteadSim sim)
		{
			while (sim.BeginRestore().Ok)
			{
				RelicDef relic = sim.Catalog.Get<RelicDef>(sim.State.Restoring.Relic);
				bool finished = false;
				while (finished == false)
				{
					sim.Clean(relic.Grime[sim.State.Restoring.Cleaned], out bool _, out finished);
				}
			}
		}

		private static int ReachedMilestones(HomesteadSim sim)
		{
			int reached = 0;
			foreach (WingDef wing in sim.Catalog.Wings)
			{
				foreach (MilestoneDef milestone in wing.Milestones)
				{
					if (sim.DonatedCount(wing) >= milestone.Count)
					{
						reached++;
					}
				}
			}
			return reached;
		}

		private static string WingCounts(HomesteadSim sim)
		{
			List<string> parts = new();
			foreach (WingDef wing in sim.Catalog.Wings)
			{
				parts.Add($"{wing.Id} {sim.DonatedCount(wing)}/{sim.ExhibitsOf(wing).Count}");
			}
			return string.Join(", ", parts);
		}

		private static List<string> AllExhibits(HomesteadSim sim)
		{
			List<string> all = new();
			foreach (WingDef wing in sim.Catalog.Wings)
			{
				foreach (GoodsDef goods in sim.ExhibitsOf(wing))
				{
					all.Add(goods.Id);
				}
			}
			return all;
		}

		private static Dictionary<string, int> WingOfGoods(HomesteadSim sim)
		{
			Dictionary<string, int> wingOf = new();
			for (int i = 0; i < sim.Catalog.Wings.Count; i++)
			{
				foreach (GoodsDef goods in sim.ExhibitsOf(sim.Catalog.Wings[i]))
				{
					wingOf[goods.Id] = i;
				}
			}
			return wingOf;
		}

		private static void Shuffle(List<string> list, Random random)
		{
			for (int i = list.Count - 1; i > 0; i--)
			{
				int j = random.Next(i + 1);
				(list[i], list[j]) = (list[j], list[i]);
			}
		}
	}
}
