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
	}
}
