using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 사역마. 알 길 셋, 깨우기, 데려갈 하나, 하루 한 번 쓰다듬기, 던전 판마다 남는 줍기 반경
	public class HomesteadFamiliarTest
	{
		private const string FIRST = "fm_moss";
		private const string SECOND = "fm_ember";
		private const string THIRD = "fm_star";

		private static HomesteadSim MakeSim() => HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);

		[Test]
		public void Three_familiars_and_no_egg_at_start()
		{
			HomesteadSim sim = MakeSim();
			Assert.That(sim.Catalog.Familiars.Count, Is.EqualTo(3));
			Assert.That(sim.NextEgg(), Is.Null);
			Assert.That(sim.FamiliarForRun(), Is.Null);
		}

		[Test]
		public void First_dungeon_return_brings_the_first_egg()
		{
			HomesteadSim sim = MakeSim();
			sim.NotifyExpedition();
			Assert.That(sim.NextEgg().Id, Is.EqualTo(FIRST));
		}

		[Test]
		public void Hatching_makes_it_the_one_taken_along()
		{
			HomesteadSim sim = MakeSim();
			sim.NotifyExpedition();
			Assert.That(sim.Hatch(FIRST).Ok);
			Assert.That(sim.IsHatched(FIRST));
			Assert.That(sim.FamiliarForRun().Id, Is.EqualTo(FIRST));
			Assert.That(sim.NextEgg(), Is.Null);
		}

		[Test]
		public void Cannot_hatch_before_the_egg_or_twice()
		{
			HomesteadSim sim = MakeSim();
			Assert.That(sim.Hatch(FIRST).Failure, Is.EqualTo(HomesteadFailure.NotReady));
			sim.NotifyExpedition();
			sim.Hatch(FIRST);
			Assert.That(sim.Hatch(FIRST).Failure, Is.EqualTo(HomesteadFailure.AlreadyDone));
		}

		[Test]
		public void Boss_brings_the_second_egg_and_the_first_stays_along()
		{
			HomesteadSim sim = MakeSim();
			sim.NotifyExpedition();
			sim.Hatch(FIRST);
			sim.NotifyEvent(HomesteadSim.BOSS_EVENT);
			Assert.That(sim.NextEgg().Id, Is.EqualTo(SECOND));
			sim.Hatch(SECOND);
			Assert.That(sim.FamiliarForRun().Id, Is.EqualTo(FIRST));
			Assert.That(sim.TakeAlong(SECOND).Ok);
			Assert.That(sim.FamiliarForRun().Id, Is.EqualTo(SECOND));
		}

		[Test]
		public void Third_egg_comes_by_guild_level_or_by_bosses()
		{
			HomesteadSim byGuild = MakeSim();
			FamiliarDef third = byGuild.Catalog.Get<FamiliarDef>(THIRD);
			byGuild.State.GuildExp = byGuild.Catalog.Tuning.GuildSteps[third.GuildLevel - 1];
			Assert.That(byGuild.IsEggHere(third));

			HomesteadSim byBoss = MakeSim();
			for (int i = 0; i < third.EventCount; i++)
			{
				Assert.That(byBoss.IsEggHere(third), Is.False);
				byBoss.NotifyEvent(HomesteadSim.BOSS_EVENT);
			}
			Assert.That(byBoss.IsEggHere(third));
		}

		[Test]
		public void Cannot_take_along_one_not_hatched()
		{
			HomesteadSim sim = MakeSim();
			Assert.That(sim.TakeAlong(SECOND).Failure, Is.EqualTo(HomesteadFailure.NotOwned));
		}

		[Test]
		public void Taken_along_familiar_stays_for_every_run()
		{
			// 식탁 음식은 먹으면 비지만 사역마는 다음 판에도 그대로
			HomesteadSim sim = MakeSim();
			sim.NotifyExpedition();
			sim.Hatch(FIRST);
			for (int run = 0; run < 3; run++)
			{
				Assert.That(sim.FamiliarForRun().Pickup, Is.EqualTo(sim.Catalog.Get<FamiliarDef>(FIRST).Pickup));
				sim.NotifyExpedition();
			}
		}

		[Test]
		public void Petting_is_once_a_day_and_changes_nothing_else()
		{
			HomesteadSim sim = MakeSim();
			sim.NotifyExpedition();
			sim.Hatch(FIRST);
			int storeBefore = sim.State.Store.Count;
			Assert.That(sim.PetFamiliar(FIRST).Ok);
			Assert.That(sim.PetFamiliar(FIRST).Failure, Is.EqualTo(HomesteadFailure.DailyLimit));
			Assert.That(sim.State.Store.Count, Is.EqualTo(storeBefore));
			sim.AdvanceMinutes(sim.Catalog.Tuning.MinutesPerDay);
			Assert.That(sim.CanPetFamiliar(FIRST));
		}

		[Test]
		public void Not_petting_never_takes_anything_away()
		{
			HomesteadSim sim = MakeSim();
			sim.NotifyExpedition();
			sim.Hatch(FIRST);
			sim.AdvanceMinutes(sim.Catalog.Tuning.MinutesPerDay * 30L);
			Assert.That(sim.IsHatched(FIRST));
			Assert.That(sim.FamiliarForRun().Id, Is.EqualTo(FIRST));
		}

		[Test]
		public void Unknown_familiar_in_save_is_dropped()
		{
			HomesteadState state = new() { FamiliarWith = "없는 사역마" };
			state.Familiars.Add(new FamiliarState { Id = "없는 사역마" });
			HomesteadRepair.DropUnknown(state, HomesteadTestKit.LoadCatalog());
			Assert.That(state.Familiars, Is.Empty);
			Assert.That(state.FamiliarWith, Is.Empty);
		}

		[Test]
		public void Familiar_data_passes_the_catalog_check()
		{
			Assert.That(HomesteadCatalogCheck.Run(HomesteadTestKit.LoadCatalog()), Is.Empty);
		}
	}
}
