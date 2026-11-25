using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 집 제작대는 처음부터 바로, 그 밖은 마을 공작소와 상점
	public class HomesteadBenchTest
	{
		[Test]
		public void Bench_makes_material_at_once_without_a_workshop()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.Give("wood", 3);
			Assert.That(sim.StartCraft("c_plank").Ok);
			Assert.That(sim.Count("plank"), Is.EqualTo(2));
			Assert.That(sim.State.Crafts, Is.Empty);
		}

		[Test]
		public void Bench_furniture_needs_only_goods()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.Give("plank", 2);
			Assert.That(sim.MakeFurniture("wood_chair").Ok);
			Assert.That(sim.OwnedCount("wood_chair"), Is.EqualTo(1));
		}

		[Test]
		public void Harder_furniture_waits_for_the_workshop()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.Give("brick", 12);
			sim.Give("coal", 4);
			Assert.That(sim.MakeFurniture("fireplace").Failure, Is.EqualTo(HomesteadFailure.NotBuilt));
			sim.State.Buildings[HomesteadSim.WORKSHOP] = 2;
			Assert.That(sim.MakeFurniture("fireplace").Ok);
		}

		[Test]
		public void Shop_furniture_is_bought_with_money_once_the_shop_is_open()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse);
			int price = sim.Catalog.Get<FurnitureDef>("cushion").Cost.Nyang;
			Assert.That(sim.MakeFurniture("cushion").Failure, Is.EqualTo(HomesteadFailure.NotBuilt));
			sim.State.Buildings[HomesteadSim.SHOP] = 1;
			sim.State.Residents["r_shop"] = new ResidentState { Arrived = true };
			int before = purse.Nyang;
			Assert.That(sim.MakeFurniture("cushion").Ok);
			Assert.That(before - purse.Nyang, Is.EqualTo(price));
		}

		[Test]
		public void House_grows_without_the_guild_hall()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse);
			sim.TryGetNextHouseTier(out HouseTierDef next);
			HomesteadTestKit.Afford(sim, purse, next.Cost);
			Assert.That(sim.State.TownHall, Is.EqualTo(1));
			Assert.That(sim.ExpandHouse().Ok);
		}
	}
}
