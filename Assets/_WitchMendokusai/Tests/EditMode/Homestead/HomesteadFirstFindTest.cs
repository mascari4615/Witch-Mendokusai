using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 확정 f05-2 (못 본 물고기 가중) 와 f05-3 (처음 얻은 전시품은 큰 알림 한 장)
	public class HomesteadFirstFindTest
	{
		private static int Picks(HomesteadSim sim, string fishId, int tries)
		{
			int count = 0;
			for (int i = 0; i < tries; i++)
			{
				if (sim.PickFish("stream", 5, 0).Id == fishId)
				{
					count++;
				}
			}
			return count;
		}

		[Test]
		public void Unseen_fish_bite_more_often_than_known_ones()
		{
			HomesteadSim unseen = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _, 3);
			HomesteadSim known = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _, 3);
			unseen.State.Tools[ToolKind.Rod] = 3;
			known.State.Tools[ToolKind.Rod] = 3;
			known.State.Known.Add("sweetfish");
			int unseenPicks = Picks(unseen, "sweetfish", 20000);
			int knownPicks = Picks(known, "sweetfish", 20000);
			TestContext.WriteLine($"은어 못 봄 {unseenPicks}, 앎 {knownPicks}");
			// 다른 종은 모두 못 본 상태라 은어만 알면 은어 몫이 반쯤으로 줄어듦
			Assert.That(unseenPicks / (float)knownPicks, Is.InRange(1.7f, 2.3f));
		}

		[Test]
		public void A_new_exhibit_makes_one_card_and_a_second_one_none()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			HomesteadFirstFinds finds = new(sim);
			sim.Gained += gain => finds.Add(gain);
			sim.Give("minnow", 1);
			Assert.That(finds.HasPending, Is.True);
			FirstFindCard card = finds.Take();
			Assert.That(card.Goods.Select(goods => goods.Id), Is.EqualTo(new[] { "minnow" }));
			Assert.That(card.Title, Is.EqualTo(sim.Catalog.Get<GoodsDef>("minnow").Name));
			WingDef wing = HomesteadFirstFinds.WingOf(sim, "minnow");
			Assert.That(card.Text, Does.Contain(wing.Name).And.Contain($"1 / {sim.ExhibitsOf(wing).Count}"));
			sim.Give("minnow", 1);
			Assert.That(finds.HasPending, Is.False, "두 번째는 카드 없음");
		}

		[Test]
		public void New_exhibits_at_the_same_moment_share_one_card()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			HomesteadFirstFinds finds = new(sim);
			sim.Gained += gain => finds.Add(gain);
			sim.Give("copper", 1);
			sim.Give("quartz", 1);
			FirstFindCard card = finds.Take();
			Assert.That(card.Goods.Count, Is.EqualTo(2));
			Assert.That(card.Title, Does.Contain("2"));
			Assert.That(finds.HasPending, Is.False, "가져가면 비어 있음");
		}

		[Test]
		public void Goods_that_never_hang_in_a_wing_make_no_card()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			HomesteadFirstFinds finds = new(sim);
			GoodsDef other = sim.Catalog.Goods.First(goods => sim.CanExhibit(goods.Id) == false);
			Assert.That(finds.Add(new HomesteadGain(other.Id, 1, true)), Is.False, other.Id);
			Assert.That(finds.HasPending, Is.False);
		}
	}
}
