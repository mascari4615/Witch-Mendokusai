using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	/// <summary>
	/// 봇 둘을 붙여 <b>엔진 없이</b> 판을 굴린다 (TASK-WM-411). 목적은 그림이 아니라 <b>숫자</b> —
	/// 어느 카드가 세고, 어떤 조합이 판을 깨고, 한 판이 몇 라운드에 끝나는지를 사람이 앉기 전에 본다.
	///
	/// ★ 규칙은 여기에 <b>없다</b>. <see cref="VersusRoundState"/>(서버·유니티·웹이 같이 쓰는 한 벌)를 그대로 돌린다.
	///   전에는 시뮬이 규칙 사본을 들고 있었다 — 그러면 「시뮬에선 되는데 게임에선 다른」 상태가 조용히 생긴다.
	///
	/// ⚠ 근사인 곳(정직하게): 곡사는 높이 대신 사거리 단축. 몸·탄은 원, 벽은 사각.
	///   즉 이 숫자는 <b>기울기</b>를 보는 도구지 손맛의 증거가 아니다.
	/// </summary>
	public static class VersusDuelSim
	{
		public const float ARENA_HALF_WIDTH = 13f;
		public const float ARENA_HALF_DEPTH = 9f;

		/// <summary>
		/// 한 라운드를 끝까지 굴린다. 반환 = 이긴 쪽(0/1) 또는 <see cref="VersusMatchCore.NO_WINNER"/>.
		/// </summary>
		public static int RunRound(VersusFighterStats first, VersusFighterStats second, VersusTuning tuning,
			VersusBotTuning botTuning, float timeLimitSeconds, ref VersusRandom random)
		{
			// ★ 판마다 흔든다. 안 흔들면 두 정책이 같아 결과가 <b>분포가 아니라 하나의 답</b>이 된다 —
			//   실제로 승률이 0%/100% 로만 나왔다(2026-08-16 실측). 사람이 앉으면 늘 있는 흔들림
			//   (시작 자리 · 타이밍 · 조준)을 최소한으로 흉내 내야 「몇 % 」라는 말이 뜻을 가진다.
			Vector2 firstSpawn = new Vector2(-ARENA_HALF_WIDTH * 0.7f + Jitter(ref random, 2.5f), Jitter(ref random, 5f));
			Vector2 secondSpawn = new Vector2(ARENA_HALF_WIDTH * 0.7f + Jitter(ref random, 2.5f), Jitter(ref random, 5f));

			VersusRoundState state = new VersusRoundState(first, second, tuning,
				ARENA_HALF_WIDTH, ARENA_HALF_DEPTH, firstSpawn, secondSpawn);

			VersusBotPolicy[] policies = new VersusBotPolicy[VersusRoundState.PLAYER_COUNT];
			for (int index = 0; index < policies.Length; index++)
			{
				policies[index] = new VersusBotPolicy(botTuning, ARENA_HALF_WIDTH, ARENA_HALF_DEPTH,
					random.NextInt(2) == 0 ? 1f : -1f, Jitter(ref random, botTuning.StrafeFlipSeconds));
			}

			VersusInputFrame[] inputs = new VersusInputFrame[VersusRoundState.PLAYER_COUNT];

			while (state.IsOver == false)
			{
				for (int index = 0; index < inputs.Length; index++)
				{
					// 완벽 조준은 사람이 아니다 — ±6도 흔든다. 이 흔들림이 「탄이 빠를수록 잘 맞는다」를 살린다.
					inputs[index] = policies[index].Decide(state, index, VersusRoundState.TICK,
						Jitter(ref random, 6f) * Mathf.Deg2Rad);
				}

				state.Step(inputs, timeLimitSeconds);
			}

			return state.Winner;
		}

		/// <summary>
		/// 한 매치(5선승)를 끝까지 굴린다 — 진 쪽이 카드를 뽑는 러버밴딩까지 그대로.
		/// 반환 = (이긴 쪽, 돈 라운드 수).
		/// </summary>
		public static (int winner, int rounds) RunMatch(VersusRules rules, VersusTuning tuning,
			VersusBotTuning botTuning, int seed, IReadOnlyList<VersusCardKind> firstStartingCards = null)
		{
			VersusRandom random = new VersusRandom(seed);
			VersusMatchCore match = new VersusMatchCore(rules, seed);
			VersusFighterStats[] stats = new VersusFighterStats[VersusRoundState.PLAYER_COUNT];
			stats[0] = VersusFighterStats.Default();
			stats[1] = VersusFighterStats.Default();

			// 「이 카드 한 장이 얼마나 센가」를 재려고 0번에게만 미리 쥐여 줄 수 있다.
			if (firstStartingCards != null)
			{
				for (int index = 0; index < firstStartingCards.Count; index++)
					stats[0] = VersusCards.Apply(stats[0], firstStartingCards[index]);
			}

			int rounds = 0;

			while (match.IsConcluded == false && rounds < 400)
			{
				rounds++;
				match.ResolveRound(RunRound(stats[0], stats[1], tuning, botTuning, rules.RoundTimeLimitSeconds, ref random));

				if (match.DraftingPlayerIndex == VersusMatchCore.NO_WINNER)
					continue;

				int drafting = match.DraftingPlayerIndex;
				int pick = random.NextInt(match.PendingOffer.Count);
				VersusCardKind card = match.PendingOffer[pick];
				match.TakeOffered(pick);
				stats[drafting] = VersusCards.Apply(stats[drafting], card);
			}

			return (match.WinnerIndex, rounds);
		}

		/// <summary> -amount ~ +amount 사이의 흔들림. </summary>
		private static float Jitter(ref VersusRandom random, float amount)
		{
			return (random.NextInt(2001) / 1000f - 1f) * amount;
		}
	}
}
