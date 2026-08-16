using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 대결 매치 브레인 — 점수·카드 뽑기·매치 종료. MonoBehaviour/물리/입력 0(엔진 밖에서 그대로 돈다).
	/// 핵심 규칙은 하나: **진 쪽만 카드를 뽑는다.** 이긴 쪽이 계속 이기면 상대의 빌드가 계속 두꺼워져
	/// 저절로 따라붙는다 — 실력 차가 나도 판이 안 죽는 이유(ROUNDS 의 심장).
	/// </summary>
	public class VersusMatchCore
	{
		public const int NO_WINNER = MatchConstants.NO_WINNER;
		public const int PLAYER_COUNT = 2;

		private readonly VersusRules rules;
		private readonly VersusFighterStats[] stats = new VersusFighterStats[PLAYER_COUNT];
		private readonly List<VersusCardKind>[] owned = new List<VersusCardKind>[PLAYER_COUNT];
		private readonly int[] scores = new int[PLAYER_COUNT];
		private VersusRandom random;
		private List<VersusCardKind> pendingOffer;

		public VersusMatchCore(VersusRules rules, int seed)
		{
			this.rules = rules;
			random = new VersusRandom(seed);

			for (int playerIndex = 0; playerIndex < PLAYER_COUNT; playerIndex++)
			{
				stats[playerIndex] = VersusFighterStats.Default();
				owned[playerIndex] = new List<VersusCardKind>();
			}
		}

		public int RoundNumber { get; private set; } = 1;
		public bool IsConcluded { get; private set; }
		public int WinnerIndex { get; private set; } = NO_WINNER;

		/// <summary> 카드를 고를 차례인 플레이어. 없으면 <see cref="NO_WINNER"/>. 이 값이 있으면 다음 라운드는 못 시작한다. </summary>
		public int DraftingPlayerIndex { get; private set; } = NO_WINNER;

		public int ScoreOf(int playerIndex) => scores[playerIndex];
		public VersusFighterStats StatsOf(int playerIndex) => stats[playerIndex];
		public IReadOnlyList<VersusCardKind> CardsOf(int playerIndex) => owned[playerIndex];

		/// <summary> 지금 내밀고 있는 카드 후보. 뽑을 차례가 아니면 빈 목록. </summary>
		public IReadOnlyList<VersusCardKind> PendingOffer =>
			pendingOffer ?? (IReadOnlyList<VersusCardKind>)System.Array.Empty<VersusCardKind>();

		/// <summary>
		/// 한 라운드가 끝났다. <paramref name="winnerIndex"/> = 이긴 사람(무승부는 <see cref="NO_WINNER"/>).
		/// 승자 점수 +1 → 매치가 끝났으면 거기서 멈추고, 안 끝났으면 **진 쪽에게 카드를 내민다**.
		/// 무승부면 점수도 카드도 없이 그 라운드를 다시 한다(즉사제라 동시사가 실제로 자주 난다).
		/// </summary>
		public void ResolveRound(int winnerIndex)
		{
			if (IsConcluded || DraftingPlayerIndex != NO_WINNER)
				return;

			if (winnerIndex == NO_WINNER)
			{
				RoundNumber++;
				return;
			}

			scores[winnerIndex]++;

			if (scores[winnerIndex] >= rules.RoundsToWin)
			{
				IsConcluded = true;
				WinnerIndex = winnerIndex;
				return;
			}

			int loserIndex = 1 - winnerIndex;
			DraftingPlayerIndex = loserIndex;
			pendingOffer = VersusCardDraft.Draw(ref random, rules.CardsOfferedToLoser);
			RoundNumber++;
		}

		/// <summary>
		/// 내민 후보 중 하나를 고른다(인덱스). 고른 카드는 즉시 그 사람의 스탯에 얹히고 다음 라운드부터 적용된다.
		/// 뽑을 차례가 아니거나 범위 밖이면 아무 일도 안 일어난다(멱등·안전).
		/// </summary>
		public bool TakeOffered(int offerIndex)
		{
			if (DraftingPlayerIndex == NO_WINNER || pendingOffer == null)
				return false;

			if (offerIndex < 0 || offerIndex >= pendingOffer.Count)
				return false;

			VersusCardKind picked = pendingOffer[offerIndex];
			int playerIndex = DraftingPlayerIndex;

			owned[playerIndex].Add(picked);
			stats[playerIndex] = VersusCards.Apply(stats[playerIndex], picked);

			pendingOffer = null;
			DraftingPlayerIndex = NO_WINNER;
			return true;
		}

		/// <summary> 다음 라운드를 시작해도 되나 — 매치가 안 끝났고 아무도 카드를 고르는 중이 아닐 때. </summary>
		public bool CanStartNextRound => !IsConcluded && DraftingPlayerIndex == NO_WINNER;
	}
}
