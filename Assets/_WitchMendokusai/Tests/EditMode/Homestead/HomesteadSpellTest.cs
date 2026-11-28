using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 생활 주문. 연구 한 번, 북돋기로 자라는 밭 한 칸을 바로 익힘, 업그레이드 축과 겹치지 않음
	public class HomesteadSpellTest
	{
		private const string RIPEN = HomesteadSim.SPELL_RIPEN;

		private static HomesteadSim MakeSim() => HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);

		private static void GiveAll(HomesteadSim sim, Dictionary<string, int> goods)
		{
			foreach (KeyValuePair<string, int> pair in goods)
			{
				sim.Give(pair.Key, pair.Value);
			}
		}

		private static HomesteadSim LearnedSim()
		{
			HomesteadSim sim = MakeSim();
			GiveAll(sim, sim.Catalog.Get<SpellDef>(RIPEN).Research);
			Assert.That(sim.Research(RIPEN).Ok);
			return sim;
		}

		private static void PlantPumpkin(HomesteadSim sim, int index)
		{
			sim.Give("seed_pumpkin", 1);
			sim.Till(index);
			Assert.That(sim.Plant(index, "pumpkin").Ok);
			Assert.That(sim.Water(index).Ok);
		}

		[Test]
		public void Ripen_spell_is_in_the_data_and_unknown_at_start()
		{
			HomesteadSim sim = MakeSim();
			Assert.That(sim.Catalog.Spells.Exists(spell => spell.Id == RIPEN));
			Assert.That(sim.IsSpellKnown(RIPEN), Is.False);
		}

		[Test]
		public void Research_takes_its_goods_once()
		{
			HomesteadSim sim = LearnedSim();
			Assert.That(sim.IsSpellKnown(RIPEN));
			foreach (string goods in sim.Catalog.Get<SpellDef>(RIPEN).Research.Keys)
			{
				Assert.That(sim.Count(goods), Is.EqualTo(0), goods);
			}
			Assert.That(sim.Research(RIPEN).Failure, Is.EqualTo(HomesteadFailure.AlreadyDone));
		}

		[Test]
		public void Research_without_goods_refuses_and_changes_nothing()
		{
			HomesteadSim sim = MakeSim();
			HomesteadResult result = sim.Research(RIPEN);
			Assert.That(result.Failure, Is.EqualTo(HomesteadFailure.NotEnoughGoods));
			Assert.That(sim.IsSpellKnown(RIPEN), Is.False);
		}

		[Test]
		public void Ripen_makes_one_growing_plot_ripe_and_takes_the_cast_goods()
		{
			HomesteadSim sim = LearnedSim();
			PlantPumpkin(sim, 0);
			GiveAll(sim, sim.Catalog.Get<SpellDef>(RIPEN).Cast);
			Assert.That(sim.Ripen(0).Ok);
			Assert.That(sim.PhaseOf(sim.State.Plots[0]), Is.EqualTo(PlotPhase.Ripe));
			foreach (string goods in sim.Catalog.Get<SpellDef>(RIPEN).Cast.Keys)
			{
				Assert.That(sim.Count(goods), Is.EqualTo(0), goods);
			}
			Assert.That(sim.Harvest(0, 0).Ok);
			Assert.That(sim.Count("pumpkin"), Is.GreaterThan(0));
		}

		[Test]
		public void Ripen_touches_only_its_plot_and_not_wetness()
		{
			HomesteadSim sim = LearnedSim();
			PlantPumpkin(sim, 0);
			PlantPumpkin(sim, 1);
			GiveAll(sim, sim.Catalog.Get<SpellDef>(RIPEN).Cast);
			long wetBefore = sim.State.Plots[0].WetUntil;
			sim.Ripen(0);
			Assert.That(sim.PhaseOf(sim.State.Plots[1]), Is.EqualTo(PlotPhase.Growing), "옆 칸은 그대로 (물뿌리개 등급의 범위와 다름)");
			Assert.That(sim.State.Plots[0].WetUntil, Is.EqualTo(wetBefore), "젖음은 물 주기 몫");
		}

		[Test]
		public void Ripen_needs_a_watered_plot()
		{
			HomesteadSim sim = LearnedSim();
			sim.Give("seed_pumpkin", 1);
			sim.Till(0);
			sim.Plant(0, "pumpkin");
			GiveAll(sim, sim.Catalog.Get<SpellDef>(RIPEN).Cast);
			Assert.That(sim.Ripen(0).Failure, Is.EqualTo(HomesteadFailure.NotReady), "마른 밭은 물 주기가 먼저");
			sim.Water(0);
			Assert.That(sim.Ripen(0).Ok);
		}

		[Test]
		public void Catalog_check_refuses_a_zero_cast_amount()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			catalog.Spells[0].Cast["moonmoss"] = 0;
			Assert.That(HomesteadCatalogCheck.Run(catalog), Has.Some.Contains("쓰는 값이 없음"));
		}

		[Test]
		public void Ripen_needs_a_growing_plot()
		{
			HomesteadSim sim = LearnedSim();
			GiveAll(sim, sim.Catalog.Get<SpellDef>(RIPEN).Cast);
			Assert.That(sim.Ripen(0).Failure, Is.EqualTo(HomesteadFailure.NotReady), "빈 밭");
			PlantPumpkin(sim, 0);
			sim.Ripen(0);
			GiveAll(sim, sim.Catalog.Get<SpellDef>(RIPEN).Cast);
			Assert.That(sim.Ripen(0).Failure, Is.EqualTo(HomesteadFailure.NotReady), "이미 익은 밭");
		}

		[Test]
		public void Ripen_before_research_or_without_goods_refuses()
		{
			HomesteadSim sim = MakeSim();
			PlantPumpkin(sim, 0);
			GiveAll(sim, sim.Catalog.Get<SpellDef>(RIPEN).Cast);
			Assert.That(sim.Ripen(0).Failure, Is.EqualTo(HomesteadFailure.Locked));

			HomesteadSim learned = LearnedSim();
			PlantPumpkin(learned, 0);
			Assert.That(learned.Ripen(0).Failure, Is.EqualTo(HomesteadFailure.NotEnoughGoods));
			Assert.That(learned.PhaseOf(learned.State.Plots[0]), Is.EqualTo(PlotPhase.Growing));
		}

		[Test]
		public void Every_spell_costs_something_to_cast()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			Assert.That(HomesteadCatalogCheck.Run(catalog), Is.Empty);
			catalog.Spells[0].Cast.Clear();
			Assert.That(HomesteadCatalogCheck.Run(catalog), Has.Some.Contains("쓰는 값이 없음"));
		}

		[Test]
		public void Unknown_spell_is_forgotten_on_load()
		{
			HomesteadState state = new();
			state.Spells.Add("없는 주문");
			state.Spells.Add(RIPEN);
			HomesteadRepair.DropUnknown(state, HomesteadTestKit.LoadCatalog());
			Assert.That(state.Spells, Is.EqualTo(new[] { RIPEN }));
		}
	}
}
