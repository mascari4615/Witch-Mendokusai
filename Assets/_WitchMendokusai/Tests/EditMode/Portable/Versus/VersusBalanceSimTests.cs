using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 카드 밸런스를 <b>사람이 앉기 전에</b> 숫자로 본다 (TASK-WM-411).
	/// 둘 다 같은 규칙으로 움직이므로 결과를 가르는 것은 카드뿐 — 승률이 곧 그 카드의 세기다.
	/// 표는 <c>TestContext.WriteLine</c> 으로 찍는다(엔진 밖에서도 그대로 보인다).
	/// </summary>
	public sealed class VersusBalanceSimTests
	{
		private const int ROUNDS_PER_CARD = 600;

		private static float WinRateWithCard(VersusCardKind card, int seedBase)
		{
			VersusTuning tuning = VersusTuning.Default();
			VersusBotTuning botTuning = VersusBotTuning.Default();
			VersusRules rules = VersusRules.Default();

			VersusFighterStats carded = VersusCards.Apply(VersusFighterStats.Default(), card);
			VersusFighterStats plain = VersusFighterStats.Default();

			int wins = 0;
			int decided = 0;

			for (int round = 0; round < ROUNDS_PER_CARD; round++)
			{
				VersusRandom random = new VersusRandom(seedBase + round);

				// 자리(왼쪽/오른쪽)가 유리할 수 있으니 절반씩 바꿔 단다.
				bool cardOnLeft = (round % 2) == 0;
				int winner = cardOnLeft
					? VersusDuelSim.RunRound(carded, plain, tuning, botTuning, rules.RoundTimeLimitSeconds, ref random)
					: VersusDuelSim.RunRound(plain, carded, tuning, botTuning, rules.RoundTimeLimitSeconds, ref random);

				if (winner == VersusMatchCore.NO_WINNER)
					continue;

				decided++;
				bool cardWon = cardOnLeft ? winner == 0 : winner == 1;
				if (cardWon)
					wins++;
			}

			return decided == 0 ? 0.5f : (float)wins / decided;
		}

		[Test]
		public void 카드별_승률표를_찍는다()
		{
			TestContext.WriteLine("카드 1장 vs 맨몸 — 승률(무승부 제외), 판 " + ROUNDS_PER_CARD + "회");

			List<string> broken = new List<string>();

			foreach (VersusCardKind card in VersusCards.All)
			{
				float winRate = WinRateWithCard(card, 1000 + (int)card * 7919);
				TestContext.WriteLine(string.Format("  {0,-10} {1,6:P1}  {2}", card, winRate, VersusCards.Describe(card)));

				// 「한 장으로 판이 끝나는」 카드 = 뽑는 순간 게임이 죽는다. 여기서 잡는다.
				if (winRate > 0.85f || winRate < 0.15f)
					broken.Add(card + " " + winRate.ToString("P1"));
			}

			Assert.IsEmpty(broken, "한 장으로 판을 끝내는(또는 아무 쓸모 없는) 카드: " + string.Join(", ", broken));
		}

		[Test]
		public void 맨몸끼리는_승률이_반반이다()
		{
			// 시뮬 자체가 한쪽으로 기울어 있으면 위 표가 전부 거짓말이 된다 — 먼저 그것부터 잰다.
			VersusTuning tuning = VersusTuning.Default();
			VersusBotTuning botTuning = VersusBotTuning.Default();
			VersusFighterStats plain = VersusFighterStats.Default();

			int leftWins = 0;
			int decided = 0;
			int draws = 0;

			for (int round = 0; round < 1000; round++)
			{
				VersusRandom random = new VersusRandom(50000 + round);
				int winner = VersusDuelSim.RunRound(plain, plain, tuning, botTuning, 30f, ref random);

				if (winner == VersusMatchCore.NO_WINNER)
				{
					draws++;
					continue;
				}

				decided++;
				if (winner == 0)
					leftWins++;
			}

			float leftRate = decided == 0 ? 0f : (float)leftWins / decided;
			TestContext.WriteLine(string.Format("맨몸 vs 맨몸 — 왼쪽 승률 {0:P1} · 무승부 {1:P1} ({2}판)", leftRate, draws / 1000f, 1000));

			Assert.Greater(decided, 0, "1000판이 전부 무승부다 — 아무도 아무도 못 맞힌다는 뜻(시뮬이 고장)");
			Assert.That(leftRate, Is.InRange(0.35f, 0.65f), "자리만으로 승부가 갈린다");
		}

		[Test]
		public void 한_매치는_적당한_라운드_안에_끝난다()
		{
			VersusRules rules = VersusRules.Default();
			VersusTuning tuning = VersusTuning.Default();
			VersusBotTuning botTuning = VersusBotTuning.Default();

			int totalRounds = 0;
			int longest = 0;

			for (int match = 0; match < 200; match++)
			{
				(int winner, int rounds) result = VersusDuelSim.RunMatch(rules, tuning, botTuning, 900000 + match);
				totalRounds += result.rounds;
				if (result.rounds > longest)
					longest = result.rounds;

				Assert.AreNotEqual(VersusMatchCore.NO_WINNER, result.winner, "매치가 안 끝났다 (seed " + (900000 + match) + ")");
			}

			float average = totalRounds / 200f;
			TestContext.WriteLine(string.Format("5선승 한 매치 — 평균 {0:F1} 라운드 · 최장 {1} 라운드", average, longest));

			// 실력이 같으면 5선승은 동전던지기라 기대값이 8.2 라운드다 — 그 부근이 정상.
			// 훨씬 짧으면 스윕(러버밴딩이 안 먹는다), 훨씬 길면 서로 못 죽여 늘어지는 것.
			Assert.That(average, Is.InRange(7f, 20f), "한 판이 너무 짧거나(스윕) 너무 늘어진다");
		}
	}
}
