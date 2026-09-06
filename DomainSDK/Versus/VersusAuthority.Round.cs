using System.Collections.Generic;
using WitchMendokusai.Net;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	// VersusAuthority.cs 의 Round 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 라운드 시작과 끝, 드래프트.
	public sealed partial class VersusAuthority
	{
		/// <summary>지금 도는 라운드. 심판 자신도 이걸 보고 그린다(호스트가 곧 플레이어인 경우).</summary>
		public VersusRoundState Round { get; private set; }

		private void StartRound()
		{
			Vector2 firstSpawn = new Vector2(-halfWidth * 0.7f, 0f);
			Vector2 secondSpawn = new Vector2(halfWidth * 0.7f, 0f);

			Round = new VersusRoundState(Match.StatsOf(0), Match.StatsOf(1), tuning, halfWidth, halfDepth,
				firstSpawn, secondSpawn);

			tickAccumulator = 0f;
			roundTick = 0;
			lastSnapshotTick = 0;
			inputLog[0].Clear();
			inputLog[1].Clear();

			roundSpawnA = firstSpawn;
			roundSpawnB = secondSpawn;

			// 창이 <b>같은 판을 스스로 지을</b> 재료를 먼저 준다 — 이게 있어야 예측이 첫 틱부터 맞는다.
			for (int seat = 0; seat < transports.Length; seat++)
				SendRoundStartTo(seat);

			BroadcastState();
		}

		private void SendRoundStartTo(int seat)
		{
			if (Round == null || transports[seat] == null || transports[seat].IsOpen == false)
				return;

			SendTo(seat, new VersusRoundStartMessage
			{
				tick = roundTick,
				statsA = Match.StatsOf(0),
				statsB = Match.StatsOf(1),
				spawnAX = roundSpawnA.x,
				spawnAY = roundSpawnA.y,
				spawnBX = roundSpawnB.x,
				spawnBY = roundSpawnB.y,
				halfWidth = halfWidth,
				halfDepth = halfDepth,
				roundTimeLimitSeconds = rules.RoundTimeLimitSeconds,
			});
		}

		private void EndRound()
		{
			Match.ResolveRound(Round.Winner);
			intermission = tuning.IntermissionSeconds;

			Broadcast(new VersusRoundEndMessage
			{
				winner = Round.Winner,
				scoreA = Match.ScoreOf(0),
				scoreB = Match.ScoreOf(1),
			});

			if (Match.IsConcluded)
			{
				Broadcast(new VersusMatchEndMessage { winner = Match.WinnerIndex });
				return;
			}

			if (Match.DraftingPlayerIndex == VersusMatchCore.NO_WINNER)
				return;

			// 진 쪽에게만 후보를 내민다 — 이긴 쪽은 상대가 뭘 골랐는지 다음 판에서 몸으로 안다.
			int drafting = Match.DraftingPlayerIndex;
			pickedOffer[drafting] = -1;

			int[] cards = new int[Match.PendingOffer.Count];
			string[] texts = new string[Match.PendingOffer.Count];

			for (int index = 0; index < Match.PendingOffer.Count; index++)
			{
				cards[index] = (int)Match.PendingOffer[index];
				texts[index] = VersusCards.Describe(Match.PendingOffer[index]);
			}

			SendTo(drafting, new VersusOfferMessage { cards = cards, texts = texts });
		}

		private void TickDraft()
		{
			int drafting = Match.DraftingPlayerIndex;

			// 봇은 고민하지 않는다 — 아무거나 집고 다음 판으로.
			if (isBot[drafting])
			{
				VersusRandom random = new VersusRandom(tick + 1);
				Match.TakeOffered(random.NextInt(Match.PendingOffer.Count));
				intermission = tuning.IntermissionSeconds;
				return;
			}

			if (pickedOffer[drafting] < 0)
				return;

			Match.TakeOffered(pickedOffer[drafting]);
			pickedOffer[drafting] = -1;
			intermission = tuning.IntermissionSeconds;
		}
	}
}

