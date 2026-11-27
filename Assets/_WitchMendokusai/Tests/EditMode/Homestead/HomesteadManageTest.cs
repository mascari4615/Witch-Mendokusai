using System;
using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 관리: 창고, 공작소, 귀환. 던전 한 판 동안 공작소가 얼마나 일하는지 봇으로 잼
	public class HomesteadManageTest
	{
		// 세계 시계는 실제 1초에 1분 (WorldClockSO). 짧은 던전 3분, 기본 던전 10분
		private const int SHORT_DUNGEON_MINUTES = 180;
		private const int DUNGEON_MINUTES = 600;

		private static HomesteadSim MakeWorkshop(int level, out HomesteadTestKit.FakePurse purse)
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out purse);
			while (sim.BuildingLevel(HomesteadSim.WORKSHOP) < level)
			{
				sim.TryGetNextLevel(HomesteadSim.WORKSHOP, out BuildingLevelDef next);
				while (sim.State.TownHall < next.TownHall)
				{
					sim.TryGetNextTownHall(out TownHallDef hall);
					HomesteadTestKit.Afford(sim, purse, hall.Cost);
					sim.UpgradeTownHall();
				}
				HomesteadTestKit.BuildNow(sim, purse, HomesteadSim.WORKSHOP);
			}
			return sim;
		}

		private static void Stock(HomesteadSim sim, RecipeDef recipe, int batches)
		{
			foreach (KeyValuePair<string, int> pair in recipe.Inputs)
			{
				sim.Give(pair.Key, pair.Value * batches);
			}
		}

		[Test]
		public void Every_workshop_recipe_fits_in_the_shortest_dungeon()
		{
			foreach (RecipeDef recipe in HomesteadTestKit.LoadCatalog().Recipes)
			{
				if (recipe.Building == HomesteadSim.BENCH)
				{
					continue;
				}
				Assert.That(recipe.Minutes, Is.InRange(1, SHORT_DUNGEON_MINUTES), $"{recipe.Id} 는 짧은 던전 한 판 안에 끝나야 함");
			}
		}

		[Test]
		public void Selling_a_crafted_good_never_loses_to_selling_its_inputs()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			foreach (RecipeDef recipe in catalog.Recipes)
			{
				GoodsDef output = catalog.Get<GoodsDef>(recipe.Output);
				if (HomesteadSim.CanSell(output) == false)
				{
					continue;
				}
				int inputValue = 0;
				foreach (KeyValuePair<string, int> pair in recipe.Inputs)
				{
					GoodsDef input = catalog.Get<GoodsDef>(pair.Key);
					inputValue += HomesteadSim.CanSell(input) ? input.Price * pair.Value : 0;
				}
				Assert.That(output.Price * recipe.Amount, Is.GreaterThan(inputValue), $"{recipe.Id}: 만들어 팔면 재료째 파는 것보다 손해");
			}
		}

		[Test]
		public void A_finished_craft_does_not_block_a_new_one()
		{
			HomesteadSim sim = MakeWorkshop(1, out HomesteadTestKit.FakePurse _);
			RecipeDef brick = sim.Catalog.Get<RecipeDef>("c_brick");
			Stock(sim, brick, sim.CraftSlots + 1);
			for (int i = 0; i < sim.CraftSlots; i++)
			{
				Assert.That(sim.StartCraft(brick.Id).Ok);
			}
			Assert.That(sim.CanCraft(brick.Id).Failure, Is.EqualTo(HomesteadFailure.Busy), "다 안 된 동안은 칸이 참");
			sim.AdvanceMinutes(brick.Minutes);
			Assert.That(sim.CanCraft(brick.Id).Ok, "다 된 것은 자리를 막지 않음");
			int before = sim.StoredCount(brick.Output);
			Assert.That(sim.StartCraft(brick.Id).Ok);
			Assert.That(sim.StoredCount(brick.Output), Is.EqualTo(before + brick.Amount * sim.CraftSlots), "새로 넣을 때 다 된 것을 창고로 거둠");
			Assert.That(sim.State.Crafts.Count, Is.EqualTo(1));
		}

		[Test]
		public void Workshop_materials_come_from_the_dungeon_bag_after_the_store()
		{
			HomesteadSim sim = MakeWorkshop(1, out HomesteadTestKit.FakePurse purse);
			RecipeDef brick = sim.Catalog.Get<RecipeDef>("c_brick");
			GoodsDef stone = sim.Catalog.Get<GoodsDef>("stone");
			if (stone.ItemId < 0)
			{
				Assert.Ignore("돌이 던전 아이템이 아님");
			}
			sim.Give("stone", 1);
			sim.Give("coal", 1);
			purse.Bag[stone.ItemId] = 10;
			Assert.That(sim.StartCraft(brick.Id).Ok);
			Assert.That(sim.StoredCount("stone"), Is.EqualTo(0), "창고 먼저");
			Assert.That(purse.CountItem(stone.ItemId), Is.EqualTo(10 - (brick.Inputs["stone"] - 1)));
		}

		// 봇: 떠나기 전에 칸마다 한 묶음씩 채우고 돌아와 거둠 (대기열 전 기준). 대기열은 HomesteadCraftQueueTest
		[Test]
		public void Measure_workshop_output_during_one_dungeon()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			TestContext.WriteLine("단계 칸 던전분 | 묶음 | 칸이 일한 몫 (가공 분 / 던전 분, 평균) | 가장 짧은 것과 긴 것");
			for (int level = 1; level <= 3; level++)
			{
				foreach (int dungeon in new[] { SHORT_DUNGEON_MINUTES, DUNGEON_MINUTES })
				{
					int batches = 0;
					double busyShare = 0;
					int recipes = 0;
					int shortest = int.MaxValue;
					int longest = 0;
					foreach (RecipeDef recipe in catalog.Recipes)
					{
						if (recipe.Building == HomesteadSim.BENCH || recipe.Level > level)
						{
							continue;
						}
						HomesteadSim sim = MakeWorkshop(level, out HomesteadTestKit.FakePurse _);
						Stock(sim, recipe, sim.CraftSlots);
						while (sim.StartCraft(recipe.Id).Ok)
						{
						}
						sim.AdvanceMinutes(dungeon);
						int collected = sim.CollectCrafts();
						Assert.That(collected, Is.EqualTo(sim.CraftSlots), $"{recipe.Id} 는 돌아오면 다 돼 있음");
						batches += collected;
						busyShare += Math.Min(1.0, recipe.Minutes / (double)dungeon);
						recipes++;
						shortest = Math.Min(shortest, recipe.Minutes);
						longest = Math.Max(longest, recipe.Minutes);
					}
					int slots = MakeWorkshop(level, out HomesteadTestKit.FakePurse _).CraftSlots;
					TestContext.WriteLine($"{level} {slots} {dungeon} | {slots} | {busyShare / recipes:P0} ({recipes}가지) | {shortest}~{longest}분");
				}
			}
		}

		// 봇: 회관 승급에 드는 벽돌을 공작소로만 만들면 몇 번을 눌러야 하나
		[Test]
		public void Measure_brick_batches_for_town_hall_and_buildings()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			RecipeDef brick = catalog.Get<RecipeDef>("c_brick");
			int need = 0;
			foreach (TownHallDef hall in catalog.TownHalls)
			{
				need += hall.Cost.Goods.TryGetValue(brick.Output, out int amount) ? amount : 0;
			}
			foreach (BuildingDef building in catalog.Buildings)
			{
				foreach (BuildingLevelDef level in building.Levels)
				{
					need += level.Cost.Goods.TryGetValue(brick.Output, out int amount) ? amount : 0;
				}
			}
			int batches = (need + brick.Amount - 1) / brick.Amount;
			TestContext.WriteLine($"벽돌 {need}개 = {batches}묶음, 가공 {batches * brick.Minutes}분. 칸 2개로 던전 10분 판마다 2묶음이면 {Math.Ceiling(batches / 2.0)}판");
			Assert.That(need, Is.GreaterThan(0));
		}

		// 떼기 주석 "빈 칸은 다음 날". 다 떼어도 같은 날에는 다시 안 참
		[Test]
		public void Dismissing_every_request_waits_for_the_next_day()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.RefreshRequests();
			foreach (RequestState request in sim.State.Requests.ToArray())
			{
				sim.Dismiss(request.Uid);
			}
			sim.RefreshRequests();
			Assert.That(sim.State.Requests, Is.Empty, "같은 날");
			sim.AdvanceMinutes(sim.Catalog.Tuning.MinutesPerDay);
			sim.RefreshRequests();
			Assert.That(sim.State.Requests.Count, Is.EqualTo(sim.RequestSlotCount), "다음 날");
		}
	}
}
