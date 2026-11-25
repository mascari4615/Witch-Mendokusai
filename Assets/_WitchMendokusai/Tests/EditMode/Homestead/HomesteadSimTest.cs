using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	public class HomesteadSimTest
	{
		[Test]
		public void Catalog_has_no_broken_links()
		{
			List<string> problems = HomesteadCatalogCheck.Run(HomesteadTestKit.LoadCatalog());
			Assert.That(problems, Is.Empty, string.Join("\n", problems));
		}

		[Test]
		public void Catalog_check_reports_a_broken_link()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			catalog.Crops[0].Seed = "없는 씨앗";
			Assert.That(HomesteadCatalogCheck.Run(catalog), Is.Not.Empty);
		}

		// ---- 밭

		[Test]
		public void Crop_grows_while_away_and_never_dies()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.Give("seed_turnip", 1);
			sim.Till(0);
			Assert.That(sim.Plant(0, "turnip").Ok);
			sim.AdvanceMinutes(239);
			Assert.That(sim.PhaseOf(sim.State.Plots[0]), Is.EqualTo(PlotPhase.Growing));
			sim.AdvanceMinutes(1);
			Assert.That(sim.PhaseOf(sim.State.Plots[0]), Is.EqualTo(PlotPhase.Ripe));
			sim.AdvanceMinutes(100000);
			Assert.That(sim.PhaseOf(sim.State.Plots[0]), Is.EqualTo(PlotPhase.Ripe));
			Assert.That(sim.Harvest(0, 3).Ok);
			Assert.That(sim.Count("turnip"), Is.GreaterThan(0));
		}

		[Test]
		public void Water_speeds_up_only_while_wet()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.Give("seed_pumpkin", 1);
			sim.Till(0);
			sim.Plant(0, "pumpkin");
			sim.Water(0);
			sim.AdvanceMinutes(720);
			Assert.That(sim.State.Plots[0].Growth, Is.EqualTo(900f).Within(0.01f), "젖은 720분은 1.5배라 1080, 900에서 익음");
		}

		[Test]
		public void Water_boundary_is_split_inside_one_step()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.Give("seed_moonbean", 1);
			sim.Till(0);
			sim.Plant(0, "moonbean");
			sim.State.Plots[0].WetUntil = 100;
			sim.AdvanceMinutes(300);
			Assert.That(sim.State.Plots[0].Growth, Is.EqualTo(350f).Within(0.01f));
		}

		[Test]
		public void Refused_plant_changes_nothing()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.Till(0);
			Assert.That(sim.Plant(0, "turnip").Failure, Is.EqualTo(HomesteadFailure.NotEnoughGoods));
			Assert.That(sim.State.Plots[0].Crop, Is.Empty);
		}

		[Test]
		public void Regrowing_crop_stays_planted()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.Give("seed_tomato", 1);
			sim.Till(0);
			sim.Plant(0, "tomato");
			sim.AdvanceMinutes(540);
			sim.Harvest(0, 0);
			Assert.That(sim.PhaseOf(sim.State.Plots[0]), Is.EqualTo(PlotPhase.Growing));
			sim.AdvanceMinutes(240);
			Assert.That(sim.PhaseOf(sim.State.Plots[0]), Is.EqualTo(PlotPhase.Ripe));
		}

		// ---- 값

		[Test]
		public void Cost_takes_store_first_then_dungeon_bag()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse);
			sim.Give("wood", 5);
			purse.Bag[0] = 5;
			sim.Give("stone", 4);
			Assert.That(sim.Count("wood"), Is.EqualTo(10));
			Assert.That(sim.StartBuild("farm").Ok);
			Assert.That(sim.StoredCount("wood"), Is.EqualTo(0));
			Assert.That(purse.Bag[0], Is.EqualTo(2));
		}

		[Test]
		public void Unaffordable_build_takes_nothing()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.Give("wood", 8);
			Assert.That(sim.StartBuild("farm").Failure, Is.EqualTo(HomesteadFailure.NotEnoughGoods));
			Assert.That(sim.StoredCount("wood"), Is.EqualTo(8));
			Assert.That(sim.State.Works, Is.Empty);
		}

		// ---- 마을

		[Test]
		public void Expedition_finishes_work_and_unveil_raises_level()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse);
			sim.TryGetNextLevel("farm", out BuildingLevelDef level);
			HomesteadTestKit.Afford(sim, purse, level.Cost);
			sim.StartBuild("farm");
			Assert.That(sim.Unveil("farm").Failure, Is.EqualTo(HomesteadFailure.NotReady));
			sim.NotifyExpedition();
			Assert.That(sim.IsBuilt("farm"), Is.False, "가림막을 걷기 전에는 안 선 것");
			int plots = sim.State.Plots.Count;
			Assert.That(sim.Unveil("farm").Ok);
			Assert.That(sim.State.Plots.Count, Is.EqualTo(plots + sim.Catalog.Tuning.PlotsPerFarmLevel));
		}

		[Test]
		public void Building_with_resident_opens_only_when_both_are_there()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse);
			HomesteadTestKit.BuildNow(sim, purse, "training");
			Assert.That(sim.IsBuilt("training"));
			Assert.That(sim.IsOpen("training"), Is.False);
			sim.NotifyEvent("rescue:training");
			Assert.That(sim.IsOpen("training"));
		}

		[Test]
		public void Building_the_shop_reveals_its_resident()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse);
			HomesteadTestKit.BuildNow(sim, purse, "shop");
			Assert.That(sim.IsOpen("shop"));
		}

		[Test]
		public void Town_hall_needs_only_materials()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse);
			while (sim.TryGetNextTownHall(out TownHallDef next))
			{
				HomesteadTestKit.Afford(sim, purse, next.Cost);
				Assert.That(sim.UpgradeTownHall().Ok, next.Name);
			}
			Assert.That(sim.State.TownHall, Is.EqualTo(sim.Catalog.TownHalls.Count));
			Assert.That(sim.GuildLevel, Is.GreaterThan(1), "승급 보상 경험치");
		}

		[Test]
		public void Every_resident_has_a_path_besides_guild_level()
		{
			foreach (ResidentDef resident in HomesteadTestKit.LoadCatalog().Residents)
			{
				Assert.That(resident.Recruits.Exists(recruit => recruit.Kind != RecruitKind.GuildLevel), resident.Id);
			}
		}

		[Test]
		public void Requests_fill_once_a_day_and_keep_waiting()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.RefreshRequests();
			Assert.That(sim.State.Requests.Count, Is.EqualTo(sim.RequestSlotCount));
			int uid = sim.State.Requests[0].Uid;
			sim.AdvanceMinutes(sim.Catalog.Tuning.MinutesPerDay * 30);
			sim.RefreshRequests();
			Assert.That(sim.State.Requests.Exists(request => request.Uid == uid), "기한 없음");
		}

		[Test]
		public void Gift_is_once_a_day_per_resident()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse);
			HomesteadTestKit.BuildNow(sim, purse, "shop");
			sim.Give("honey", 3);
			Assert.That(sim.Gift("r_shop", "honey").Ok);
			Assert.That(sim.Gift("r_shop", "honey").Failure, Is.EqualTo(HomesteadFailure.DailyLimit));
			sim.AdvanceMinutes(sim.Catalog.Tuning.MinutesPerDay);
			Assert.That(sim.Gift("r_shop", "honey").Ok);
			Assert.That(sim.AffinityStep("r_shop"), Is.GreaterThan(0));
		}

		// ---- 일

		[Test]
		public void Job_stops_at_cap_without_losing_anything()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse);
			HomesteadTestKit.BuildNow(sim, purse, "workshop");
			Assert.That(sim.Assign("j_wood", 4).Ok);
			sim.AdvanceMinutes(100000);
			int cap = sim.JobCap(sim.Catalog.Get<JobDef>("j_wood"));
			Assert.That(sim.CollectJob("j_wood"), Is.EqualTo(cap));
			Assert.That(sim.CollectJob("j_wood"), Is.EqualTo(0));
		}

		[Test]
		public void One_doll_does_one_job()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse);
			HomesteadTestKit.BuildNow(sim, purse, "workshop");
			HomesteadTestKit.BuildNow(sim, purse, "dock");
			sim.Assign("j_wood", 4);
			sim.Assign("j_fish", 4);
			Assert.That(sim.IsStaffed("j_wood"), Is.False);
			Assert.That(sim.JobOf(4), Is.EqualTo("j_fish"));
		}

		[Test]
		public void Watering_job_keeps_every_plot_wet()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse);
			HomesteadTestKit.BuildNow(sim, purse, "farm");
			sim.Assign("j_water", 0);
			sim.Till(0);
			Assert.That(sim.IsWet(sim.State.Plots[0]));
		}

		// ---- 박물관과 유물

		[Test]
		public void Donation_is_once_per_thing_and_milestones_come_in_order()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse);
			HomesteadTestKit.BuildNow(sim, purse, "museum");
			foreach (string goods in new[] { "turnip", "potato", "carrot" })
			{
				sim.Give(goods, 2);
				Assert.That(sim.Donate(goods).Ok);
			}
			Assert.That(sim.Donate("turnip").Failure, Is.EqualTo(HomesteadFailure.AlreadyDone));
			Assert.That(sim.ClaimMilestone("plants", out RewardDef _).Ok);
			Assert.That(sim.OwnedCount("plant_pot"), Is.EqualTo(1));
			Assert.That(sim.ClaimMilestone("plants", out RewardDef _).Ok, Is.False);
		}

		[Test]
		public void Wrong_brush_never_breaks_a_relic()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse);
			HomesteadTestKit.BuildNow(sim, purse, "museum");
			sim.State.DirtyRelics = 1;
			sim.BeginRestore();
			RelicDef relic = sim.Catalog.Get<RelicDef>(sim.State.Restoring.Relic);
			sim.Clean((relic.Grime[0] + 1) % 3, out bool matched, out bool _);
			Assert.That(matched, Is.False);
			bool finished = false;
			foreach (int tool in relic.Grime)
			{
				sim.Clean(tool, out matched, out finished);
				Assert.That(matched);
			}
			Assert.That(finished);
			Assert.That(sim.Count(relic.Goods), Is.EqualTo(1));
		}

		[Test]
		public void All_relics_show_up_without_a_long_tail()
		{
			List<int> tries = new();
			for (int seed = 0; seed < 30; seed++)
			{
				HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse, seed);
				HomesteadTestKit.BuildNow(sim, purse, "museum");
				int count = 0;
				while (sim.Catalog.Relics.Exists(relic => sim.IsKnown(relic.Goods) == false))
				{
					sim.State.DirtyRelics = 1;
					sim.BeginRestore();
					foreach (int tool in sim.Catalog.Get<RelicDef>(sim.State.Restoring.Relic).Grime)
					{
						sim.Clean(tool, out bool _, out bool _);
					}
					count++;
					Assert.That(count, Is.LessThan(200));
				}
				tries.Add(count);
			}
			tries.Sort();
			Assert.That(tries[tries.Count / 2], Is.LessThan(45), "열다섯 점을 다 보는 데 걸린 손질 수의 가운데 값");
		}

		// ---- 갱도

		[Test]
		public void Every_floor_can_be_passed_with_the_first_pickaxe()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.State.MineDeepest = sim.MineMaxDepth;
			foreach (int depth in sim.LiftStops())
			{
				MineRun run = sim.EnterMine(depth);
				bool reachable = run.PassageOpen;
				foreach (MineNode node in run.Nodes)
				{
					reachable |= node.HidesPassage && node.Def.Tier == 0;
				}
				Assert.That(reachable, $"깊이 {depth}");
			}
		}

		[Test]
		public void Weak_pickaxe_spends_no_lantern()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.State.MineDeepest = 30;
			MineRun run = sim.EnterMine(30);
			for (int i = 0; i < run.Nodes.Count; i++)
			{
				if (run.Nodes[i].Def.Tier == 0)
				{
					continue;
				}
				int lantern = run.Lantern;
				Assert.That(run.Strike(i, 1f).Result.Failure, Is.EqualTo(HomesteadFailure.ToolTooWeak));
				Assert.That(run.Lantern, Is.EqualTo(lantern));
			}
		}

		// ---- 집

		[Test]
		public void Furniture_cannot_overlap_on_the_same_layer()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.AddFurniture("wood_table", 2);
			sim.AddFurniture("round_rug", 1);
			Assert.That(sim.Place("wood_table", 0f, 0f, 0f, out PlacedFurniture _).Ok);
			Assert.That(sim.Place("wood_table", 1f, 0.3f, 30f, out PlacedFurniture _).Failure, Is.EqualTo(HomesteadFailure.Blocked));
			Assert.That(sim.Place("round_rug", 0f, 0f, 0f, out PlacedFurniture _).Ok, "깔개는 다른 층");
			Assert.That(sim.Place("wood_table", 2.8f, 0f, 0f, out PlacedFurniture _).Failure, Is.EqualTo(HomesteadFailure.NoRoom), "벽 밖");
		}

		[Test]
		public void Touching_furniture_is_allowed_and_overlap_is_symmetric()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			FurnitureDef chair = catalog.Get<FurnitureDef>("wood_chair");
			FurnitureDef bed = catalog.Get<FurnitureDef>("wood_bed");
			Assert.That(RoomRules.Overlaps(chair, 0f, 0f, 0f, chair, 0.6f, 0f, 0f), Is.False);
			Random random = new(3);
			for (int i = 0; i < 500; i++)
			{
				float x = (float)random.NextDouble() * 3f - 1.5f;
				float z = (float)random.NextDouble() * 3f - 1.5f;
				float yaw = (float)random.NextDouble() * 360f;
				Assert.That(RoomRules.Overlaps(chair, 0f, 0f, 0f, bed, x, z, yaw), Is.EqualTo(RoomRules.Overlaps(bed, x, z, yaw, chair, 0f, 0f, 0f)));
				Assert.That(RoomRules.Overlaps(chair, 0f, 0f, 0f, bed, x, z, yaw), Is.EqualTo(SampledOverlap(chair, bed, x, z, yaw)), $"{x} {z} {yaw}");
			}
		}

		// 촘촘한 점으로 직접 센 겹침. 분리축 식의 반례 찾기
		private static bool SampledOverlap(FurnitureDef chair, FurnitureDef bed, float x, float z, float yaw)
		{
			const int STEPS = 120;
			double radians = yaw * Math.PI / 180.0;
			double cosine = Math.Cos(radians);
			double sine = Math.Sin(radians);
			float inset = 0.011f;
			for (int i = 0; i <= STEPS; i++)
			{
				for (int j = 0; j <= STEPS; j++)
				{
					double localX = (i / (double)STEPS - 0.5) * (bed.Width - inset);
					double localZ = (j / (double)STEPS - 0.5) * (bed.Depth - inset);
					// 엔진의 y 축 회전과 같은 방향
					double worldX = x + localX * cosine + localZ * sine;
					double worldZ = z - localX * sine + localZ * cosine;
					if (Math.Abs(worldX) < chair.Width / 2f && Math.Abs(worldZ) < chair.Depth / 2f)
					{
						return true;
					}
				}
			}
			return false;
		}

		[Test]
		public void Same_furniture_twice_is_worth_less_than_two_kinds()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			List<PlacedFurniture> same = new() { new() { Id = "wood_chair" }, new() { Id = "wood_chair" } };
			List<PlacedFurniture> mixed = new() { new() { Id = "wood_chair" }, new() { Id = "cushion" } };
			Assert.That(RoomRules.Comfort(catalog, mixed), Is.GreaterThan(RoomRules.Comfort(catalog, same)));
		}

		[Test]
		public void First_placement_of_a_kind_pays_guild_exp_once()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.AddFurniture("cushion", 2);
			sim.Place("cushion", 0f, 0f, 0f, out PlacedFurniture first);
			int exp = sim.State.GuildExp;
			sim.PickUp(first.Uid);
			sim.Place("cushion", 0f, 0f, 0f, out PlacedFurniture _);
			Assert.That(exp, Is.EqualTo(sim.Catalog.Tuning.FirstPlaceGuildExp));
			Assert.That(sim.State.GuildExp, Is.EqualTo(exp));
		}

		// ---- 저장

		[Test]
		public void State_survives_a_save_round_trip()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse);
			HomesteadTestKit.BuildNow(sim, purse, "museum");
			HomesteadTestKit.BuildNow(sim, purse, "workshop");
			sim.Give("seed_turnip", 2);
			sim.Till(0);
			sim.Plant(0, "turnip");
			sim.Assign("j_wood", 4);
			sim.AddFurniture("cushion", 1);
			sim.Place("cushion", 1f, 1f, 45f, out PlacedFurniture _);
			sim.RefreshRequests();
			string first = JsonConvert.SerializeObject(sim.State);
			HomesteadState loaded = JsonConvert.DeserializeObject<HomesteadState>(first);
			Assert.That(JsonConvert.SerializeObject(loaded), Is.EqualTo(first));
		}
	}
}
