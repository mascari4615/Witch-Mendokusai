using System.Collections.Generic;
using WitchMendokusai.Net;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	/// <summary>
	/// 대결의 <b>심판</b> — 판을 돌리고 결과를 말한다 (TASK-WM-411).
	///
	/// ★ 이 코드가 어디서 도는지는 상관없다:
	///   · 서버가 돌리면 = 서버 권위(공정 · NAT 걱정 0)
	///   · 한쪽 창이 돌리면 = <b>P2P 호스트 권위</b>(지연 낮음 · 서버비 0)
	///   · 혼자 연습이면 = 내 컴퓨터가 돌리고 상대는 봇
	///   셋이 <b>같은 코드</b>다. 나르는 방법은 <see cref="IVersusTransport"/> 가, 글자 변환은
	///   <see cref="IVersusCodec"/> 가 밖에서 꽂힌다.
	///
	/// 심판은 위치를 <b>정해서 내려 준다</b>. 두 화면이 어긋나도 결과가 하나인 이유가 이것이다.
	/// </summary>
	public sealed class VersusAuthority
	{
		private readonly VersusRules rules;
		private readonly VersusTuning tuning;
		private readonly VersusBotTuning botTuning;
		private readonly IVersusCodec codec;
		private readonly IVersusTransport[] transports = new IVersusTransport[MatchConstants.VERSUS_PLAYER_COUNT];
		private readonly VersusInputFrame[] frames = new VersusInputFrame[MatchConstants.VERSUS_PLAYER_COUNT];
		private readonly VersusBotPolicy[] botPolicies = new VersusBotPolicy[MatchConstants.VERSUS_PLAYER_COUNT];
		private readonly bool[] isBot = new bool[MatchConstants.VERSUS_PLAYER_COUNT];
		private readonly VersusRandom[] botRandom = new VersusRandom[MatchConstants.VERSUS_PLAYER_COUNT];
		private readonly List<VersusBodyView> shotBuffer = new List<VersusBodyView>();
		private readonly List<string> incoming = new List<string>();
		private readonly int[] pickedOffer = new int[MatchConstants.VERSUS_PLAYER_COUNT];
		private readonly bool[] wantsRematch = new bool[MatchConstants.VERSUS_PLAYER_COUNT];
		private readonly VersusRules rulesForRematch;

		private readonly float halfWidth;
		private readonly float halfDepth;
		private readonly int snapshotEvery;

		private int matchSeed;
		private float tickAccumulator;
		private float intermission;
		private int tick;

		public VersusAuthority(VersusRules rules, VersusTuning tuning, VersusBotTuning botTuning,
			IVersusCodec codec, int seed, float halfWidth, float halfDepth, int snapshotHz = 20)
		{
			this.rules = rules;
			this.tuning = tuning;
			this.botTuning = botTuning;
			this.codec = codec;
			this.halfWidth = halfWidth;
			this.halfDepth = halfDepth;

			// 판정은 60Hz, 보내기는 그보다 성기게 — 사람 눈에는 충분하고 줄은 가볍다.
			snapshotEvery = snapshotHz > 0 ? Mathf.Max(1, (int)(1f / VersusRoundState.TICK) / snapshotHz) : 3;

			rulesForRematch = rules;
			matchSeed = seed;
			Match = new VersusMatchCore(rules, seed);
			pickedOffer[0] = -1;
			pickedOffer[1] = -1;
			StartRound();
		}

		public VersusMatchCore Match { get; private set; }

		/// <summary>「한 판 더」에 손 든 사람 수 — 화면이 「1/2 기다리는 중」을 띄우는 데 쓴다.</summary>
		public int RematchReady
		{
			get
			{
				int ready = 0;

				for (int seat = 0; seat < wantsRematch.Length; seat++)
				{
					if (wantsRematch[seat])
						ready++;
				}

				return ready;
			}
		}

		/// <summary>몇 명이 손을 들어야 새 판이 서나 — 봇 자리는 항상 준비된 것으로 친다.</summary>
		public int RematchNeeded
		{
			get
			{
				int needed = 0;

				for (int seat = 0; seat < isBot.Length; seat++)
				{
					if (isBot[seat] == false)
						needed++;
				}

				return needed < 1 ? 1 : needed;
			}
		}

		/// <summary>지금 도는 라운드. 심판 자신도 이걸 보고 그린다(호스트가 곧 플레이어인 경우).</summary>
		public VersusRoundState Round { get; private set; }

		/// <summary> 그 자리에 네트워크 상대를 앉힌다. 안 앉히면 그 자리는 로컬(사람 or 봇)이다. </summary>
		public void Attach(int seat, IVersusTransport transport)
		{
			transports[seat] = transport;
			isBot[seat] = false;
		}

		/// <summary> 그 자리를 봇으로 채운다. </summary>
		public void FillWithBot(int seat, int seed)
		{
			isBot[seat] = true;
			transports[seat] = null;
			VersusRandom random = new VersusRandom(seed);
			botPolicies[seat] = new VersusBotPolicy(botTuning, halfWidth, halfDepth,
				random.NextInt(2) == 0 ? 1f : -1f, 0f);
			botRandom[seat] = random;
		}

		/// <summary> 이 컴퓨터 앞 사람의 의도를 넣는다(호스트가 직접 플레이할 때). </summary>
		public void SubmitLocalInput(int seat, VersusInputFrame frame)
		{
			frames[seat] = frame;
		}

		/// <summary> 이 컴퓨터 앞 사람이 카드를 골랐다. </summary>
		public void SubmitLocalPick(int seat, int offerIndex)
		{
			pickedOffer[seat] = offerIndex;
		}

		/// <summary> 이 컴퓨터 앞 사람이 「한 판 더」를 눌렀다. </summary>
		public void SubmitLocalRematch(int seat)
		{
			RequestRematch(seat);
		}

		/// <summary>
		/// 흘러간 시간만큼 판을 굴린다. 60Hz 고정 틱이라 프레임이 들쭉날쭉해도 결과가 같다.
		/// </summary>
		public void Tick(float deltaSeconds)
		{
			ReceiveAll();

			if (Match.IsConcluded)
			{
				// 끝났다고 방을 접지 않는다 — v0 가 재려는 것이 바로 「한 판 더가 나오나」다.
				TickRematch();
				return;
			}

			if (Round != null && Round.IsOver == false)
			{
				tickAccumulator += deltaSeconds;

				while (tickAccumulator >= VersusRoundState.TICK && Round.IsOver == false)
				{
					tickAccumulator -= VersusRoundState.TICK;
					tick++;

					for (int seat = 0; seat < frames.Length; seat++)
					{
						if (isBot[seat] == false || botPolicies[seat] == null)
							continue;

						// 조준을 매번 조금씩 흔든다. 고정값이면 봇 둘이 거울처럼 움직여 <b>영원히 무승부</b>가 난다
						// (2026-08-16 실측: 봇끼리 10분을 돌려도 점수가 0-0 이었다).
						float jitter = (botRandom[seat].NextInt(2001) / 1000f - 1f) * 6f * Mathf.Deg2Rad;
						frames[seat] = botPolicies[seat].Decide(Round, seat, VersusRoundState.TICK, jitter);
					}

					Round.Step(frames, rules.RoundTimeLimitSeconds);

					if (tick % snapshotEvery == 0 || Round.IsOver)
						BroadcastState();
				}

				if (Round.IsOver)
					EndRound();

				return;
			}

			if (Match.DraftingPlayerIndex != VersusMatchCore.NO_WINNER)
			{
				TickDraft();
				return;
			}

			intermission -= deltaSeconds;
			if (intermission <= 0f)
				StartRound();
		}

		// ── 안쪽 ──────────────────────────────────────────────────────────────

		private void StartRound()
		{
			Round = new VersusRoundState(Match.StatsOf(0), Match.StatsOf(1), tuning, halfWidth, halfDepth,
				new Vector2(-halfWidth * 0.7f, 0f), new Vector2(halfWidth * 0.7f, 0f));

			tickAccumulator = 0f;
			BroadcastState();
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

		private void ReceiveAll()
		{
			for (int seat = 0; seat < transports.Length; seat++)
			{
				IVersusTransport transport = transports[seat];

				if (transport == null)
					continue;

				transport.Drain(incoming);

				for (int index = 0; index < incoming.Count; index++)
					Receive(seat, incoming[index]);
			}
		}

		private void Receive(int seat, string message)
		{
			string type = codec.TypeOf(message);

			if (type == VersusMessageType.INPUT)
			{
				VersusInputMessage input = codec.Decode<VersusInputMessage>(message);

				if (input == null)
					return;

				frames[seat] = new VersusInputFrame
				{
					Move = new Vector2(input.moveX, input.moveY),
					Aim = new Vector2(input.aimX, input.aimY),
					Fire = input.fire,
					Dash = input.dash,
				};
				return;
			}

			if (type == VersusMessageType.PICK)
			{
				VersusPickMessage pick = codec.Decode<VersusPickMessage>(message);

				if (pick != null)
					pickedOffer[seat] = pick.index;

				return;
			}

			if (type == VersusMessageType.REMATCH)
				RequestRematch(seat);
		}

		// ── 한 판 더 ──────────────────────────────────────────────────────────

		private void RequestRematch(int seat)
		{
			if (Match.IsConcluded == false)
				return;

			wantsRematch[seat] = true;
			Broadcast(new VersusRematchStateMessage { ready = RematchReady, needed = RematchNeeded });
		}

		// 손 든 사람이 다 차면 <b>완전히 새 판</b>을 연다 — 카드도 점수도 처음부터.
		// 씨앗을 바꾸는 이유: 같은 씨앗이면 카드 후보 순서가 똑같아 「또 그 카드」가 된다.
		private void TickRematch()
		{
			if (RematchReady < RematchNeeded)
				return;

			for (int seat = 0; seat < wantsRematch.Length; seat++)
			{
				wantsRematch[seat] = false;
				pickedOffer[seat] = -1;
			}

			matchSeed = matchSeed * 31 + 17;
			Match = new VersusMatchCore(rulesForRematch, matchSeed);
			tick = 0;
			StartRound();
		}

		private void BroadcastState()
		{
			Round.CollectShots(shotBuffer);

			VersusBodyMessage[] fighters = new VersusBodyMessage[MatchConstants.VERSUS_PLAYER_COUNT];
			for (int seat = 0; seat < fighters.Length; seat++)
			{
				Vector2 position = Round.PositionOf(seat);
				fighters[seat] = new VersusBodyMessage
				{
					x = position.x,
					y = position.y,
					r = Round.RadiusOf(seat),
					owner = seat,
					alive = Round.IsAlive(seat),
				};
			}

			VersusBodyMessage[] shots = new VersusBodyMessage[shotBuffer.Count];
			for (int index = 0; index < shots.Length; index++)
			{
				shots[index] = new VersusBodyMessage
				{
					x = shotBuffer[index].Position.x,
					y = shotBuffer[index].Position.y,
					r = shotBuffer[index].Radius,
					owner = shotBuffer[index].Owner,
					alive = true,
				};
			}

			Broadcast(new VersusStateMessage
			{
				tick = tick,
				fighters = fighters,
				shots = shots,
				scoreA = Match.ScoreOf(0),
				scoreB = Match.ScoreOf(1),
			});
		}

		private void Broadcast(object message)
		{
			string text = null;

			for (int seat = 0; seat < transports.Length; seat++)
			{
				if (transports[seat] == null || transports[seat].IsOpen == false)
					continue;

				text = text ?? codec.Encode(message);
				transports[seat].Send(text);
			}
		}

		private void SendTo(int seat, object message)
		{
			if (transports[seat] == null || transports[seat].IsOpen == false)
				return;

			transports[seat].Send(codec.Encode(message));
		}
	}
}
