using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 귀환 소식 ("그동안 마을에서는") 은 지난번 보인 뒤 새 소식이 생겼을 때만
	public class HomesteadDigestTest
	{
		private static HomesteadSim MakeSimWithRipeTurnip()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.Give("seed_turnip", 2);
			sim.Till(0);
			sim.Plant(0, "turnip");
			sim.AdvanceMinutes(240);
			return sim;
		}

		[Test]
		public void First_take_shows_the_news()
		{
			HomesteadSim sim = MakeSimWithRipeTurnip();
			HomesteadDigest digest = sim.TakeNewDigest();
			Assert.That(digest.IsEmpty, Is.False);
			Assert.That(digest.RipePlots, Is.EqualTo(1));
		}

		[Test]
		public void Same_news_is_not_shown_twice()
		{
			HomesteadSim sim = MakeSimWithRipeTurnip();
			sim.TakeNewDigest();
			Assert.That(sim.TakeNewDigest().IsEmpty, Is.True, "마을이나 박물관을 오가기만 하면 다시 뜨지 않음");
			sim.AdvanceMinutes(30);
			Assert.That(sim.TakeNewDigest().IsEmpty, Is.True, "시간만 흐르고 새로 생긴 것이 없음");
		}

		[Test]
		public void New_news_shows_again()
		{
			HomesteadSim sim = MakeSimWithRipeTurnip();
			sim.TakeNewDigest();
			sim.Till(1);
			sim.Plant(1, "turnip");
			sim.AdvanceMinutes(240);
			HomesteadDigest digest = sim.TakeNewDigest();
			Assert.That(digest.IsEmpty, Is.False);
			Assert.That(digest.RipePlots, Is.EqualTo(2), "보일 때는 지금 상태 전부");
		}

		[Test]
		public void Fewer_news_is_not_new()
		{
			HomesteadSim sim = MakeSimWithRipeTurnip();
			sim.TakeNewDigest();
			Assert.That(sim.Harvest(0, 3).Ok);
			Assert.That(sim.TakeNewDigest().IsEmpty, Is.True, "거둬서 줄어든 것은 새 소식이 아님");
		}

		// ---- 거둔 뒤 같은 칸이 다시 차는 경우. 수만 세면 지난번과 같은 수라 못 알아봄

		[Test]
		public void Ripe_again_after_harvest_shows()
		{
			HomesteadSim sim = MakeSimWithRipeTurnip();
			sim.TakeNewDigest();
			Assert.That(sim.Harvest(0, 3).Ok);
			sim.Till(1);
			sim.Plant(1, "turnip");
			sim.AdvanceMinutes(240);
			HomesteadDigest digest = sim.TakeNewDigest();
			Assert.That(digest.RipePlots, Is.EqualTo(1), "밭에서 거둔 뒤 다른 밭이 익으면 새 소식");
			Assert.That(sim.TakeNewDigest().IsEmpty, Is.True, "같은 상태로 또 들어서면 안 뜸");
		}

		[Test]
		public void Craft_done_again_after_collect_shows()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse);
			HomesteadTestKit.BuildNow(sim, purse, HomesteadSim.WORKSHOP);
			sim.TakeNewDigest();
			sim.Give("stone", 8);
			sim.Give("coal", 2);
			Assert.That(sim.StartCraft("c_brick").Ok);
			sim.AdvanceMinutes(60);
			Assert.That(sim.TakeNewDigest().DoneCrafts, Is.EqualTo(1), "공작소 완료");
			Assert.That(sim.TakeNewDigest().IsEmpty, Is.True);
			Assert.That(sim.CollectCrafts(), Is.EqualTo(1));
			Assert.That(sim.StartCraft("c_brick").Ok);
			sim.AdvanceMinutes(60);
			Assert.That(sim.TakeNewDigest().DoneCrafts, Is.EqualTo(1), "거둔 뒤 다시 완료");
			Assert.That(sim.TakeNewDigest().IsEmpty, Is.True);
		}

		[Test]
		public void Animal_product_again_after_care_shows()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse);
			HomesteadTestKit.BuildNow(sim, purse, HomesteadSim.RANCH);
			purse.Nyang += 1000;
			Assert.That(sim.BuyAnimal("hen").Ok);
			sim.TakeNewDigest();
			sim.AdvanceMinutes(480);
			Assert.That(sim.TakeNewDigest().AnimalProducts, Is.EqualTo(1));
			Assert.That(sim.TakeNewDigest().IsEmpty, Is.True);
			Assert.That(sim.Care(sim.State.Animals[0].Uid, out bool collected).Ok);
			Assert.That(collected);
			sim.AdvanceMinutes(480);
			Assert.That(sim.TakeNewDigest().AnimalProducts, Is.EqualTo(1), "거둔 뒤 다시 낸 것");
			Assert.That(sim.TakeNewDigest().IsEmpty, Is.True);
		}

		[Test]
		public void Job_yield_again_after_collect_shows()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse);
			HomesteadTestKit.BuildNow(sim, purse, HomesteadSim.WORKSHOP);
			Assert.That(sim.Assign("j_wood", 4).Ok);
			sim.TakeNewDigest();
			sim.AdvanceMinutes(100000);
			Assert.That(sim.TakeNewDigest().JobYields, Is.GreaterThan(0));
			Assert.That(sim.TakeNewDigest().IsEmpty, Is.True);
			Assert.That(sim.CollectJob("j_wood"), Is.GreaterThan(0));
			sim.AdvanceMinutes(100000);
			Assert.That(sim.TakeNewDigest().JobYields, Is.GreaterThan(0), "거둔 뒤 다시 쌓인 일감");
			Assert.That(sim.TakeNewDigest().IsEmpty, Is.True);
		}

		[Test]
		public void New_resident_shows_once()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.TakeNewDigest();
			sim.ResidentOf("r_shop").Arrived = true;
			Assert.That(sim.TakeNewDigest().NewResidents, Does.Contain("r_shop"));
			Assert.That(sim.TakeNewDigest().IsEmpty, Is.True, "인사 전이라도 같은 주민은 한 번만");
			sim.Greet("r_shop");
			sim.ResidentOf("r_smithy").Arrived = true;
			Assert.That(sim.TakeNewDigest().NewResidents, Is.EquivalentTo(new[] { "r_smithy" }));
		}

		[Test]
		public void Deliverable_request_again_after_deliver_shows()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.RefreshRequests();
			sim.TakeNewDigest();
			RequestState first = sim.State.Requests[0];
			sim.Give(first.Goods, first.Amount);
			Assert.That(sim.TakeNewDigest().DeliverableRequests, Is.EqualTo(1), "낼 수 있는 의뢰");
			Assert.That(sim.TakeNewDigest().IsEmpty, Is.True);
			Assert.That(sim.Deliver(first.Uid).Ok);
			RequestState second = sim.State.Requests[0];
			sim.Give(second.Goods, second.Amount);
			Assert.That(sim.TakeNewDigest().DeliverableRequests, Is.EqualTo(1), "낸 뒤 다른 의뢰를 낼 수 있게 됨");
			Assert.That(sim.TakeNewDigest().IsEmpty, Is.True);
		}
	}
}
