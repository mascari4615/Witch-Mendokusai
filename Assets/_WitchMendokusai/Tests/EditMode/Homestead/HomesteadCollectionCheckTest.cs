using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 수집 쪽 자료 점검. 관에 걸 물건이 실제로 얻어지는지, 이정표가 받침대 수 안인지
	public class HomesteadCollectionCheckTest
	{
		private const int DEMO_TOWN_HALL = 3;

		[Test]
		public void First_town_hall_follows_the_gate_of_the_easiest_source()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			Assert.That(HomesteadCatalogCheck.FirstTownHall(catalog, "turnip"), Is.EqualTo(1), "첫 작물");
			Assert.That(HomesteadCatalogCheck.FirstTownHall(catalog, "strawberry"), Is.EqualTo(3), "작물의 타운홀");
			Assert.That(HomesteadCatalogCheck.FirstTownHall(catalog, "copper"), Is.EqualTo(1), "바위 채집과 갱도 중 쉬운 쪽");
			Assert.That(HomesteadCatalogCheck.FirstTownHall(catalog, "rainbow"), Is.EqualTo(3), "곡괭이 등급의 타운홀");
			Assert.That(HomesteadCatalogCheck.FirstTownHall(catalog, "mistking"), Is.EqualTo(3), "낚싯대 등급의 타운홀");
			Assert.That(HomesteadCatalogCheck.FirstTownHall(catalog, "thimble"), Is.EqualTo(catalog.Get<BuildingDef>(HomesteadSim.MUSEUM).TownHall), "유물은 박물관이 서야 손질");
		}

		[Test]
		public void Every_exhibit_opens_by_the_last_town_hall()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			List<string> late = new();
			foreach (WingDef wing in catalog.Wings)
			{
				foreach (GoodsDef goods in catalog.Goods.Where(candidate => HomesteadSim.Accepts(wing, candidate)))
				{
					int townHall = HomesteadCatalogCheck.FirstTownHall(catalog, goods.Id);
					Assert.That(townHall, Is.LessThanOrEqualTo(catalog.TownHalls.Count), goods.Id);
					if (townHall > DEMO_TOWN_HALL)
					{
						late.Add($"{wing.Name} {goods.Name} (타운홀 {townHall})");
					}
				}
			}
			TestContext.WriteLine($"데모 끝 (타운홀 {DEMO_TOWN_HALL}) 에 못 얻는 전시품: {(late.Count == 0 ? "없음" : string.Join(", ", late))}");
			// 확정 f05-1. 데모 안에서 관마다 끝까지 모을 수 있음
			Assert.That(late, Is.Empty, "데모 끝에 못 얻는 전시품");
		}

		[Test]
		public void Check_flags_a_milestone_beyond_the_pedestals()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			WingDef wing = catalog.Wings[0];
			int pedestals = catalog.Goods.Count(candidate => HomesteadSim.Accepts(wing, candidate));
			wing.Milestones.Add(new MilestoneDef { Count = pedestals + 1, Reward = new RewardDef() });
			Assert.That(HomesteadCatalogCheck.Run(catalog), Has.Some.Contains(wing.Id).And.Contains("받침대"));
		}

		[Test]
		public void Check_flags_milestones_out_of_order()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			WingDef wing = catalog.Wings[0];
			wing.Milestones.Insert(1, new MilestoneDef { Count = wing.Milestones[0].Count, Reward = new RewardDef() });
			Assert.That(HomesteadCatalogCheck.Run(catalog), Has.Some.Contains(wing.Id).And.Contains("이정표"));
		}

		[Test]
		public void Check_flags_an_exhibit_whose_only_rock_lies_in_no_band()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			foreach (MineBandDef band in catalog.MineBands)
			{
				band.Weights.RemoveAll(weight => weight.Node == "n_rainbow");
			}
			Assert.That(HomesteadCatalogCheck.Run(catalog), Has.Some.Contains("rainbow").And.Contains("얻을 길"));
		}

		[Test]
		public void Check_flags_an_exhibit_behind_a_rod_that_does_not_exist()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			catalog.Get<FishDef>("mistking").RodTier = catalog.Tool(ToolKind.Rod).Tiers.Count;
			Assert.That(HomesteadCatalogCheck.Run(catalog), Has.Some.Contains("mistking").And.Contains("얻을 길"));
		}
	}
}
