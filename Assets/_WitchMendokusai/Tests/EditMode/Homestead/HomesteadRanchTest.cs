using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 목장 규칙. 벌 없음: 돌볼수록 손해가 없고, 비운 동안 놓친 것도 하루치까지는 남음
	public class HomesteadRanchTest
	{
		private static HomesteadSim MakeRanch(out HomesteadTestKit.FakePurse purse)
		{
			HomesteadState state = new HomesteadState();
			state.Buildings[HomesteadSim.RANCH] = 3;
			state.TownHall = 4;
			HomesteadSim sim = HomesteadTestKit.MakeSim(out purse, state: state);
			purse.Nyang = 100000;
			return sim;
		}

		private static AnimalState Buy(HomesteadSim sim, string animalId)
		{
			Assert.That(sim.BuyAnimal(animalId).Ok, animalId);
			return sim.State.Animals[sim.State.Animals.Count - 1];
		}

		[Test]
		public void Full_affinity_still_gives_plain_goods_for_recipes()
		{
			HomesteadSim sim = MakeRanch(out HomesteadTestKit.FakePurse _);
			foreach (AnimalDef def in sim.Catalog.Animals)
			{
				AnimalState animal = Buy(sim, def.Id);
				animal.Affinity = sim.Catalog.Tuning.AnimalMaxAffinity;
				sim.AdvanceMinutes(def.IntervalMinutes);
				int plain = sim.Count(def.Product);
				int fine = sim.Count(def.FineProduct);
				Assert.That(sim.Care(animal.Uid, out bool collected).Ok, def.Id);
				Assert.That(collected, def.Id);
				Assert.That(sim.Count(def.Product), Is.EqualTo(plain + 1), def.Id + " 공작소가 받는 보통 산물은 계속 나옴");
				Assert.That(sim.Count(def.FineProduct), Is.EqualTo(fine + 1), def.Id + " 정이 차면 좋은 산물이 덤");
			}
		}

		// 정 단계는 하트 칸. 0 에서 비고 끝 정에서 다 참, 줄지 않음
		[Test]
		public void Hearts_follow_affinity()
		{
			HomesteadSim sim = MakeRanch(out HomesteadTestKit.FakePurse _);
			AnimalState hen = Buy(sim, "hen");
			int hearts = sim.Catalog.Tuning.AnimalHearts;
			Assert.That(hearts, Is.EqualTo(5));
			Assert.That(sim.AnimalHearts(hen), Is.EqualTo(0));
			int previous = 0;
			for (int day = 0; day < 20; day++)
			{
				sim.Care(hen.Uid, out bool _);
				sim.AdvanceMinutes(sim.Catalog.Tuning.MinutesPerDay);
				int now = sim.AnimalHearts(hen);
				Assert.That(now, Is.GreaterThanOrEqualTo(previous));
				Assert.That(now, Is.LessThanOrEqualTo(hearts));
				previous = now;
			}
			Assert.That(hen.Affinity, Is.EqualTo(sim.Catalog.Tuning.AnimalMaxAffinity));
			Assert.That(sim.AnimalHearts(hen), Is.EqualTo(hearts));
		}

		// 좋은 산물은 팔기 말고도 쓰임이 있음: 공작소 조합과 박물관 받침.
		// 의뢰는 넣지 않음. 의뢰는 기한도 버리기도 없어 목장이 없는 사람의 칸을 영영 막음
		[Test]
		public void Fine_products_go_into_a_recipe_and_a_museum_stand()
		{
			HomesteadSim sim = MakeRanch(out HomesteadTestKit.FakePurse _);
			foreach (AnimalDef animal in sim.Catalog.Animals)
			{
				Assert.That(sim.Catalog.Recipes.Exists(recipe => recipe.Inputs.ContainsKey(animal.FineProduct)), animal.FineProduct + " 조합");
				Assert.That(sim.CanExhibit(animal.FineProduct), animal.FineProduct + " 받침");
			}
		}

		// 좋은 산물 조합은 보통 산물로 같은 것을 만들 때보다 하나당 많이 냄 (좋은 것을 넣을 이유)
		[Test]
		public void Fine_recipes_yield_more_than_plain_ones()
		{
			HomesteadSim sim = MakeRanch(out HomesteadTestKit.FakePurse _);
			foreach (AnimalDef animal in sim.Catalog.Animals)
			{
				foreach (RecipeDef fine in sim.Catalog.Recipes.FindAll(recipe => recipe.Inputs.ContainsKey(animal.FineProduct)))
				{
					RecipeDef plain = sim.Catalog.Recipes.Find(recipe => recipe.Output == fine.Output && recipe.Inputs.ContainsKey(animal.Product));
					Assert.That(plain, Is.Not.Null, fine.Id + " 와 짝인 보통 조합");
					float finePer = fine.Amount / (float)fine.Inputs[animal.FineProduct];
					float plainPer = plain.Amount / (float)plain.Inputs[animal.Product];
					Assert.That(finePer, Is.GreaterThan(plainPer), fine.Id);
				}
			}
		}

		[Test]
		public void Low_affinity_gives_plain_only()
		{
			HomesteadSim sim = MakeRanch(out HomesteadTestKit.FakePurse _);
			AnimalState hen = Buy(sim, "hen");
			sim.AdvanceMinutes(sim.Catalog.Get<AnimalDef>("hen").IntervalMinutes);
			sim.Care(hen.Uid, out bool _);
			Assert.That(sim.Count("egg"), Is.EqualTo(1));
			Assert.That(sim.Count("fine_egg"), Is.EqualTo(0));
		}

		[Test]
		public void Products_pile_up_to_one_day_while_away()
		{
			HomesteadSim sim = MakeRanch(out HomesteadTestKit.FakePurse _);
			foreach (AnimalDef def in sim.Catalog.Animals)
			{
				AnimalState animal = Buy(sim, def.Id);
				int expected = sim.ProductCap(def);
				Assert.That(expected, Is.EqualTo(System.Math.Max(1, sim.Catalog.Tuning.MinutesPerDay / def.IntervalMinutes)), def.Id);
				sim.AdvanceMinutes(sim.Catalog.Tuning.MinutesPerDay * 5L);
				Assert.That(sim.ProductsReady(animal), Is.EqualTo(expected), def.Id);
				int before = sim.Count(def.Product);
				sim.Care(animal.Uid, out bool _);
				Assert.That(sim.Count(def.Product) - before, Is.EqualTo(expected), def.Id + " 닷새 비워도 하루치");
				Assert.That(sim.ProductsReady(animal), Is.EqualTo(0), def.Id);
			}
		}

		[Test]
		public void Fine_bonus_is_one_per_collect_even_when_piled()
		{
			HomesteadSim sim = MakeRanch(out HomesteadTestKit.FakePurse _);
			AnimalState hen = Buy(sim, "hen");
			hen.Affinity = sim.Catalog.Tuning.AnimalMaxAffinity;
			sim.AdvanceMinutes(sim.Catalog.Tuning.MinutesPerDay * 2L);
			sim.Care(hen.Uid, out bool _);
			Assert.That(sim.Count("egg"), Is.EqualTo(sim.ProductCap(sim.Catalog.Get<AnimalDef>("hen"))));
			Assert.That(sim.Count("fine_egg"), Is.EqualTo(1), "덤은 거둘 때 하나. 하루 한 번 들르는 수입이 밭 몇 칸 수준을 넘지 않게");
		}

		[Test]
		public void Return_news_counts_piled_products()
		{
			HomesteadSim sim = MakeRanch(out HomesteadTestKit.FakePurse _);
			Buy(sim, "hen");
			Buy(sim, "cow");
			sim.AdvanceMinutes(sim.Catalog.Tuning.MinutesPerDay * 3L);
			int expected = sim.ProductCap(sim.Catalog.Get<AnimalDef>("hen")) + sim.ProductCap(sim.Catalog.Get<AnimalDef>("cow"));
			Assert.That(sim.MakeDigest().AnimalProducts, Is.EqualTo(expected), "소식의 '동물이 낸 것 N개' 는 마리 수가 아니라 개수");
		}

		[Test]
		public void Partial_progress_is_kept_after_collecting()
		{
			HomesteadSim sim = MakeRanch(out HomesteadTestKit.FakePurse _);
			AnimalState hen = Buy(sim, "hen");
			int interval = sim.Catalog.Get<AnimalDef>("hen").IntervalMinutes;
			sim.AdvanceMinutes(interval + 100);
			sim.Care(hen.Uid, out bool collected);
			Assert.That(collected);
			sim.AdvanceMinutes(interval - 100);
			Assert.That(sim.ProductsReady(hen), Is.EqualTo(1), "거둔 뒤 넘친 100분은 다음 몫으로");
		}

		[Test]
		public void Affinity_never_drops_when_neglected()
		{
			HomesteadSim sim = MakeRanch(out HomesteadTestKit.FakePurse _);
			AnimalState cow = Buy(sim, "cow");
			sim.Care(cow.Uid, out bool _);
			int affinity = cow.Affinity;
			sim.AdvanceMinutes(sim.Catalog.Tuning.MinutesPerDay * 60L);
			Assert.That(cow.Affinity, Is.EqualTo(affinity));
		}

		[Test]
		public void Care_raises_affinity_once_per_day()
		{
			HomesteadSim sim = MakeRanch(out HomesteadTestKit.FakePurse _);
			AnimalState sheep = Buy(sim, "sheep");
			Assert.That(sim.Care(sheep.Uid, out bool _).Ok);
			Assert.That(sim.Care(sheep.Uid, out bool _).Failure, Is.EqualTo(HomesteadFailure.DailyLimit));
			Assert.That(sheep.Affinity, Is.EqualTo(sim.Catalog.Tuning.AnimalCarePoints));
		}

		// 정이 차기까지 날 수. 설계 근거: 일주일 안 (자료를 바꾸면 이 시험이 알려 줌)
		[Test]
		public void Fine_product_within_a_week_of_daily_care()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			int points = catalog.Tuning.AnimalCarePoints;
			foreach (AnimalDef def in catalog.Animals)
			{
				int days = (def.FineAffinity + points - 1) / points;
				Assert.That(days, Is.InRange(3, 7), def.Id);
				Assert.That(def.FineAffinity, Is.LessThanOrEqualTo(catalog.Tuning.AnimalMaxAffinity), def.Id);
			}
		}
	}
}
