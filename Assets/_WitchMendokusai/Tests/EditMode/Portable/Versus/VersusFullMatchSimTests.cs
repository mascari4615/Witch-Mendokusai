using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 대결 축 v0 — 한 판이 <b>반드시 끝나나</b> (TASK-WM-411). 즉사·무승부·카드 뽑기가 얽히면
	/// 「아무도 못 이기는 상태」가 나기 쉽다. 사람 둘을 앉히기 전에 그 교착을 기계가 먼저 판정한다.
	/// </summary>
	public sealed class VersusFullMatchSimTests
	{
		private const int MAX_ROUNDS = 400; // 이 안에 안 끝나면 교착 — 즉시 실패로 잡는다.

		[Test]
		public void 어떤_승패_순서로도_매치가_끝난다()
		{
			for (int seed = 1; seed <= 200; seed++)
			{
				VersusMatchCore match = new VersusMatchCore(VersusRules.Default(), seed);
				VersusRandom flow = new VersusRandom(seed * 31 + 7);
				int rounds = 0;

				while (match.IsConcluded == false && rounds < MAX_ROUNDS)
				{
					rounds++;

					// 0/1 = 각자 승, 2 = 동시사(무승부). 즉사제에서 실제로 나오는 세 결과 전부를 섞는다.
					int roll = flow.NextInt(3);
					match.ResolveRound(roll == 2 ? VersusMatchCore.NO_WINNER : roll);

					if (match.DraftingPlayerIndex != VersusMatchCore.NO_WINNER)
						Assert.IsTrue(match.TakeOffered(flow.NextInt(match.PendingOffer.Count)), "seed " + seed);
				}

				Assert.IsTrue(match.IsConcluded, "seed " + seed + " 가 " + MAX_ROUNDS + " 라운드 안에 안 끝났다");
				Assert.AreEqual(VersusRules.Default().RoundsToWin, match.ScoreOf(match.WinnerIndex), "seed " + seed);
			}
		}

		[Test]
		public void 진_쪽이_항상_더_두껍다()
		{
			// 「지는 쪽이 카드를 받는다」가 실제로 러버밴딩이 되는지 — 한 쪽이 계속 이기는 최악의 판을 돌려본다.
			VersusMatchCore match = new VersusMatchCore(VersusRules.Default(), 20260816);

			while (match.IsConcluded == false)
			{
				match.ResolveRound(0);

				if (match.DraftingPlayerIndex != VersusMatchCore.NO_WINNER)
					match.TakeOffered(0);
			}

			Assert.AreEqual(0, match.CardsOf(0).Count, "이긴 쪽은 카드를 못 받는다");
			Assert.AreEqual(VersusRules.Default().RoundsToWin - 1, match.CardsOf(1).Count);
		}

		[Test]
		public void 무승부만_이어져도_점수는_안_움직인다()
		{
			VersusMatchCore match = new VersusMatchCore(VersusRules.Default(), 99);

			for (int round = 0; round < 50; round++)
				match.ResolveRound(VersusMatchCore.NO_WINNER);

			Assert.IsFalse(match.IsConcluded);
			Assert.AreEqual(0, match.ScoreOf(0));
			Assert.AreEqual(0, match.ScoreOf(1));
			Assert.IsTrue(match.CanStartNextRound);
		}
	}
}
