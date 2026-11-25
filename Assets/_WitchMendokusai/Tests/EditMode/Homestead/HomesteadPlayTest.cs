using System;
using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 사람 흉내 봇으로 재는 난도와 진행 속도. 수치를 바꾸면 여기서 먼저 걸림
	public class HomesteadPlayTest
	{
		private const float FRAME = 1f / 60f;
		private const float DUEL_LIMIT = 90f;
		private const int SPARE_STOCK = 20;

		// 반응 늦음을 둔 봇. 몸부림을 보고 놓고, 알맞은 구간을 지킴
		private static FishingPhase PlayDuel(FishingDuel duel, float reaction, Random random, out float seconds)
		{
			seconds = 0f;
			Queue<bool> seen = new();
			int delayFrames = Math.Max(1, (int)(reaction / FRAME));
			bool reeling = false;
			while (duel.IsOver == false && seconds < DUEL_LIMIT)
			{
				if (duel.Phase == FishingPhase.Bite)
				{
					duel.Press();
				}
				if (duel.Phase == FishingPhase.Duel)
				{
					bool wantReel = (duel.IsSurging || duel.IsSurgeNear) == false && duel.Tension < duel.TensionCeiling;
					seen.Enqueue(wantReel);
					if (seen.Count > delayFrames)
					{
						reeling = seen.Dequeue();
					}
					seconds += FRAME;
				}
				duel.Step(FRAME, reeling);
			}
			return duel.Phase;
		}

		private static float WinRate(string fishId, int rodTier, float reaction, out float averageSeconds)
		{
			int wins = 0;
			float total = 0f;
			const int TRIES = 200;
			for (int seed = 0; seed < TRIES; seed++)
			{
				HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _, seed);
				sim.State.Tools[ToolKind.Rod] = rodTier;
				FishingDuel duel = sim.StartDuel(sim.Catalog.Get<FishDef>(fishId));
				if (PlayDuel(duel, reaction, new Random(seed), out float seconds) == FishingPhase.Caught)
				{
					wins++;
				}
				total += seconds;
			}
			averageSeconds = total / TRIES;
			return wins / (float)TRIES;
		}

		[Test]
		public void Fishing_difficulty_follows_the_fish()
		{
			foreach (FishDef fish in HomesteadTestKit.LoadCatalog().Fishes)
			{
				float good = WinRate(fish.Id, fish.RodTier, 0.18f, out float seconds);
				float slow = WinRate(fish.Id, fish.RodTier, 0.45f, out float _);
				TestContext.WriteLine($"{fish.Name,-8} 낚싯대 {fish.RodTier} 빠른 손 {good:P0} 느린 손 {slow:P0} {seconds:F1}초");
			}
			Assert.That(WinRate("minnow", 0, 0.45f, out float first), Is.GreaterThan(0.9f), "첫 물고기는 느린 손도 낚음");
			Assert.That(first, Is.InRange(2f, 8f));
			Assert.That(WinRate("mistking", 3, 0.45f, out float _), Is.InRange(0.1f, 0.95f), "가장 센 고기는 놓칠 수 있음");
			Assert.That(WinRate("mistking", 3, 0.3f, out float _), Is.GreaterThan(WinRate("mistking", 1, 0.3f, out float _)), "낚싯대가 도움");
		}

		[Test]
		public void Holding_the_button_snaps_and_never_pressing_loses_a_strong_fish()
		{
			for (int seed = 0; seed < 20; seed++)
			{
				HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _, seed);
				FishingDuel held = sim.StartDuel(sim.Catalog.Get<FishDef>("carp"));
				FishingDuel idle = sim.StartDuel(sim.Catalog.Get<FishDef>("carp"));
				for (float time = 0f; time < DUEL_LIMIT; time += FRAME)
				{
					if (held.Phase == FishingPhase.Bite) held.Press();
					if (idle.Phase == FishingPhase.Bite) idle.Press();
					held.Step(FRAME, true);
					idle.Step(FRAME, false);
				}
				Assert.That(held.Phase, Is.EqualTo(FishingPhase.Snapped));
				Assert.That(idle.Phase, Is.EqualTo(FishingPhase.Escaped));
			}
		}

		[Test]
		public void Every_water_has_a_bite_at_every_hour_with_the_first_rod()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			for (int season = 0; season < 4; season++)
			{
				for (int hour = 0; hour < 24; hour++)
				{
					Assert.DoesNotThrow(() => sim.PickFish("pond", hour, season));
					Assert.DoesNotThrow(() => sim.PickFish("stream", hour, season));
				}
			}
		}

		// 던전 한 판과 마을 한 바퀴를 번갈아. 타운홀 단계가 오르는 판 수를 잼
		[Test]
		public void Town_grows_over_a_reasonable_number_of_runs()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse, 11);
			purse.Nyang = 300;
			Random random = new(5);
			List<int> tierRuns = new();
			int run = 0;
			for (; run < 120 && sim.State.TownHall < sim.Catalog.TownHalls.Count; run++)
			{
				PlayDungeon(sim, purse, random, run);
				PlayTown(sim, random);
				while (sim.CanUpgradeTownHall().Ok)
				{
					sim.UpgradeTownHall();
					tierRuns.Add(run + 1);
				}
			}
			TestContext.WriteLine($"타운홀 {sim.State.TownHall} 에 {run}판. 단계별 판 {string.Join(", ", tierRuns)}. 길드 {sim.GuildLevel}, 냥 {purse.Nyang}, 기증 {sim.State.Donated.Count}, 유물 {sim.State.DirtyRelics}, 깊이 {sim.State.MineDeepest}, 아늑함 {sim.Comfort}");
			foreach (string goods in new[] { "wood", "stone", "plank", "brick", "copper_bar", "iron_bar", "gold_bar", "rainbow", "hide", "bone", "glass" })
			{
				TestContext.WriteLine($"  {goods} {sim.Count(goods)}");
			}
			Assert.That(sim.State.TownHall, Is.GreaterThanOrEqualTo(4));
			Assert.That(tierRuns[0], Is.InRange(1, 6), "첫 승급");
			Assert.That(tierRuns[1], Is.InRange(tierRuns[0] + 1, 16), "데모 끝 단계");
		}

		private static void PlayDungeon(HomesteadSim sim, HomesteadTestKit.FakePurse purse, Random random, int run)
		{
			sim.AdvanceMinutes(600);
			sim.NotifyExpedition();
			purse.Nyang += 350;
			foreach (int item in new[] { 90, 91, 92, 93 })
			{
				purse.Bag[item] = purse.CountItem(item) + random.Next(2, 6);
			}
			if (run > 5)
			{
				purse.Bag[18212] = purse.CountItem(18212) + random.Next(0, 3);
				purse.Bag[18213] = purse.CountItem(18213) + random.Next(0, 3);
			}
			if (run == 4 || run == 9)
			{
				sim.NotifyEvent("boss");
			}
		}

		private static void PlayTown(HomesteadSim sim, Random random)
		{
			foreach (BuildingDef building in sim.Catalog.Buildings)
			{
				sim.Unveil(building.Id);
			}
			int spot = 0;
			foreach (string gather in new[] { "tree", "tree", "tree", "tree", "tree", "tree", "rock", "rock", "rock", "rock", "bush", "bush", "bush", "bush", "herb", "herb", "herb", "mushroompatch", "mushroompatch", "hardtree", "hardtree" })
			{
				sim.Gather("spot" + spot++, gather);
			}
			for (int i = 0; i < sim.State.Plots.Count; i++)
			{
				sim.Till(i);
				sim.Harvest(i, 0);
				string crop = sim.Catalog.Crops[random.Next(4)].Id;
				sim.BuySeed(crop, 1);
				sim.Plant(i, crop);
				sim.Water(i);
			}
			if (sim.IsBuilt("quarry"))
			{
				Dig(sim, random);
			}
			sim.CollectAllJobs();
			int doll = 0;
			foreach (JobDef job in sim.Catalog.Jobs)
			{
				if (sim.IsBuilt(job.Building) && sim.IsStaffed(job.Id) == false)
				{
					sim.Assign(job.Id, doll);
				}
				doll++;
			}
			sim.RefreshRequests();
			foreach (RequestState request in sim.State.Requests.ToArray())
			{
				if (KeepsTownHall(sim, request.Goods, request.Amount))
				{
					sim.Deliver(request.Uid);
				}
			}
			Produce(sim);
			foreach (ToolDef tool in sim.Catalog.Tools)
			{
				if (sim.TryGetNextToolTier(tool.Kind, out ToolTierDef nextTool) && KeepsTownHall(sim, nextTool.Cost))
				{
					sim.UpgradeTool(tool.Kind);
				}
			}
			foreach (BuildingDef building in sim.Catalog.Buildings)
			{
				if (sim.TryGetNextLevel(building.Id, out BuildingLevelDef level) && (sim.IsBuilt(building.Id) == false || KeepsTownHall(sim, level.Cost)))
				{
					sim.StartBuild(building.Id);
				}
			}
			foreach (GoodsDef goods in sim.Catalog.Goods)
			{
				sim.Donate(goods.Id);
			}
		}

		// 다음 승급에 쓸 것은 남김. 처음 짓는 건물만 예외
		private static bool KeepsTownHall(HomesteadSim sim, string goodsId, int amount)
		{
			if (sim.TryGetNextTownHall(out TownHallDef next) == false || next.Cost.Goods.TryGetValue(goodsId, out int need) == false)
			{
				return true;
			}
			return sim.Count(goodsId) - amount >= need;
		}

		private static bool KeepsTownHall(HomesteadSim sim, CostDef cost)
		{
			foreach (KeyValuePair<string, int> pair in cost.Goods)
			{
				if (KeepsTownHall(sim, pair.Key, pair.Value) == false)
				{
					return false;
				}
			}
			return true;
		}

		// 다음 승급과 건물에 모자란 것부터 만듦
		private static void Produce(HomesteadSim sim)
		{
			sim.CollectCrafts();
			Dictionary<string, int> wanted = new();
			if (sim.TryGetNextTownHall(out TownHallDef next))
			{
				foreach (KeyValuePair<string, int> pair in next.Cost.Goods)
				{
					wanted[pair.Key] = pair.Value + SPARE_STOCK;
				}
			}
			for (int round = 0; round < 6; round++)
			{
				foreach (RecipeDef recipe in sim.Catalog.Recipes)
				{
					if (wanted.TryGetValue(recipe.Output, out int need) && sim.Count(recipe.Output) < need)
					{
						sim.StartCraft(recipe.Id);
					}
				}
				foreach (RecipeDef recipe in sim.Catalog.Recipes)
				{
					if (sim.Count(recipe.Output) < 8)
					{
						sim.StartCraft(recipe.Id);
					}
				}
				sim.AdvanceMinutes(60);
				sim.CollectCrafts();
			}
		}

		private static void Dig(HomesteadSim sim, Random random)
		{
			List<int> stops = sim.LiftStops();
			MineRun run = sim.EnterMine(stops[random.Next(stops.Count)]);
			while (run.Lantern > 0)
			{
				int target = -1;
				for (int i = 0; i < run.Nodes.Count; i++)
				{
					if (run.Nodes[i].IsBroken == false && run.Nodes[i].Def.Tier <= sim.ToolTier(ToolKind.Pickaxe))
					{
						target = i;
						break;
					}
				}
				if (target < 0 || run.PassageOpen)
				{
					if (run.Descend().Ok == false)
					{
						return;
					}
					continue;
				}
				run.Strike(target, (float)random.NextDouble());
			}
		}
	}
}
