using System;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 집 부엌과 식탁. 요리 한 번, 잘 구움 덤, 요리법 열림, 식탁 한 칸과 던전 한 판
	public class HomesteadCookTest
	{
		private static HomesteadSim MakeSim() => HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);

		[Test]
		public void Twelve_cooks_and_six_open_at_start()
		{
			HomesteadSim sim = MakeSim();
			Assert.That(sim.Catalog.Cooks.Count, Is.EqualTo(12));
			Assert.That(sim.Catalog.Cooks.FindAll(sim.IsCookOpen).Count, Is.EqualTo(6));
		}

		[Test]
		public void Cook_tuning_comes_from_the_data_file()
		{
			// cook.json 의 Tuning 이 village.json 의 Tuning 과 합쳐짐 (덮어쓰지 않음)
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			Assert.That(catalog.Tuning.CookMaxSeconds, Is.EqualTo(6f));
			Assert.That(catalog.Tuning.GuildSteps, Is.Not.Empty);
		}

		[Test]
		public void Cooking_takes_inputs_and_gives_one_dish()
		{
			HomesteadSim sim = MakeSim();
			sim.Give("potato", 2);
			Assert.That(sim.Cook("k_potato", false).Ok);
			Assert.That(sim.Count("potato"), Is.EqualTo(0));
			Assert.That(sim.Count("potato_bake"), Is.EqualTo(1));
		}

		[Test]
		public void Well_done_gives_a_bonus_dish()
		{
			HomesteadSim sim = MakeSim();
			sim.Give("potato", 2);
			sim.Cook("k_potato", true);
			Assert.That(sim.Count("potato_bake"), Is.EqualTo(1 + sim.Catalog.Tuning.CookWellDoneBonus));
		}

		[Test]
		public void Missing_input_refuses_and_changes_nothing()
		{
			HomesteadSim sim = MakeSim();
			sim.Give("potato", 1);
			HomesteadResult result = sim.Cook("k_potato", true);
			Assert.That(result.Failure, Is.EqualTo(HomesteadFailure.NotEnoughGoods));
			Assert.That(sim.Count("potato"), Is.EqualTo(1));
			Assert.That(sim.Count("potato_bake"), Is.EqualTo(0));
		}

		[Test]
		public void Locked_cook_opens_when_its_ingredient_is_first_gained()
		{
			HomesteadSim sim = MakeSim();
			CookDef tart = sim.Catalog.Get<CookDef>("k_tart");
			Assert.That(sim.IsCookOpen(tart), Is.False);
			Assert.That(sim.CanCook("k_tart").Failure, Is.EqualTo(HomesteadFailure.Locked));
			sim.Give("strawberry", 2);
			sim.Give("flour", 1);
			Assert.That(sim.IsCookOpen(tart));
			Assert.That(sim.Cook("k_tart", false).Ok);
		}

		[Test]
		public void Ingredient_only_in_the_dungeon_bag_also_opens_the_cook()
		{
			// 꿀 (ItemId 69) 을 던전 가방으로만 가짐. 창고에 안 들어와 Known 에 없음
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse);
			purse.Bag[sim.Catalog.Get<GoodsDef>("honey").ItemId] = 1;
			Assert.That(sim.IsKnown("honey"), Is.False);
			Assert.That(sim.IsCookOpen(sim.Catalog.Get<CookDef>("k_pancake")));
		}

		[Test]
		public void Catalog_check_catches_a_broken_cook_timing()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			catalog.Tuning.CookSweetFrom = 0.9f;
			catalog.Tuning.CookSweetTo = 0.5f;
			Assert.That(HomesteadCatalogCheck.Run(catalog), Is.Not.Empty);
		}

		[Test]
		public void Meal_is_eaten_once_at_the_next_run()
		{
			HomesteadSim sim = MakeSim();
			sim.Give("grill", 1);
			Assert.That(sim.SetMeal("grill").Ok);
			Assert.That(sim.Count("grill"), Is.EqualTo(0), "식탁에 올린 것은 창고에서 빠짐");
			CookDef eaten = sim.EatMeal();
			Assert.That(eaten.Buff, Is.EqualTo(MealBuff.Move));
			Assert.That(eaten.Value, Is.GreaterThan(0));
			Assert.That(sim.EatMeal(), Is.Null, "한 판만. 다시 시작하면 빈 식탁");
		}

		[Test]
		public void Swapping_the_meal_returns_the_old_one()
		{
			HomesteadSim sim = MakeSim();
			sim.Give("grill", 1);
			sim.Give("soup", 1);
			sim.SetMeal("grill");
			sim.SetMeal("soup");
			Assert.That(sim.State.Meal, Is.EqualTo("soup"));
			Assert.That(sim.Count("grill"), Is.EqualTo(1));
			Assert.That(sim.ClearMeal().Ok);
			Assert.That(sim.Count("soup"), Is.EqualTo(1));
			Assert.That(sim.HasMeal, Is.False);
		}

		[Test]
		public void Only_dishes_with_a_cook_go_on_the_table()
		{
			HomesteadSim sim = MakeSim();
			sim.Give("potato", 1);
			Assert.That(sim.SetMeal("potato").Failure, Is.EqualTo(HomesteadFailure.Locked));
			Assert.That(sim.SetMeal("pie").Failure, Is.EqualTo(HomesteadFailure.NotEnoughGoods));
		}

		[Test]
		public void Workshop_dish_has_the_same_meal_buff()
		{
			// 공작소 빵 (c_bread) 도 집 빵과 같은 물건이라 식탁에 올림
			HomesteadSim sim = MakeSim();
			Assert.That(sim.TryGetMeal(sim.Catalog.Get<RecipeDef>("c_bread").Output, out CookDef meal));
			Assert.That(meal.Id, Is.EqualTo("k_bread"));
		}

		[Test]
		public void Unknown_meal_is_dropped_on_load()
		{
			HomesteadState state = new() { Meal = "없는 음식" };
			HomesteadRepair.DropUnknown(state, HomesteadTestKit.LoadCatalog());
			Assert.That(state.Meal, Is.Empty);
		}

		[Test]
		public void Favorite_dish_gift_is_worth_double_a_liked_one()
		{
			HomesteadSim sim = MakeSim();
			Assert.That(sim.GiftPoints("r_trial", "eel_skewer"), Is.EqualTo(sim.Catalog.Tuning.GiftLikedPoints * 2));
		}

		// ---- 굽기 바늘

		[Test]
		public void Needle_swings_between_zero_and_one()
		{
			HomesteadSim sim = MakeSim();
			CookTiming timing = sim.StartCookTiming();
			for (int i = 0; i < 100; i++)
			{
				timing.Step(0.05f);
				Assert.That(timing.Needle, Is.InRange(0f, 1f));
			}
		}

		[Test]
		public void Press_in_the_sweet_zone_is_well_done()
		{
			CookTiming timing = PressWhen(MakeSim(), sweet: true);
			Assert.That(timing.IsOver && timing.IsWellDone);
		}

		[Test]
		public void Press_outside_is_plain_and_never_fails()
		{
			CookTiming timing = PressWhen(MakeSim(), sweet: false);
			Assert.That(timing.IsOver);
			Assert.That(timing.IsWellDone, Is.False);
		}

		[Test]
		public void Waiting_out_the_timer_ends_plain()
		{
			HomesteadSim sim = MakeSim();
			CookTiming timing = sim.StartCookTiming();
			timing.Step(sim.Catalog.Tuning.CookMaxSeconds + 0.01f);
			Assert.That(timing.IsOver);
			Assert.That(timing.IsWellDone, Is.False);
			timing.Press();
			Assert.That(timing.IsWellDone, Is.False, "끝난 뒤 누름은 무시");
		}

		private static CookTiming PressWhen(HomesteadSim sim, bool sweet)
		{
			CookTiming timing = sim.StartCookTiming();
			for (int i = 0; i < 1000 && timing.IsSweet != sweet; i++)
			{
				timing.Step(0.01f);
			}
			if (timing.IsSweet != sweet)
			{
				throw new InvalidOperationException("바늘이 원하는 칸에 오지 않음");
			}
			timing.Press();
			return timing;
		}
	}
}
