using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Alchemy;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 집 솥과 물약 선반. 물약 칸에 닿는 길, 처방 노트와 다시 만들기, 흐린 물약 (실패 없음), 선반 한 칸과 던전 한 판
	public class HomesteadBrewTest
	{
		private static HomesteadSim MakeSim() => HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);

		// 게임의 솥 (HomesteadBrewMap) 과 같은 변환: 재료의 (X, Y) 를 방향과 갈기로, 얼룩을 위험 지대로
		private static BrewState Walk(HomesteadCatalog catalog, params string[] goods)
		{
			List<HazardZone> hazards = new List<HazardZone>();
			catalog.BrewHazards.ForEach(hazard => hazards.Add(new HazardZone { Center = new BrewVector(hazard.X, hazard.Y), Radius = hazard.Radius, SeverityPerUnit = hazard.Severity }));
			BrewState state = new BrewState();
			foreach (string goodsId in goods)
			{
				BrewIngredientDef ingredient = catalog.Get<BrewIngredientDef>(goodsId);
				BrewVector move = new BrewVector(ingredient.X, ingredient.Y);
				state = BrewEngine.Apply(state, new BrewStep { Direction = new BrewVector(move.X / move.Magnitude, move.Y / move.Magnitude), Grind = move.Magnitude }, hazards);
			}
			return state;
		}

		private static BrewOutcome Grade(HomesteadCatalog catalog, string brewId, params string[] goods)
		{
			BrewDef brew = catalog.Get<BrewDef>(brewId);
			EffectTarget target = new EffectTarget { Position = new BrewVector(brew.X, brew.Y), Radius = brew.Radius };
			return BrewEngine.Evaluate(Walk(catalog, goods), target, BrewOutcomeRules.Default);
		}

		[Test]
		public void Six_potions_and_one_cloudy_fallback()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			Assert.That(catalog.Brews.Count, Is.EqualTo(7));
			Assert.That(catalog.Brews.FindAll(brew => brew.Fallback).Count, Is.EqualTo(1));
			Assert.That(catalog.BrewIngredients.Count, Is.EqualTo(10));
		}

		// 물약마다 짧고 깨끗한 길 (던전, 갱도, 유물 재료) 과 들풀만으로 가는 긴 길이 둘 다 닿음
		[TestCase("b_magnet", new[] { "mugwort", "mugwort", "lavender" })]
		[TestCase("b_haste", new[] { "clover", "clover", "lavender" })]
		[TestCase("b_nimble", new[] { "bone", "mushroom" })]
		[TestCase("b_nimble", new[] { "clover", "mushroom", "mushroom" })]
		[TestCase("b_might", new[] { "sundew", "sundew" })]
		[TestCase("b_might", new[] { "mugwort", "mugwort", "mugwort", "mushroom" })]
		[TestCase("b_fortune", new[] { "quartz", "quartz" })]
		[TestCase("b_fortune", new[] { "lavender", "lavender", "lavender" })]
		[TestCase("b_revive", new[] { "star_shard", "star_shard" })]
		[TestCase("b_revive", new[] { "clover", "clover", "clover", "lavender", "lavender" })]
		public void Every_potion_is_reachable(string brewId, string[] goods)
		{
			Assert.That(Grade(HomesteadTestKit.LoadCatalog(), brewId, goods).Reached, Is.True);
		}

		[Test]
		public void Short_paths_hit_the_center_cleanly()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			Assert.That(Grade(catalog, "b_magnet", "mugwort", "mugwort", "lavender").Grade, Is.EqualTo(BrewGrade.Masterwork));
			Assert.That(Grade(catalog, "b_might", "sundew", "sundew").Grade, Is.EqualTo(BrewGrade.Masterwork));
			Assert.That(Grade(catalog, "b_revive", "star_shard", "star_shard").Grade, Is.EqualTo(BrewGrade.Masterwork));
		}

		[Test]
		public void Wandering_through_a_stain_loses_the_masterwork_but_still_brews()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			// 아래로 둘, 오른쪽으로 둘, 위로 둘: 끓어 넘침 얼룩을 지나 끌림 칸 (2, 0) 근처... 칸은 (2, 1) 이라 하나 더 위로
			BrewOutcome outcome = Grade(catalog, "b_magnet", "mushroom", "mushroom", "mugwort", "mugwort", "lavender", "lavender", "lavender");
			Assert.That(outcome.Reached, Is.True);
			Assert.That(outcome.SideEffect, Is.GreaterThan(0f));
			Assert.That(outcome.Grade, Is.Not.EqualTo(BrewGrade.Masterwork));
		}

		[Test]
		public void Brewing_takes_used_goods_writes_the_note_and_gives_a_bonus_for_masterwork()
		{
			HomesteadSim sim = MakeSim();
			sim.Give("mugwort", 2);
			sim.Give("lavender", 1);
			Assert.That(sim.IsBrewKnown("b_magnet"), Is.False);
			Assert.That(sim.BrewReached("b_magnet", new[] { "mugwort", "mugwort", "lavender" }, true).Ok);
			Assert.That(sim.Count("mugwort"), Is.EqualTo(0));
			Assert.That(sim.Count("lavender"), Is.EqualTo(0));
			Assert.That(sim.Count("p_magnet"), Is.EqualTo(1 + sim.Catalog.Tuning.BrewMasterworkBonus));
			Assert.That(sim.IsBrewKnown("b_magnet"));
		}

		[Test]
		public void Adding_an_ingredient_needs_one_more_than_already_in_the_pot()
		{
			HomesteadSim sim = MakeSim();
			sim.Give("mugwort", 1);
			Assert.That(sim.CanAddBrewIngredient("mugwort", new string[0]).Ok);
			Assert.That(sim.CanAddBrewIngredient("mugwort", new[] { "mugwort" }).Failure, Is.EqualTo(HomesteadFailure.NotEnoughGoods));
			Assert.That(sim.CanAddBrewIngredient("potato", new string[0]).Failure, Is.EqualTo(HomesteadFailure.Locked));
		}

		[Test]
		public void Missing_every_cell_gives_cloudy_and_returns_the_last_ingredient()
		{
			HomesteadSim sim = MakeSim();
			sim.Give("mugwort", 1);
			sim.Give("clover", 1);
			Assert.That(sim.BrewMissed(new[] { "mugwort", "clover" }).Ok);
			Assert.That(sim.Count("mugwort"), Is.EqualTo(0));
			Assert.That(sim.Count("clover"), Is.EqualTo(1));
			Assert.That(sim.Count("p_cloudy"), Is.EqualTo(1));
		}

		[Test]
		public void A_single_ingredient_miss_still_spends_it()
		{
			HomesteadSim sim = MakeSim();
			sim.Give("mugwort", 1);
			sim.BrewMissed(new[] { "mugwort" });
			Assert.That(sim.Count("mugwort"), Is.EqualTo(0));
			Assert.That(sim.Count("p_cloudy"), Is.EqualTo(1));
		}

		[Test]
		public void Goods_gone_meanwhile_refuses_and_changes_nothing()
		{
			HomesteadSim sim = MakeSim();
			sim.Give("sundew", 1);
			HomesteadResult result = sim.BrewReached("b_might", new[] { "sundew", "sundew" }, true);
			Assert.That(result.Failure, Is.EqualTo(HomesteadFailure.NotEnoughGoods));
			Assert.That(sim.Count("sundew"), Is.EqualTo(1));
			Assert.That(sim.IsBrewKnown("b_might"), Is.False);
		}

		[Test]
		public void Rebrew_follows_the_note_in_one_go()
		{
			HomesteadSim sim = MakeSim();
			Assert.That(sim.CanRebrew("b_might").Failure, Is.EqualTo(HomesteadFailure.Locked));
			sim.Give("sundew", 4);
			sim.BrewReached("b_might", new[] { "sundew", "sundew" }, false);
			Assert.That(sim.Rebrew("b_might").Ok);
			Assert.That(sim.Count("sundew"), Is.EqualTo(0));
			Assert.That(sim.Count("p_might"), Is.EqualTo(2));
			Assert.That(sim.CanRebrew("b_might").Failure, Is.EqualTo(HomesteadFailure.NotEnoughGoods));
		}

		[Test]
		public void Note_keeps_the_better_path()
		{
			HomesteadSim sim = MakeSim();
			sim.Give("mugwort", 3);
			sim.Give("mushroom", 1);
			sim.Give("sundew", 2);
			sim.BrewReached("b_might", new[] { "mugwort", "mugwort", "mugwort", "mushroom" }, false);
			sim.BrewReached("b_might", new[] { "sundew", "sundew" }, false);
			sim.TryGetNote("b_might", out BrewNote note);
			Assert.That(note.Inputs, Is.EquivalentTo(new Dictionary<string, int> { { "sundew", 2 } }));
			// 더 많이 받는 길이 이김
			sim.Give("mugwort", 3);
			sim.Give("mushroom", 1);
			sim.BrewReached("b_might", new[] { "mugwort", "mugwort", "mugwort", "mushroom" }, true);
			sim.TryGetNote("b_might", out note);
			Assert.That(note.Amount, Is.EqualTo(1 + sim.Catalog.Tuning.BrewMasterworkBonus));
			Assert.That(note.Inputs["mugwort"], Is.EqualTo(3));
		}

		[Test]
		public void Shelf_holds_one_potion_and_is_drunk_at_dungeon_start()
		{
			HomesteadSim sim = MakeSim();
			sim.Give("p_might", 1);
			sim.Give("p_haste", 1);
			Assert.That(sim.SetPotion("p_might").Ok);
			Assert.That(sim.SetPotion("p_haste").Ok);
			Assert.That(sim.Count("p_might"), Is.EqualTo(1));
			Assert.That(sim.Count("p_haste"), Is.EqualTo(0));
			BrewDef drunk = sim.DrinkPotion();
			Assert.That(drunk.Effect, Is.EqualTo(PotionEffect.Cooldown));
			Assert.That(sim.HasPotion, Is.False);
			Assert.That(sim.DrinkPotion(), Is.Null);
		}

		[Test]
		public void Shelf_refuses_non_potions()
		{
			HomesteadSim sim = MakeSim();
			sim.Give("bread", 1);
			Assert.That(sim.SetPotion("bread").Failure, Is.EqualTo(HomesteadFailure.Locked));
		}

		[Test]
		public void Shelf_and_meal_both_hold_at_once()
		{
			HomesteadSim sim = MakeSim();
			sim.Give("p_magnet", 1);
			sim.Give("bread", 1);
			Assert.That(sim.SetPotion("p_magnet").Ok);
			Assert.That(sim.SetMeal("bread").Ok);
			Assert.That(sim.DrinkPotion(), Is.Not.Null);
			Assert.That(sim.EatMeal(), Is.Not.Null);
		}

		// 물약은 기존 포션 그림만 빌림. 던전 가방의 포션과 셈하지도 꺼내 쓰지도 않음
		[Test]
		public void Potions_never_count_or_take_dungeon_bag_items()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse);
			foreach (BrewDef brew in sim.Catalog.Brews)
			{
				GoodsDef potion = sim.Catalog.Get<GoodsDef>(brew.Output);
				Assert.That(potion.ItemId, Is.LessThan(0), potion.Id);
				Assert.That(potion.IconItem, Is.GreaterThanOrEqualTo(0), potion.Id);
				purse.Bag[potion.IconItem] = 1;
				Assert.That(sim.Count(potion.Id), Is.EqualTo(0), potion.Id);
			}
			Assert.That(sim.SetPotion("p_might").Failure, Is.EqualTo(HomesteadFailure.NotEnoughGoods));
		}

		[Test]
		public void Note_with_an_ingredient_gone_from_data_is_dropped()
		{
			HomesteadSim sim = MakeSim();
			sim.State.BrewNotes["b_might"] = new BrewNote { Inputs = new Dictionary<string, int> { { "potato", 2 } } };
			sim.State.BrewNotes["b_haste"] = new BrewNote { Inputs = new Dictionary<string, int> { { "clover", 2 } } };
			HomesteadRepair.DropUnknown(sim.State, sim.Catalog);
			Assert.That(sim.State.BrewNotes.Keys, Is.EquivalentTo(new[] { "b_haste" }));
		}

		[Test]
		public void Repair_drops_shelf_potion_and_notes_missing_from_data()
		{
			HomesteadSim sim = MakeSim();
			sim.State.Potion = "gone_potion";
			sim.State.BrewNotes["b_gone"] = new BrewNote();
			sim.State.BrewNotes["b_might"] = new BrewNote { Inputs = new Dictionary<string, int> { { "sundew", 2 } } };
			HomesteadRepair.DropUnknown(sim.State, sim.Catalog);
			Assert.That(sim.State.Potion, Is.Empty);
			Assert.That(sim.State.BrewNotes.Keys, Is.EquivalentTo(new[] { "b_might" }));
		}
	}
}
