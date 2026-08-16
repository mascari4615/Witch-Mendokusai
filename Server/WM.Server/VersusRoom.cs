using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using WitchMendokusai;
using WitchMendokusai.Net;

namespace WitchMendokusai.Server
{
	/// <summary>
	/// 대결 한 판이 사는 자리 (TASK-WM-411) — 두 사람, 한 심판, 하나의 시계.
	///
	/// ★ 규칙도 심판도 여기 없다. <see cref="VersusAuthority"/>(DomainSDK)가 전부 하고,
	///   이 파일은 <b>웹소켓 배선</b>만 한다 — 누가 앉았나, 누가 나갔나, 시계를 누가 돌리나.
	///   그래서 같은 심판이 P2P 호스트(유니티 창)에서도 글자 하나 안 바꾸고 돈다.
	/// </summary>
	public sealed class VersusRoom
	{
		private readonly VersusHost host;
		private readonly VersusSeat[] seats = new VersusSeat[MatchConstants.VERSUS_PLAYER_COUNT];
		private readonly JsonVersusCodec codec = new JsonVersusCodec();
		private readonly object gate = new object();

		private VersusAuthority authority;
		private CancellationTokenSource loopCancel;
		private bool wantsBot;

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
			bool full;

			lock (gate)
			{
				for (int index = 0; index < seats.Length; index++)
				{
					if (seats[index] != null)
						continue;

					taken = new VersusSeat(this, socket, index, new WebSocketVersusTransport(socket));
					seats[index] = taken;
					wantsBot = wantsBot || fillWithBot;
					break;
				}

				full = seats[0] != null && seats[1] != null;
			}

			if (taken == null)
				return null;

			taken.Transport.Send(codec.Encode(new VersusStartMessage
			{
				seat = taken.Index,
				halfWidth = VersusDuelSim.ARENA_HALF_WIDTH,
				halfDepth = VersusDuelSim.ARENA_HALF_DEPTH,
				roundsToWin = VersusRules.Default().RoundsToWin,
				room = Name,
			}));

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
			VersusSeat other;

			lock (gate)
			{
				if (seats[seat.Index] == seat)
					seats[seat.Index] = null;

				other = seats[1 - seat.Index];
				empty = seats[0] == null && seats[1] == null;
			}

			other?.Transport.Send(codec.Encode(new { type = VersusMessageType.OPPONENT_LEFT }));

			if (empty == false)
				return;

			loopCancel?.Cancel();
			host.Remove(this);
		}

		/// <summary> 창이 보낸 말 한 줄 — 뜯어보지 않고 그대로 심판에게 넘긴다(뜻은 심판이 안다). </summary>
		public void Handle(VersusSeat seat, string text)
		{
			seat.Transport.Deliver(text);
		}

		// ── 판 ────────────────────────────────────────────────────────────────

		// 상대가 안 오면 봇으로 채운다(청한 경우만). 혼자 연습하려고 들어온 사람을 영영 세워 두지 않는다.
		private void StartBotWatchdog()
		{
			_ = Task.Run(async () =>
			{
				await Task.Delay(TimeSpan.FromSeconds(VersusHost.BotFillSeconds));

				bool needsBot;
				int emptyIndex;

				lock (gate)
				{
					needsBot = wantsBot && authority == null && (seats[0] == null || seats[1] == null);
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
				if (authority != null)
					return;

				authority = new VersusAuthority(VersusRules.Default(), VersusTuning.Default(), VersusBotTuning.Default(),
					codec, Environment.TickCount ^ Name.GetHashCode(),
					VersusDuelSim.ARENA_HALF_WIDTH, VersusDuelSim.ARENA_HALF_DEPTH, VersusHost.SnapshotHz);

				for (int index = 0; index < seats.Length; index++)
				{
					if (seats[index] != null)
						authority.Attach(index, seats[index].Transport);
				}

				if (botSeat >= 0)
					authority.FillWithBot(botSeat, Environment.TickCount);
			}

			loopCancel = new CancellationTokenSource();
			_ = Task.Run(() => LoopAsync(loopCancel.Token));
		}

		// 심판의 시계. 서버가 느려도 판정은 60Hz 그대로다 — 흘러간 시간을 통째로 넘겨 준다.
		private async Task LoopAsync(CancellationToken stopping)
		{
			TimeSpan step = TimeSpan.FromSeconds(VersusRoundState.TICK);
			DateTime previous = DateTime.UtcNow;

			while (stopping.IsCancellationRequested == false)
			{
				await Task.Delay(step, stopping);

				DateTime now = DateTime.UtcNow;
				float elapsed = (float)(now - previous).TotalSeconds;
				previous = now;

				authority.Tick(elapsed);

				if (authority.Match.IsConcluded)
					return;
			}
		}
	}
}
