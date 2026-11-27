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
		private const int MILESTONE_TOTAL = 16;
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

		private static HomesteadSim OpenMuseum(out HomesteadTestKit.FakePurse purse)
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out purse);
			HomesteadTestKit.BuildNow(sim, purse, HomesteadSim.MUSEUM);
			return sim;
		}

		// 빈 받침 넷: 모름, 본 적 있는데 지금 없음, 가짐, 놓임
		[Test]
		public void Exhibit_state_has_four_steps()
		{
			HomesteadSim sim = OpenMuseum(out HomesteadTestKit.FakePurse _);
			string goods = sim.Catalog.Fishes[0].Goods;
			Assert.That(sim.ExhibitStateOf(goods), Is.EqualTo(ExhibitState.Unknown));
			sim.Give(goods, 1);
			Assert.That(sim.ExhibitStateOf(goods), Is.EqualTo(ExhibitState.Held));
			Assert.That(sim.Sell(goods, 1).Ok);
			Assert.That(sim.ExhibitStateOf(goods), Is.EqualTo(ExhibitState.Seen), "팔았거나 쓴 것");
			sim.Give(goods, 2);
			Assert.That(sim.Donate(goods).Ok);
			Assert.That(sim.ExhibitStateOf(goods), Is.EqualTo(ExhibitState.Donated), "가진 것이 남아도 놓임이 앞");
		}

		// 받침에 놓인 크기보다 큰 것을 가져오면 바꾸고 값의 반을 냥으로. 작거나 같으면 그대로
		[Test]
		public void Larger_fish_replaces_the_stand_and_pays_part_of_its_price()
		{
			HomesteadSim sim = OpenMuseum(out HomesteadTestKit.FakePurse purse);
			FishDef fish = sim.Catalog.Fishes[0];
			sim.State.FishRecords[fish.Id] = new FishRecord { Count = 1, BestSize = 10f };
			sim.Give(fish.Goods, 3);
			Assert.That(sim.Donate(fish.Goods).Ok);
			Assert.That(sim.ExhibitSize(fish.Goods), Is.EqualTo(10f));
			Assert.That(sim.Donate(fish.Goods).Failure, Is.EqualTo(HomesteadFailure.AlreadyDone), "같은 크기는 거절");

			sim.State.FishRecords[fish.Id].BestSize = 12.5f;
			int nyang = purse.Nyang;
			int count = sim.Count(fish.Goods);
			Assert.That(sim.HasLargerFish(fish.Goods));
			Assert.That(sim.Donate(fish.Goods).Ok);
			Assert.That(sim.ExhibitSize(fish.Goods), Is.EqualTo(12.5f));
			Assert.That(sim.Count(fish.Goods), Is.EqualTo(count - 1));
			Assert.That(fish.Goods, Is.EqualTo("minnow"));
			Assert.That(sim.Catalog.Get<GoodsDef>(fish.Goods).Price, Is.EqualTo(10));
			Assert.That(purse.Nyang - nyang, Is.EqualTo(5), "송사리 10냥의 반");
			Assert.That(sim.State.Donated.FindAll(id => id == fish.Goods).Count, Is.EqualTo(1), "기증 수는 그대로");
			Assert.That(sim.HasLargerFish(fish.Goods), Is.False);
		}

		// 낚지 않고 얻은 물고기 (기록 없음) 를 놓으면 크기를 모름. 화면은 '0cm' 대신 크기를 안 씀 (2026-11-27 화면 확인)
		[Test]
		public void Unfished_exhibit_has_no_size_until_a_larger_one_is_caught()
		{
			HomesteadSim sim = OpenMuseum(out HomesteadTestKit.FakePurse _);
			FishDef fish = sim.Catalog.Fishes[0];
			sim.State.FishRecords.Remove(fish.Id);
			sim.Give(fish.Goods, 2);
			Assert.That(sim.Donate(fish.Goods).Ok);
			Assert.That(sim.IsExhibitMeasured(fish.Goods), Is.False, "기록 없이 놓인 받침");

			sim.State.FishRecords[fish.Id] = new FishRecord { Count = 1, BestSize = 8f };
			Assert.That(sim.Donate(fish.Goods).Ok, "낚은 기록이 생기면 바꿀 수 있음");
			Assert.That(sim.IsExhibitMeasured(fish.Goods));
			Assert.That(sim.ExhibitSize(fish.Goods), Is.EqualTo(8f));
		}

		[Test]
		public void Larger_fish_needs_one_in_the_bag()
		{
			HomesteadSim sim = OpenMuseum(out HomesteadTestKit.FakePurse _);
			FishDef fish = sim.Catalog.Fishes[0];
			sim.State.FishRecords[fish.Id] = new FishRecord { Count = 1, BestSize = 10f };
			sim.Give(fish.Goods, 1);
			Assert.That(sim.Donate(fish.Goods).Ok);
			sim.State.FishRecords[fish.Id].BestSize = 20f;
			Assert.That(sim.Donate(fish.Goods).Failure, Is.EqualTo(HomesteadFailure.NotEnoughGoods));
		}

		// 목장 산물 (보통, 좋은 것) 은 갈래가 가공품과 섞여 있어 관이 이름으로 받음. 한 관에만
		[Test]
		public void Ranch_products_have_a_stand_in_one_wing()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			foreach (AnimalDef animal in sim.Catalog.Animals)
			{
				Assert.That(sim.CanExhibit(animal.Product), animal.Product);
				Assert.That(sim.CanExhibit(animal.FineProduct), animal.FineProduct);
			}
			foreach (WingDef wing in sim.Catalog.Wings)
			{
				foreach (string goodsId in wing.Exhibits)
				{
					GoodsDef goods = sim.Catalog.Get<GoodsDef>(goodsId);
					Assert.That(wing.Kinds.Contains(goods.Kind), Is.False, $"{wing.Id} {goodsId} 는 갈래로 이미 받음");
					Assert.That(sim.Catalog.Wings.FindAll(other => other.Kinds.Contains(goods.Kind) || other.Exhibits.Contains(goodsId)).Count, Is.EqualTo(1), goodsId);
					Assert.That(sim.WingOf(goodsId), Is.SameAs(wing));
				}
			}
			Assert.That(sim.CanExhibit("flour"), Is.False, "가공품은 안 받음");
		}

		// 옛 세이브: 물 관 이정표 셋을 받았는데 낚시 모자가 넷째에 있던 때라 모자가 없음. 크기 없이 놓인 물고기
		[Test]
		public void Old_save_catches_up_moved_costume_and_stand_sizes()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			HomesteadState state = new HomesteadState();
			state.WingClaims["waters"] = 3;
			state.Donated.Add("minnow");
			state.FishRecords["minnow"] = new FishRecord { Count = 2, BestSize = 7.5f };
			List<string> granted = HomesteadRepair.CatchUp(state, catalog);
			Assert.That(granted, Does.Contain("hat_fisher"));
			Assert.That(state.OwnedCostumes, Does.Contain("hat_fisher"));
			Assert.That(state.ExhibitSizes["minnow"], Is.EqualTo(7.5f), "옛 받침은 가장 큰 기록으로");
			Assert.That(HomesteadRepair.CatchUp(state, catalog), Is.Empty, "두 번째는 할 것 없음");
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _, state: state);
			Assert.That(sim.HasLargerFish("minnow"), Is.False, "따라잡은 뒤 공짜 바꾸기 없음");
		}

		[Test]
		public void Catalog_check_reports_a_missing_wing_exhibit()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			catalog.Wings[0].Exhibits.Add("no_such_goods");
			Assert.That(HomesteadCatalogCheck.Run(catalog), Has.Some.Contains("no_such_goods"));
		}

		// 기증함 없음 (f06-3): 박물관을 짓기 전에는 기증이 안 됨
		[Test]
		public void No_donation_before_the_museum_opens()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			string goods = sim.Catalog.Fishes[0].Goods;
			sim.Give(goods, 1);
			Assert.That(sim.Donate(goods).Failure, Is.EqualTo(HomesteadFailure.NotBuilt));
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
