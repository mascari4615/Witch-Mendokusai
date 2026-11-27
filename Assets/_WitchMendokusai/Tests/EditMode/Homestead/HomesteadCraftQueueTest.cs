using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 공작소 대기열: 칸 하나에 같은 레시피 여러 묶음. 확정 f08-1, f08-2, f08-3
	public class HomesteadCraftQueueTest
	{
		private const int DUNGEON_MINUTES = 600;
		private const int SHORT_DUNGEON_MINUTES = 180;
		// 설계 목표 (08-manage): 10분 던전 동안 칸이 일한 몫
		private const double BUSY_TARGET = 0.75;

		private static HomesteadSim MakeWorkshop(int level)
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse);
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
		public void Queue_cap_follows_workshop_level()
		{
			Assert.That(MakeWorkshop(1).CraftQueueMax, Is.EqualTo(7));
			Assert.That(MakeWorkshop(2).CraftQueueMax, Is.EqualTo(8));
			Assert.That(MakeWorkshop(3).CraftQueueMax, Is.EqualTo(9));
		}

		[Test]
		public void Batches_share_one_slot_and_take_all_inputs_at_once()
		{
			HomesteadSim sim = MakeWorkshop(1);
			RecipeDef brick = sim.Catalog.Get<RecipeDef>("c_brick");
			int queue = sim.CraftQueueMax;
			Stock(sim, brick, queue + 1);
			Assert.That(sim.MaxCraftBatches(brick.Id), Is.EqualTo(queue), "재료가 넘쳐도 칸 상한까지");
			Assert.That(sim.CanCraft(brick.Id, queue + 1).Failure, Is.EqualTo(HomesteadFailure.NoRoom));
			Assert.That(sim.StartCraft(brick.Id, queue).Ok);
			Assert.That(sim.State.Crafts.Count, Is.EqualTo(1));
			Assert.That(sim.BusyCrafts, Is.EqualTo(1));
			Assert.That(sim.StoredCount("stone"), Is.EqualTo(brick.Inputs["stone"]), "묶음 수만큼 한 번에 뺌");
			Assert.That(sim.CraftFinishAt(sim.State.Crafts[0]), Is.EqualTo(sim.State.Minutes + brick.Minutes * queue));
		}

		[Test]
		public void Collecting_midway_takes_only_finished_batches()
		{
			HomesteadSim sim = MakeWorkshop(1);
			RecipeDef brick = sim.Catalog.Get<RecipeDef>("c_brick");
			Stock(sim, brick, 5);
			Assert.That(sim.StartCraft(brick.Id, 5).Ok);
			sim.AdvanceMinutes(brick.Minutes * 2 + brick.Minutes / 2);
			Assert.That(sim.ReadyBatches, Is.EqualTo(2));
			Assert.That(sim.CollectCrafts(), Is.EqualTo(2));
			Assert.That(sim.StoredCount(brick.Output), Is.EqualTo(brick.Amount * 2));
			CraftState craft = sim.State.Crafts[0];
			Assert.That(craft.Batches, Is.EqualTo(3), "나머지는 계속");
			sim.AdvanceMinutes(brick.Minutes / 2);
			Assert.That(sim.DoneBatches(craft), Is.EqualTo(1), "거둬도 만드는 중인 묶음의 시각은 그대로");
			sim.AdvanceMinutes(brick.Minutes * 2);
			Assert.That(sim.IsCraftDone(craft));
			Assert.That(sim.BusyCrafts, Is.EqualTo(0), "다 된 칸은 막지 않음");
			Assert.That(sim.CollectCrafts(), Is.EqualTo(3));
			Assert.That(sim.State.Crafts, Is.Empty);
		}

		[Test]
		public void Pulling_queued_batches_refunds_every_input()
		{
			HomesteadSim sim = MakeWorkshop(1);
			RecipeDef brick = sim.Catalog.Get<RecipeDef>("c_brick");
			Stock(sim, brick, 5);
			Assert.That(sim.StartCraft(brick.Id, 5).Ok);
			sim.AdvanceMinutes(brick.Minutes + 1);
			Assert.That(sim.QueuedBatches(sim.State.Crafts[0]), Is.EqualTo(3), "끝난 1, 만드는 중 1, 기다리는 3");
			Assert.That(sim.CancelQueuedCraft(0), Is.EqualTo(3));
			Assert.That(sim.StoredCount("stone"), Is.EqualTo(brick.Inputs["stone"] * 3), "기다리던 묶음 재료 전부");
			Assert.That(sim.StoredCount("coal"), Is.EqualTo(brick.Inputs["coal"] * 3));
			Assert.That(sim.State.Crafts[0].Batches, Is.EqualTo(2), "끝난 1 과 만드는 중 1 은 남음");
			Assert.That(sim.CollectCrafts(), Is.EqualTo(1), "끝난 묶음은 거두기로");
			Assert.That(sim.CancelQueuedCraft(0), Is.EqualTo(0));
		}

		[Test]
		public void Save_from_before_the_queue_loads_as_one_batch()
		{
			HomesteadSim sim = MakeWorkshop(1);
			RecipeDef brick = sim.Catalog.Get<RecipeDef>("c_brick");
			Stock(sim, brick, 1);
			Assert.That(sim.StartCraft(brick.Id).Ok);
			string json = JsonConvert.SerializeObject(sim.State);
			string old = json.Replace(",\"Batches\":1", string.Empty);
			Assert.That(old, Is.Not.EqualTo(json), "옛 모양은 Batches 가 없음");
			HomesteadState loaded = JsonConvert.DeserializeObject<HomesteadState>(old);
			Assert.That(loaded.Crafts.Count, Is.EqualTo(1));
			Assert.That(loaded.Crafts[0].Batches, Is.EqualTo(1));
			sim.State.Crafts.Clear();
			sim.State.Crafts.Add(loaded.Crafts[0]);
			sim.AdvanceMinutes(brick.Minutes);
			Assert.That(sim.CollectCrafts(), Is.EqualTo(1));
			Assert.That(sim.StoredCount(brick.Output), Is.EqualTo(brick.Amount));
		}

		// f08-2: 다 된 가공은 손으로 거둠. 시간이 흘러도 창고로 저절로 가지 않음
		[Test]
		public void Finished_crafts_wait_in_the_workshop_until_collected()
		{
			HomesteadSim sim = MakeWorkshop(1);
			RecipeDef brick = sim.Catalog.Get<RecipeDef>("c_brick");
			Stock(sim, brick, 3);
			Assert.That(sim.StartCraft(brick.Id, 3).Ok);
			sim.AdvanceMinutes(DUNGEON_MINUTES);
			Assert.That(sim.StoredCount(brick.Output), Is.EqualTo(0));
			Assert.That(sim.MakeDigest().DoneCrafts, Is.EqualTo(3), "귀환 소식은 거둘 묶음 수");
			Assert.That(sim.CollectCrafts(), Is.EqualTo(3));
		}

		// 봇: 떠나기 전에 칸마다 상한까지 줄 세우고 던전 한 판 뒤 거둠. 대기열 전 (칸마다 1묶음) 과 비교
		[Test]
		public void Measure_queue_keeps_the_workshop_busy_through_a_dungeon()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			TestContext.WriteLine("단계 칸 상한 던전분 | 칸이 일한 몫 1묶음 -> 대기열 | 거둔 묶음 1묶음 -> 대기열");
			for (int level = 1; level <= 3; level++)
			{
				foreach (int dungeon in new[] { SHORT_DUNGEON_MINUTES, DUNGEON_MINUTES })
				{
					double before = 0;
					double after = 0;
					int beforeBatches = 0;
					int afterBatches = 0;
					int recipes = 0;
					int slots = 0;
					int queue = 0;
					foreach (RecipeDef recipe in catalog.Recipes)
					{
						if (recipe.Building == HomesteadSim.BENCH || recipe.Level > level)
						{
							continue;
						}
						beforeBatches += Run(level, recipe, dungeon, 1, out double beforeShare, out slots, out queue);
						afterBatches += Run(level, recipe, dungeon, int.MaxValue, out double afterShare, out slots, out queue);
						before += beforeShare;
						after += afterShare;
						recipes++;
					}
					TestContext.WriteLine($"{level} {slots} {queue} {dungeon} | {before / recipes:P0} -> {after / recipes:P0} | {beforeBatches} -> {afterBatches} ({recipes}가지)");
					// 설계 목표: 10분 던전 동안 칸이 75% 이상 일함. 상한 5 는 1단계 65%, 6 은 74.5% 라 균형 판에서 7, 8, 9 로 올림 (단계마다 상한도 늘어 승급 이득이 있게)
					if (dungeon == DUNGEON_MINUTES)
					{
						Assert.That(after / recipes, Is.GreaterThanOrEqualTo(BUSY_TARGET), $"{level}단계: 10분 던전 동안 칸이 일한 몫이 목표 아래");
						Assert.That(after / recipes, Is.GreaterThanOrEqualTo(before / recipes * 4), $"{level}단계: 10분 던전 동안 칸이 일한 몫");
					}
				}
			}
		}

		private static int Run(int level, RecipeDef recipe, int dungeon, int batchCap, out double busyShare, out int slots, out int queue)
		{
			HomesteadSim sim = MakeWorkshop(level);
			slots = sim.CraftSlots;
			queue = sim.CraftQueueMax;
			int batches = Math.Min(batchCap, queue);
			Stock(sim, recipe, slots * batches);
			while (sim.StartCraft(recipe.Id, batches).Ok)
			{
			}
			sim.AdvanceMinutes(dungeon);
			int collected = sim.CollectCrafts();
			// 실제로 거둔 묶음으로 계산. 칸마다 던전 시간 중 일한 몫
			busyShare = Math.Min(1.0, recipe.Minutes * (double)collected / (slots * (double)dungeon));
			return collected;
		}

		// 봇: 회관과 건물에 드는 벽돌을 던전 10분 판 동안만 만들면 몇 판
		[Test]
		public void Measure_dungeon_runs_for_all_bricks()
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
			int runsBefore = BrickRuns(need, 1);
			int runsAfter = BrickRuns(need, int.MaxValue);
			TestContext.WriteLine($"벽돌 {need}개, 1단계 공작소 10분 던전만: 칸마다 1묶음 {runsBefore}판 -> 대기열 {runsAfter}판");
			Assert.That(runsAfter, Is.LessThan(runsBefore));
		}

		private static int BrickRuns(int need, int batchCap)
		{
			HomesteadSim sim = MakeWorkshop(1);
			RecipeDef brick = sim.Catalog.Get<RecipeDef>("c_brick");
			int runs = 0;
			while (sim.StoredCount(brick.Output) < need)
			{
				int batches = Math.Min(batchCap, Math.Min(sim.CraftQueueMax, DUNGEON_MINUTES / brick.Minutes));
				for (int slot = 0; slot < sim.CraftSlots; slot++)
				{
					Stock(sim, brick, batches);
					sim.StartCraft(brick.Id, batches);
				}
				sim.AdvanceMinutes(DUNGEON_MINUTES);
				sim.CollectCrafts();
				runs++;
			}
			return runs;
		}

		// f08-3: 밭에서 나는 것은 모두 어딘가 쓰임이 있음 (레시피 재료나 의뢰)
		[Test]
		public void Every_crop_has_a_use()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			HashSet<string> used = new HashSet<string>();
			catalog.Recipes.ForEach(recipe => used.UnionWith(recipe.Inputs.Keys));
			catalog.Requests.ForEach(request => used.UnionWith(request.Goods));
			foreach (CropDef crop in catalog.Crops)
			{
				Assert.That(used.Contains(crop.Harvest), $"{crop.Id} 를 쓰는 레시피나 의뢰가 없음");
			}
		}

		// f08-3: 시간당 값이 낮은 결과 (절임, 치즈, 빵) 는 값 대신 쓰임새가 둘 이상
		[TestCase("pickle")]
		[TestCase("cheese")]
		[TestCase("bread")]
		public void Slow_goods_are_rewarded_by_uses(string goodsId)
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			int uses = catalog.Recipes.FindAll(recipe => recipe.Inputs.ContainsKey(goodsId)).Count;
			uses += catalog.Requests.FindAll(request => request.Goods.Contains(goodsId)).Count;
			Assert.That(uses, Is.GreaterThanOrEqualTo(2), $"{goodsId} 의 쓰임");
		}
	}
}
