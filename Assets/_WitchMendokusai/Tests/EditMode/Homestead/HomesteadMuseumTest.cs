using System;
using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 박물관 이정표. 자료 모양 검사와 봇이 실제로 기증하고 받는 곡선 기록
	public class HomesteadMuseumTest
	{
		private const int FIRST_REWARD_MAX = 3;
		private const int GAP_MAX = 6;
		private const int MILESTONE_TOTAL = 15;
		private const int CASTS_PER_RUN = 6;
		private const int HOURS_PER_DAY = 24;
		private const int SEASONS = 4;
		private const int RUNS = 60;
		private const int SEEDS = 20;
		private const int SIM_SEED_BASE = 11;
		private const int BOT_SEED_BASE = 5;
		private const int START_NYANG = 300;

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

		// 봇: 던전과 마을 한 바퀴, 낚시, 유물 손질, 가진 전시물 전부 기증, 받을 이정표 전부 받기.
		// 기록이 목적. 단언은 엔진 경로 (기증 실패 사유, 받은 수와 상태 일치) 만
		[Test]
		public void Bot_records_museum_reward_curve()
		{
			int dryMin = int.MaxValue;
			int dryMax = 0;
			int drySum = 0;
			foreach (int seed in SeedRange())
			{
				BotRun result = PlayMuseum(seed, seed == 0);
				TestContext.WriteLine($"씨앗 {seed,2}: 열림 {result.OpenRun}판 (그때 기증 {result.DonatedAtOpen}, 받은 이정표 {result.ClaimedAtOpen}). 받은 판 사이 최장 {result.LongestDry}판, 마지막 받은 뒤 {RUNS - result.LastClaimRun}판 더 없음. 끝 기증 {result.Donated}, 받은 이정표 {result.Claimed} [{result.Wings}]");
				Assert.That(result.OpenRun, Is.GreaterThan(0), "박물관이 열림");
				Assert.That(result.ClaimedAtOpen, Is.GreaterThan(0), "열자마자 받을 이정표가 있음");
				dryMin = Math.Min(dryMin, result.LongestDry);
				dryMax = Math.Max(dryMax, result.LongestDry);
				drySum += result.LongestDry;
			}
			TestContext.WriteLine($"씨앗 {SEEDS}개: 받은 판 사이 최장은 최소 {dryMin}, 평균 {drySum / (float)SEEDS:F1}, 최대 {dryMax}판");
		}

		private static IEnumerable<int> SeedRange()
		{
			for (int seed = 0; seed < SEEDS; seed++)
			{
				yield return seed;
			}
		}

		private sealed class BotRun
		{
			public int OpenRun = -1;
			public int DonatedAtOpen;
			public int ClaimedAtOpen;
			public int LongestDry;
			public int LastClaimRun;
			public int Donated;
			public int Claimed;
			public string Wings;
		}

		private static BotRun PlayMuseum(int seed, bool log)
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse, SIM_SEED_BASE + seed);
			purse.Nyang = START_NYANG;
			Random random = new(BOT_SEED_BASE + seed);
			BotRun result = new();
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
				DonateAll(sim);
				int claimedNow = ClaimAll(sim);
				result.Claimed += claimedNow;
				if (result.OpenRun < 0)
				{
					result.OpenRun = run + 1;
					result.DonatedAtOpen = sim.State.Donated.Count;
					result.ClaimedAtOpen = claimedNow;
					result.LastClaimRun = run + 1;
				}
				else if (claimedNow > 0)
				{
					result.LongestDry = Math.Max(result.LongestDry, run + 1 - result.LastClaimRun);
					result.LastClaimRun = run + 1;
				}
				if (log)
				{
					TestContext.WriteLine($"{run + 1,3}판 기증 {sim.State.Donated.Count,2} 받음 {result.Claimed,2} [{WingCounts(sim)}]");
				}
			}
			int stateClaimed = 0;
			foreach (WingDef wing in sim.Catalog.Wings)
			{
				stateClaimed += sim.ClaimedMilestones(wing);
			}
			Assert.That(stateClaimed, Is.EqualTo(result.Claimed), "받은 수와 상태가 같음");
			result.Donated = sim.State.Donated.Count;
			result.Wings = WingCounts(sim);
			return result;
		}

		private static void DonateAll(HomesteadSim sim)
		{
			foreach (WingDef wing in sim.Catalog.Wings)
			{
				foreach (GoodsDef goods in sim.ExhibitsOf(wing))
				{
					HomesteadResult donated = sim.Donate(goods.Id);
					if (donated.Ok == false)
					{
						Assert.That(donated.Failure, Is.EqualTo(HomesteadFailure.NotEnoughGoods).Or.EqualTo(HomesteadFailure.AlreadyDone), goods.Id);
					}
				}
			}
		}

		private static int ClaimAll(HomesteadSim sim)
		{
			int claimed = 0;
			foreach (WingDef wing in sim.Catalog.Wings)
			{
				while (sim.HasClaimableMilestone(wing))
				{
					Assert.That(sim.ClaimMilestone(wing.Id, out RewardDef _).Ok);
					claimed++;
				}
			}
			return claimed;
		}

		private static void Fish(HomesteadSim sim, Random random, int run)
		{
			int season = run % SEASONS;
			for (int cast = 0; cast < CASTS_PER_RUN; cast++)
			{
				string water = cast % 2 == 0 ? "pond" : "stream";
				FishDef fish = sim.PickFish(water, random.Next(HOURS_PER_DAY), season);
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

		private static string WingCounts(HomesteadSim sim)
		{
			List<string> parts = new();
			foreach (WingDef wing in sim.Catalog.Wings)
			{
				parts.Add($"{wing.Id} {sim.DonatedCount(wing)}/{sim.ExhibitsOf(wing).Count}");
			}
			return string.Join(", ", parts);
		}
	}
}
