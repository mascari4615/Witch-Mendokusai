using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 던전 구출과 오는 길 열쇠 점검
	public class HomesteadRescueTest
	{
		private const string ATELIER_RESCUE = "rescue_atelier";
		private const string ATELIER_RESIDENT = "r_atelier";

		[Test]
		public void Rescue_brings_the_resident_and_stops_being_wanted()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			Assert.That(sim.WantedRescues(), Does.Contain(ATELIER_RESCUE));
			Assert.That(sim.HasArrived(ATELIER_RESIDENT), Is.False);

			Assert.That(sim.NotifyRescue(ATELIER_RESCUE).Ok);

			Assert.That(sim.HasArrived(ATELIER_RESIDENT));
			Assert.That(sim.WantedRescues(), Does.Not.Contain(ATELIER_RESCUE));
		}

		[Test]
		public void Second_rescue_and_unknown_key_are_refused()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.NotifyRescue(ATELIER_RESCUE);

			HomesteadResult again = sim.NotifyRescue(ATELIER_RESCUE);
			Assert.That(again.Failure, Is.EqualTo(HomesteadFailure.AlreadyDone));
			Assert.That(sim.NotifyRescue("rescue_nobody").Ok, Is.False);
			Assert.That(sim.EventCount("rescue_nobody"), Is.Zero);
		}

		// 부탁으로 먼저 왔으면 던전에 갇힌 사람으로 나오지 않음
		[Test]
		public void Resident_who_came_another_way_is_not_wanted()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse);
			RecruitDef favor = sim.Catalog.Get<ResidentDef>(ATELIER_RESIDENT).Recruits.Find(recruit => recruit.Kind == RecruitKind.Favor);
			HomesteadTestKit.Afford(sim, purse, favor.Cost);
			Assert.That(sim.PayFavor(ATELIER_RESIDENT, favor).Ok);

			Assert.That(sim.WantedRescues(), Does.Not.Contain(ATELIER_RESCUE));
		}

		[Test]
		public void Every_rescue_resident_keeps_a_way_without_the_dungeon()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			foreach (ResidentDef resident in catalog.Residents)
			{
				if (resident.Recruits.Exists(recruit => recruit.Kind == RecruitKind.Rescue))
				{
					Assert.That(resident.Recruits.Exists(recruit => recruit.Kind != RecruitKind.Rescue && recruit.Kind != RecruitKind.GuildLevel), resident.Id);
				}
			}
		}

		[TestCase("atelier_rescue")]
		[TestCase("rescue_")]
		public void Catalog_check_blocks_a_rescue_key_without_prefix(string key)
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			RescueOf(catalog).Key = key;
			Assert.That(HomesteadCatalogCheck.Run(catalog), Has.Some.Contains(ATELIER_RESIDENT));
		}

		[Test]
		public void Catalog_check_blocks_a_shared_rescue_key()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			catalog.Get<ResidentDef>("r_trial").Recruits.Add(new RecruitDef { Kind = RecruitKind.Rescue, Key = ATELIER_RESCUE, Count = 1 });
			List<string> problems = HomesteadCatalogCheck.Run(catalog);
			Assert.That(problems, Has.Some.Contains("겹침"), string.Join("\n", problems));
		}

		[Test]
		public void Catalog_check_blocks_an_event_nobody_announces()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			catalog.Get<ResidentDef>("r_smithy").Recruits.Find(recruit => recruit.Kind == RecruitKind.Event).Key = "dragon";
			Assert.That(HomesteadCatalogCheck.Run(catalog), Has.Some.Contains("dragon"));
		}

		[Test]
		public void Catalog_check_blocks_a_build_way_to_a_missing_building()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			catalog.Get<ResidentDef>("r_shop").Recruits.Find(recruit => recruit.Kind == RecruitKind.Build).Key = "없는 건물";
			Assert.That(HomesteadCatalogCheck.Run(catalog), Has.Some.Contains("없는 건물"));
		}

		private static RecruitDef RescueOf(HomesteadCatalog catalog) =>
			catalog.Get<ResidentDef>(ATELIER_RESIDENT).Recruits.Find(recruit => recruit.Kind == RecruitKind.Rescue);
	}
}
