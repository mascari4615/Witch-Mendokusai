using System;
using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 마을 축제. 준비물 바치기, 여는 조건, 그날 인사, 다시 열기, 봇으로 처음 여는 판
	public sealed class HomesteadFestivalTest
	{
		private const string HARVEST = "fest_harvest";
		private const string STARS = "fest_stars";
		private const int SLOT_CROP = 0;
		private const int SLOT_DISH = 3;

		// 회관 2단, 주민 둘, 꾸밈을 문턱까지
		private static HomesteadSim ReadySim(out HomesteadTestKit.FakePurse purse)
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out purse);
			sim.TryGetNextTownHall(out TownHallDef next);
			HomesteadTestKit.Afford(sim, purse, next.Cost);
			Assert.IsTrue(sim.UpgradeTownHall().Ok);
			sim.ResidentOf("r_training").Arrived = true;
			sim.ResidentOf("r_shop").Arrived = true;
			AddDecor(sim, sim.Catalog.Get<FestivalDef>(HARVEST).Decor);
			return sim;
		}

		// 자리 규칙은 배치 시험 몫. 여기서는 수만 맞춤
		private static void AddDecor(HomesteadSim sim, int target)
		{
			PlaceableDef decor = sim.Catalog.Placeables.Find(placeable => placeable.Kind == PlaceableKind.Decor);
			while (sim.DecorCount < target)
			{
				sim.State.Village.Add(new PlacedPiece { Uid = sim.State.NextVillageUid++, Id = decor.Id });
			}
		}

		private static void FillHarvest(HomesteadSim sim)
		{
			sim.Give("turnip", 10);
			sim.Give("plum", 6);
			sim.Give("jam", 4);
			for (int slot = 0; slot < SLOT_DISH; slot++)
			{
				Assert.IsTrue(sim.Offer(HARVEST, slot).Ok, $"칸 {slot}");
			}
		}

		[Test]
		public void Data_ties_festivals_to_other_play()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			Assert.That(sim.Catalog.Festivals.Count, Is.EqualTo(2));
			int startDecor = sim.DecorCount;
			foreach (FestivalDef festival in sim.Catalog.Festivals)
			{
				Assert.That(festival.Decor, Is.GreaterThan(startDecor), $"{festival.Id} 꾸밈 문턱이 처음 배치 ({startDecor}) 위");
				Assert.That(festival.Slots.FindAll(slot => slot.Optional).Count, Is.EqualTo(1), $"{festival.Id} 선택 칸 하나");
				Assert.That(festival.Slots.Find(slot => slot.Optional).Kind, Is.EqualTo(GoodsKind.Dish), "선택 칸은 음식");
				HashSet<GoodsKind> kinds = new();
				foreach (FestivalSlotDef slot in festival.Slots)
				{
					Assert.That(slot.Amount, Is.GreaterThan(0));
					Assert.IsTrue(kinds.Add(slot.Kind), $"{festival.Id} 갈래 겹침");
				}
				Assert.That(festival.FirstReward.Furniture.Count, Is.EqualTo(1), "첫 회 기념 가구");
				festival.FirstReward.Furniture.ForEach(furniture => sim.Catalog.Get<FurnitureDef>(furniture));
				Assert.That(festival.Reward.GuildExp * 2, Is.EqualTo(festival.FirstReward.GuildExp), "다시 열면 절반");
			}
		}

		[Test]
		public void A_new_village_cannot_open_and_says_why()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			Assert.That(sim.CanOpenFestival(HARVEST).Failure, Is.EqualTo(HomesteadFailure.TownHallTooLow));
			List<FestivalNeed> needs = sim.FestivalNeeds(sim.Catalog.Get<FestivalDef>(HARVEST));
			Assert.IsFalse(needs.Find(need => need.Kind == FestivalNeedKind.TownHall).IsMet);
			Assert.IsFalse(needs.Find(need => need.Kind == FestivalNeedKind.Residents).IsMet);
			Assert.IsFalse(needs.Find(need => need.Kind == FestivalNeedKind.Decor).IsMet);
		}

		[Test]
		public void Offering_keeps_what_was_given_and_spends_cheap_goods_first()
		{
			HomesteadSim sim = ReadySim(out HomesteadTestKit.FakePurse _);
			sim.Give("turnip", 4);
			Assert.IsTrue(sim.Offer(HARVEST, SLOT_CROP).Ok);
			Assert.That(sim.Given(HARVEST, SLOT_CROP), Is.EqualTo(4), "가진 만큼만");
			Assert.That(sim.CanOffer(HARVEST, SLOT_CROP).Failure, Is.EqualTo(HomesteadFailure.NotEnoughGoods));
			sim.Give("turnip", 10);
			sim.Give("pumpkin", 10);
			Assert.IsTrue(sim.Offer(HARVEST, SLOT_CROP).Ok);
			Assert.That(sim.Given(HARVEST, SLOT_CROP), Is.EqualTo(10));
			Assert.That(sim.Count("turnip"), Is.EqualTo(4), "싼 순무부터");
			Assert.That(sim.Count("pumpkin"), Is.EqualTo(10), "비싼 호박은 아낌");
			Assert.That(sim.CanOffer(HARVEST, SLOT_CROP).Failure, Is.EqualTo(HomesteadFailure.AlreadyDone));
			Assert.That(sim.CanOpenFestival(HARVEST).Failure, Is.EqualTo(HomesteadFailure.NotEnoughGoods), "필수 칸이 남음");
		}

		[Test]
		public void Opening_pays_the_reward_and_residents_take_one_cheer_each()
		{
			HomesteadSim sim = ReadySim(out HomesteadTestKit.FakePurse purse);
			FillHarvest(sim);
			FestivalDef festival = sim.Catalog.Get<FestivalDef>(HARVEST);
			int nyang = purse.Nyang;
			int exp = sim.State.GuildExp;
			Assert.IsTrue(sim.CanOpenFestival(HARVEST).Ok, "음식 칸은 비어도 열림");
			Assert.IsTrue(sim.OpenFestival(HARVEST).Ok);
			Assert.That(purse.Nyang, Is.EqualTo(nyang + festival.FirstReward.Nyang));
			Assert.That(sim.State.GuildExp, Is.GreaterThanOrEqualTo(exp + festival.FirstReward.GuildExp));
			Assert.That(sim.State.OwnedFurniture[festival.FirstReward.Furniture[0]], Is.EqualTo(1));
			Assert.That(sim.Given(HARVEST, SLOT_CROP), Is.EqualTo(0), "연 뒤 준비는 비움");
			Assert.That(sim.ResidentOf("r_training").Affinity, Is.EqualTo(0), "음식 없으면 잔치 호감 없음");

			Assert.IsTrue(sim.TryGetFestivalToday(out FestivalDef today));
			Assert.That(today.Id, Is.EqualTo(HARVEST));
			Assert.IsTrue(sim.CanCheer("r_training"));
			Assert.IsTrue(sim.Cheer("r_training").Ok);
			Assert.That(sim.ResidentOf("r_training").Affinity, Is.EqualTo(festival.CheerPoints));
			Assert.That(sim.Cheer("r_training").Failure, Is.EqualTo(HomesteadFailure.DailyLimit));
			Assert.That(sim.Cheer("r_smithy").Failure, Is.EqualTo(HomesteadFailure.NobodyThere), "안 온 주민");
			Assert.That(sim.CanOpenFestival(STARS).Failure, Is.EqualTo(HomesteadFailure.DailyLimit), "하루 하나");

			sim.AdvanceMinutes(sim.Catalog.Tuning.MinutesPerDay);
			Assert.IsFalse(sim.TryGetFestivalToday(out FestivalDef _), "날이 바뀌면 끝");
			Assert.That(sim.Cheer("r_shop").Failure, Is.EqualTo(HomesteadFailure.NotReady));
		}

		[Test]
		public void Reopening_waits_days_without_deadline_and_pays_half()
		{
			HomesteadSim sim = ReadySim(out HomesteadTestKit.FakePurse purse);
			FillHarvest(sim);
			sim.OpenFestival(HARVEST);
			FestivalDef festival = sim.Catalog.Get<FestivalDef>(HARVEST);
			sim.AdvanceMinutes(sim.Catalog.Tuning.MinutesPerDay);
			FillHarvest(sim);
			Assert.That(sim.CanOpenFestival(HARVEST).Failure, Is.EqualTo(HomesteadFailure.NotReady));
			sim.AdvanceMinutes(sim.Catalog.Tuning.MinutesPerDay * (festival.RepeatDays + 30L));
			int nyang = purse.Nyang;
			Assert.IsTrue(sim.OpenFestival(HARVEST).Ok, "늦게 와도 그대로 열림");
			Assert.That(purse.Nyang, Is.EqualTo(nyang + festival.Reward.Nyang));
			Assert.That(sim.State.OwnedFurniture[festival.FirstReward.Furniture[0]], Is.EqualTo(1), "기념 가구는 처음만");
			Assert.That(sim.FestivalOf(HARVEST).Times, Is.EqualTo(2));
		}

		[Test]
		public void A_feast_dish_raises_every_arrived_resident()
		{
			HomesteadSim sim = ReadySim(out HomesteadTestKit.FakePurse _);
			FillHarvest(sim);
			sim.Give("bread", 3);
			Assert.IsTrue(sim.Offer(HARVEST, SLOT_DISH).Ok);
			Assert.IsTrue(sim.IsFeastReady(sim.Catalog.Get<FestivalDef>(HARVEST)));
			sim.OpenFestival(HARVEST);
			int feast = sim.Catalog.Get<FestivalDef>(HARVEST).FeastPoints;
			Assert.That(sim.ResidentOf("r_training").Affinity, Is.EqualTo(feast));
			Assert.That(sim.ResidentOf("r_shop").Affinity, Is.EqualTo(feast));
			Assert.IsFalse(sim.HasArrived("r_smithy"));
		}

		[Test]
		public void A_half_filled_dish_slot_stays_for_the_next_festival()
		{
			HomesteadSim sim = ReadySim(out HomesteadTestKit.FakePurse _);
			FillHarvest(sim);
			sim.Give("bread", 2);
			Assert.IsTrue(sim.Offer(HARVEST, SLOT_DISH).Ok);
			Assert.IsTrue(sim.OpenFestival(HARVEST).Ok);
			Assert.That(sim.ResidentOf("r_training").Affinity, Is.EqualTo(0), "덜 차면 잔치 호감 없음");
			Assert.That(sim.Given(HARVEST, SLOT_DISH), Is.EqualTo(2), "바친 음식은 남음");
			Assert.That(sim.Given(HARVEST, SLOT_CROP), Is.EqualTo(0), "필수 칸은 씀");
		}

		// 판 수 시험의 봇 (던전 한 판, 마을 한 바퀴) 에 꾸밈 놓기와 바치기를 더해 처음 여는 판을 잼
		[Test]
		public void Bot_opens_both_festivals_within_the_demo_range()
		{
			List<string> lines = new();
			List<int> harvestRuns = new();
			List<int> starRuns = new();
			foreach (int seed in new[] { 11, 23, 37, 41, 53 })
			{
				HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse, seed);
				purse.Nyang = 300;
				Random random = new(seed);
				Dictionary<string, int> opened = new();
				for (int run = 0; run < 80 && opened.Count < sim.Catalog.Festivals.Count; run++)
				{
					HomesteadPlayTest.PlayDungeon(sim, purse, random, run);
					HomesteadPlayTest.PlayTown(sim, random);
					while (sim.CanUpgradeTownHall().Ok)
					{
						sim.UpgradeTownHall();
					}
					foreach (FestivalDef festival in sim.Catalog.Festivals)
					{
						if (opened.ContainsKey(festival.Id) || sim.State.TownHall < festival.TownHall)
						{
							continue;
						}
						PutDecor(sim, festival.Decor);
						for (int slot = 0; slot < festival.Slots.Count; slot++)
						{
							sim.Offer(festival.Id, slot);
						}
						if (sim.OpenFestival(festival.Id).Ok)
						{
							opened[festival.Id] = run + 1;
						}
					}
				}
				int harvest = opened.TryGetValue(HARVEST, out int harvestRun) ? harvestRun : 999;
				int stars = opened.TryGetValue(STARS, out int starRun) ? starRun : 999;
				harvestRuns.Add(harvest);
				starRuns.Add(stars);
				lines.Add($"씨앗 {seed}: 수확 잔치 {harvest}판, 별의 밤 {stars}판, 꾸밈 {sim.DecorCount}, 주민 {sim.ArrivedCount}, 회관 {sim.State.TownHall}");
			}
			lines.ForEach(TestContext.WriteLine);
			harvestRuns.Sort();
			starRuns.Sort();
			Assert.That(harvestRuns[2], Is.InRange(3, 20), "수확 잔치 중앙 판");
			Assert.That(starRuns[2], Is.InRange(harvestRuns[2], 40), "별의 밤 중앙 판");
		}

		// 남는 재료로 싼 꾸밈부터 빈자리에. 사람이 꾸미는 속도의 하한
		private static void PutDecor(HomesteadSim sim, int target)
		{
			List<PlaceableDef> decor = sim.Catalog.Placeables.FindAll(placeable => placeable.Kind == PlaceableKind.Decor);
			decor.Sort((left, right) => (left.Cost.Nyang + Total(left.Cost)).CompareTo(right.Cost.Nyang + Total(right.Cost)));
			VillageAreaDef area = sim.Catalog.VillageArea;
			int cell = 0;
			while (sim.DecorCount < target)
			{
				PlaceableDef pick = decor.Find(candidate => sim.CanMakeDecor(candidate.Id).Ok);
				if (pick == null)
				{
					return;
				}
				bool placed = false;
				for (; cell < area.Width * area.Depth && placed == false; cell++)
				{
					placed = sim.Put(pick.Id, area.X + cell % area.Width, area.Z + cell / area.Width, 0, out PlacedPiece _).Ok;
				}
				if (placed == false)
				{
					return;
				}
			}
		}

		private static int Total(CostDef cost)
		{
			int total = 0;
			foreach (KeyValuePair<string, int> pair in cost.Goods)
			{
				total += pair.Value;
			}
			return total;
		}
	}
}
