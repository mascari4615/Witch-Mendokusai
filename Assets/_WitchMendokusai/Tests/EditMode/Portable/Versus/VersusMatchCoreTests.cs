using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 대결 축 v0 매치 규칙 (TASK-WM-411). 「진 쪽만 카드를 뽑는다」가 이 축의 심장이라
	/// 그 한 줄이 실제로 참인지를 엔진 없이 매번 증명한다.
	/// </summary>
	public sealed class VersusMatchCoreTests
	{
		private static VersusMatchCore NewMatch(int seed = 1234)
		{
			return new VersusMatchCore(VersusRules.Default(), seed);
		}

		[Test]
		public void 라운드를_이기면_점수가_오른다()
		{
			VersusMatchCore match = NewMatch();

			match.ResolveRound(0);

			Assert.AreEqual(1, match.ScoreOf(0));
			Assert.AreEqual(0, match.ScoreOf(1));
		}

		[Test]
		public void 카드는_진_쪽에게만_내민다()
		{
			VersusMatchCore match = NewMatch();

			match.ResolveRound(0);

			Assert.AreEqual(1, match.DraftingPlayerIndex);
			Assert.AreEqual(VersusRules.Default().CardsOfferedToLoser, match.PendingOffer.Count);
		}

		[Test]
		public void 카드를_고르기_전에는_다음_라운드를_못_시작한다()
		{
			VersusMatchCore match = NewMatch();

			match.ResolveRound(0);
			Assert.IsFalse(match.CanStartNextRound);

			match.TakeOffered(0);
			Assert.IsTrue(match.CanStartNextRound);
		}

		[Test]
		public void 고른_카드는_그_사람_스탯에만_얹힌다()
		{
			VersusMatchCore match = NewMatch();
			VersusFighterStats winnerBefore = match.StatsOf(0);

			match.ResolveRound(0);
			match.TakeOffered(0);

			Assert.AreEqual(1, match.CardsOf(1).Count);
			Assert.AreEqual(0, match.CardsOf(0).Count);
			Assert.AreEqual(winnerBefore.MoveSpeed, match.StatsOf(0).MoveSpeed);
		}

		[Test]
		public void 무승부는_점수도_카드도_없다()
		{
			VersusMatchCore match = NewMatch();
			int roundBefore = match.RoundNumber;

			match.ResolveRound(VersusMatchCore.NO_WINNER);

			Assert.AreEqual(0, match.ScoreOf(0));
			Assert.AreEqual(0, match.ScoreOf(1));
			Assert.AreEqual(VersusMatchCore.NO_WINNER, match.DraftingPlayerIndex);
			Assert.AreEqual(roundBefore + 1, match.RoundNumber);
		}

		[Test]
		public void 선취점에_닿으면_매치가_끝나고_카드도_안_준다()
		{
			VersusMatchCore match = NewMatch();
			int roundsToWin = VersusRules.Default().RoundsToWin;

			for (int round = 0; round < roundsToWin; round++)
			{
				match.ResolveRound(0);
				match.TakeOffered(0);
			}

			Assert.IsTrue(match.IsConcluded);
			Assert.AreEqual(0, match.WinnerIndex);
			Assert.AreEqual(VersusMatchCore.NO_WINNER, match.DraftingPlayerIndex);
			Assert.AreEqual(roundsToWin - 1, match.CardsOf(1).Count);
		}

		[Test]
		public void 매치가_끝난_뒤의_라운드_결과는_무시된다()
		{
			VersusMatchCore match = NewMatch();
			for (int round = 0; round < VersusRules.Default().RoundsToWin; round++)
			{
				match.ResolveRound(0);
				match.TakeOffered(0);
			}

			match.ResolveRound(1);

			Assert.AreEqual(0, match.ScoreOf(1));
			Assert.AreEqual(0, match.WinnerIndex);
		}

		[Test]
		public void 없는_후보를_고르면_아무_일도_안_일어난다()
		{
			VersusMatchCore match = NewMatch();
			match.ResolveRound(0);

			Assert.IsFalse(match.TakeOffered(99));
			Assert.AreEqual(1, match.DraftingPlayerIndex);
			Assert.AreEqual(0, match.CardsOf(1).Count);
		}

		[Test]
		public void 같은_씨앗이면_뽑기가_같다()
		{
			VersusMatchCore first = NewMatch(777);
			VersusMatchCore second = NewMatch(777);

			first.ResolveRound(0);
			second.ResolveRound(0);

			CollectionAssert.AreEqual(first.PendingOffer, second.PendingOffer);
		}

		[Test]
		public void 후보는_서로_다른_카드다()
		{
			for (int seed = 1; seed <= 50; seed++)
			{
				VersusMatchCore match = NewMatch(seed);
				match.ResolveRound(0);

				List<VersusCardKind> offer = new List<VersusCardKind>(match.PendingOffer);
				CollectionAssert.AllItemsAreUnique(offer, "seed " + seed);
			}
		}
	}
}
