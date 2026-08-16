using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WitchMendokusai;
using WitchMendokusai.Net;

namespace WitchMendokusai.Server
{
	/// <summary>
	/// 대결 한 판이 사는 자리 (TASK-WM-411) — 두 사람, 한 심판, 하나의 시계.
	///
	/// 시계가 <b>서버에</b> 있는 것이 요점이다. 창은 자기 프레임으로 그리지만 판정은 60Hz 로 여기서만 돈다.
	/// 그래서 두 화면이 조금 어긋나도 <b>결과는 하나</b>다.
	/// </summary>
	public sealed class VersusRoom
	{
		private readonly VersusHost host;
		private readonly VersusSeat[] seats = new VersusSeat[MatchConstants.VERSUS_PLAYER_COUNT];
		private readonly VersusRules rules = VersusRules.Default();
		private readonly VersusTuning tuning = VersusTuning.Default();
		private readonly VersusBotTuning botTuning = VersusBotTuning.Default();
		private readonly VersusInputFrame[] frames = new VersusInputFrame[MatchConstants.VERSUS_PLAYER_COUNT];
		private readonly List<VersusBodyView> shotBuffer = new List<VersusBodyView>();
		private readonly object gate = new object();

		private VersusMatchCore match;
		private VersusRoundState round;
		private VersusBotPolicy botPolicy;
		private CancellationTokenSource loopCancel;
		private DateTime openedAt = DateTime.UtcNow;
		private bool wantsBot;
		private int botSeatIndex = -1;
		private int tick;

		public VersusRoom(string name, VersusHost host)
		{
			Name = name;
			this.host = host;
		}

		public string Name { get; }

		public bool HasOpenSeat
		{
			get
			{
				lock (gate)
					return seats[0] == null || seats[1] == null;
			}
		}

		/// <summary> 빈 의자에 앉힌다. 자리가 없으면 null. 둘이 차면 그 자리에서 판이 시작된다. </summary>
		public VersusSeat TryTake(WebSocket socket, bool fillWithBot)
		{
			VersusSeat taken = null;
			bool full = false;

			lock (gate)
			{
				for (int index = 0; index < seats.Length; index++)
				{
					if (seats[index] != null)
						continue;

					taken = new VersusSeat(this, socket, index);
					seats[index] = taken;
					wantsBot = wantsBot || fillWithBot;
					break;
				}

				full = seats[0] != null && seats[1] != null;
			}

			if (taken == null)
				return null;

			_ = VersusHost.SendAsync(socket, new VersusStartMessage
			{
				seat = taken.Index,
				halfWidth = VersusDuelSim.ARENA_HALF_WIDTH,
				halfDepth = VersusDuelSim.ARENA_HALF_DEPTH,
				roundsToWin = rules.RoundsToWin,
				room = Name,
			}, CancellationToken.None);

			if (full)
				StartMatch(-1);
			else
				StartBotWatchdog();

			return taken;
		}

		/// <summary> 창이 나갔다. 남은 사람에게 알리고, 아무도 없으면 방을 접는다. </summary>
		public void Leave(VersusSeat seat)
		{
			bool empty;
			VersusSeat other = null;

			lock (gate)
			{
				if (seats[seat.Index] == seat)
					seats[seat.Index] = null;

				other = seats[1 - seat.Index];
				empty = seats[0] == null && seats[1] == null;
			}

			if (other != null)
				_ = VersusHost.SendAsync(other.Socket, new { type = VersusMessageType.OPPONENT_LEFT }, CancellationToken.None);

			if (empty == false)
				return;

			loopCancel?.Cancel();
			host.Remove(this);
		}

		/// <summary> 창이 보낸 말 한 줄. 대결에서 창이 할 수 있는 말은 둘뿐 — 의도, 그리고 카드 선택. </summary>
		public void Handle(VersusSeat seat, string text)
		{
			try
			{
				using JsonDocument document = JsonDocument.Parse(text);

				if (document.RootElement.TryGetProperty("type", out JsonElement typeElement) == false)
					return;

				string type = typeElement.GetString();

				if (type == VersusMessageType.INPUT)
				{
					VersusInputMessage input = JsonSerializer.Deserialize<VersusInputMessage>(text, VersusHost.JsonOptions);

					if (input == null)
						return;

					// 늦게 온 것은 버린다 — 지나간 틱을 되살리면 서버가 과거로 끌려간다.
					seat.Frame = new VersusInputFrame
					{
						Move = new WitchMendokusai.Numerics.Vector2(input.moveX, input.moveY),
						Aim = new WitchMendokusai.Numerics.Vector2(input.aimX, input.aimY),
						Fire = input.fire,
						Dash = input.dash,
					};
					return;
				}

				if (type == VersusMessageType.PICK)
				{
					VersusPickMessage pick = JsonSerializer.Deserialize<VersusPickMessage>(text, VersusHost.JsonOptions);

					if (pick != null)
						seat.PickedOffer = pick.index;
				}
			}
			catch (JsonException)
			{
				// 못 알아들을 말은 무시한다 — 판을 세울 이유가 아니다.
			}
		}

		// ── 판 ────────────────────────────────────────────────────────────────

		// 상대가 안 오면 봇으로 채운다(청한 경우만). 혼자 연습하려고 들어온 사람을 영영 세워 두지 않는다.
		private void StartBotWatchdog()
		{
			openedAt = DateTime.UtcNow;

			_ = Task.Run(async () =>
			{
				await Task.Delay(TimeSpan.FromSeconds(VersusHost.BotFillSeconds));

				bool needsBot;
				int emptyIndex = -1;

				lock (gate)
				{
					needsBot = wantsBot && match == null && (seats[0] == null || seats[1] == null);
					emptyIndex = seats[0] == null ? 0 : 1;
				}

				if (needsBot)
					StartMatch(emptyIndex);
			});
		}

		private void StartMatch(int botSeat)
		{
			lock (gate)
			{
				if (match != null)
					return;

				botSeatIndex = botSeat;
				match = new VersusMatchCore(rules, Environment.TickCount ^ Name.GetHashCode());
			}

			loopCancel = new CancellationTokenSource();
			_ = Task.Run(() => LoopAsync(loopCancel.Token));
		}

		private async Task LoopAsync(CancellationToken stopping)
		{
			TimeSpan step = TimeSpan.FromSeconds(1.0 / VersusHost.TickHz);
			int snapshotEvery = Math.Max(1, VersusHost.TickHz / VersusHost.SnapshotHz);
			StartRound();

			while (stopping.IsCancellationRequested == false)
			{
				await Task.Delay(step, stopping);
				tick++;

				if (round != null && round.IsOver == false)
				{
					for (int index = 0; index < frames.Length; index++)
						frames[index] = FrameOf(index);

					round.Step(frames, rules.RoundTimeLimitSeconds);

					if (tick % snapshotEvery == 0 || round.IsOver)
						await BroadcastStateAsync(stopping);

					if (round.IsOver)
						await EndRoundAsync(stopping);

					continue;
				}

				if (match.DraftingPlayerIndex != VersusMatchCore.NO_WINNER)
				{
					await TickDraftAsync(stopping);
					continue;
				}

				if (match.IsConcluded)
				{
					await BroadcastAsync(new VersusMatchEndMessage { winner = match.WinnerIndex }, stopping);
					return;
				}

				StartRound();
			}
		}

		private void StartRound()
		{
			round = new VersusRoundState(match.StatsOf(0), match.StatsOf(1), tuning,
				VersusDuelSim.ARENA_HALF_WIDTH, VersusDuelSim.ARENA_HALF_DEPTH,
				new WitchMendokusai.Numerics.Vector2(-VersusDuelSim.ARENA_HALF_WIDTH * 0.7f, 0f),
				new WitchMendokusai.Numerics.Vector2(VersusDuelSim.ARENA_HALF_WIDTH * 0.7f, 0f));

			if (botSeatIndex >= 0)
			{
				botPolicy = new VersusBotPolicy(botTuning,
					VersusDuelSim.ARENA_HALF_WIDTH, VersusDuelSim.ARENA_HALF_DEPTH, 1f, 0f);
			}
		}

		// 사람 자리는 마지막으로 받은 의도, 봇 자리는 지금 판을 보고 만든 의도.
		private VersusInputFrame FrameOf(int index)
		{
			if (index == botSeatIndex && botPolicy != null)
				return botPolicy.Decide(round, index, VersusRoundState.TICK, 0.09f);

			VersusSeat seat;

			lock (gate)
				seat = seats[index];

			return seat != null ? seat.Frame : default;
		}

		private async Task EndRoundAsync(CancellationToken stopping)
		{
			match.ResolveRound(round.Winner);

			await BroadcastAsync(new VersusRoundEndMessage
			{
				winner = round.Winner,
				scoreA = match.ScoreOf(0),
				scoreB = match.ScoreOf(1),
			}, stopping);

			if (match.DraftingPlayerIndex == VersusMatchCore.NO_WINNER)
				return;

			// 진 쪽에게만 후보를 내민다 — 이긴 쪽은 상대가 뭘 골랐는지 다음 판에서 몸으로 안다.
			int drafting = match.DraftingPlayerIndex;
			int[] cards = new int[match.PendingOffer.Count];
			string[] texts = new string[match.PendingOffer.Count];

			for (int index = 0; index < match.PendingOffer.Count; index++)
			{
				cards[index] = (int)match.PendingOffer[index];
				texts[index] = VersusCards.Describe(match.PendingOffer[index]);
			}

			VersusSeat seat;

			lock (gate)
				seat = seats[drafting];

			if (seat != null)
			{
				seat.PickedOffer = -1;
				await VersusHost.SendAsync(seat.Socket, new VersusOfferMessage { cards = cards, texts = texts }, stopping);
			}
		}

		private async Task TickDraftAsync(CancellationToken stopping)
		{
			int drafting = match.DraftingPlayerIndex;

			// 봇은 고민하지 않는다.
			if (drafting == botSeatIndex)
			{
				match.TakeOffered(new Random(tick).Next(match.PendingOffer.Count));
				return;
			}

			VersusSeat seat;

			lock (gate)
				seat = seats[drafting];

			// 고른 사람이 나갔으면 판이 멈춘다 — 아무거나 집어 다음으로 보낸다.
			if (seat == null)
			{
				match.TakeOffered(0);
				return;
			}

			if (seat.PickedOffer < 0)
				return;

			match.TakeOffered(seat.PickedOffer);
			seat.PickedOffer = -1;
			await Task.CompletedTask;
		}

		private async Task BroadcastStateAsync(CancellationToken stopping)
		{
			round.CollectShots(shotBuffer);

			VersusBodyMessage[] fighters = new VersusBodyMessage[MatchConstants.VERSUS_PLAYER_COUNT];
			for (int index = 0; index < fighters.Length; index++)
			{
				WitchMendokusai.Numerics.Vector2 position = round.PositionOf(index);
				fighters[index] = new VersusBodyMessage
				{
					x = position.x,
					y = position.y,
					r = round.RadiusOf(index),
					owner = index,
					alive = round.IsAlive(index),
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

			await BroadcastAsync(new VersusStateMessage
			{
				tick = tick,
				fighters = fighters,
				shots = shots,
				scoreA = match.ScoreOf(0),
				scoreB = match.ScoreOf(1),
			}, stopping);
		}

		private async Task BroadcastAsync(object message, CancellationToken stopping)
		{
			VersusSeat first;
			VersusSeat second;

			lock (gate)
			{
				first = seats[0];
				second = seats[1];
			}

			if (first != null)
				await VersusHost.SendAsync(first.Socket, message, stopping);

			if (second != null)
				await VersusHost.SendAsync(second.Socket, message, stopping);
		}
	}
}
