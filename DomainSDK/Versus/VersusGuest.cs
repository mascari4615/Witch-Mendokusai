using System.Collections.Generic;
using WitchMendokusai.Net;

namespace WitchMendokusai
{
	/// <summary>
	/// 심판이 아닌 쪽 (TASK-WM-411) — <b>의도를 보내고 그림을 받는다</b>. 규칙을 스스로 돌리지 않는다.
	///
	/// 심판이 서버든 상대 컴퓨터(P2P 호스트)든 이 코드는 같다. 나르는 방법도 모른다
	/// (<see cref="IVersusTransport"/> 가 밖에서 꽂힌다) — 그래서 유니티·웹이 이걸 그대로 쓴다.
	///
	/// ★ 규칙을 <b>돌리기는 한다</b>(예측). 다만 <b>정하지는 않는다</b> — 정본은 언제나 심판의 스냅샷이고,
	///   오면 되감아 다시 굴린다(<see cref="VersusPredictor"/>). 이 둘의 차이가 「내 화면에선 맞혔는데」를 없앤다.
	/// </summary>
	public sealed class VersusGuest
	{
		private readonly IVersusTransport transport;
		private readonly IVersusCodec codec;
		private readonly List<string> incoming = new List<string>();
		private VersusPredictor predictor;
		private VersusTuning tuning = VersusTuning.Default();
		private float roundTimeLimitSeconds;
		private int localTick;

		public VersusGuest(IVersusTransport transport, IVersusCodec codec, int seat)
		{
			this.transport = transport;
			this.codec = codec;
			Seat = seat;
		}

		/// <summary>내가 몇 번 자리인가(0/1). 화면에서 「나」를 가리는 유일한 기준.</summary>
		public int Seat { get; private set; }

		public int ScoreMine { get; private set; }
		public int ScoreTheirs { get; private set; }

		/// <summary>가장 최근에 받은 그림 — 사람 둘.</summary>
		public VersusBodyMessage[] Fighters { get; private set; } = new VersusBodyMessage[0];

		/// <summary>가장 최근에 받은 그림 — 탄들.</summary>
		public VersusBodyMessage[] Shots { get; private set; } = new VersusBodyMessage[0];

		/// <summary>내가 골라야 할 카드 후보. 비어 있으면 지금은 고를 차례가 아니다.</summary>
		public VersusOfferMessage Offer { get; private set; }

		/// <summary>매치가 끝났으면 이긴 자리(0/1), 아니면 -1.</summary>
		public int MatchWinner { get; private set; } = VersusMatchCore.NO_WINNER;

		/// <summary>상대가 나갔나.</summary>
		public bool OpponentLeft { get; private set; }

		/// <summary>
		/// 내가 미리 굴리는 판 — 화면은 <b>이걸</b> 그린다(내 조작이 즉시 반응하는 이유).
		/// 심판 스냅샷이 오면 이 판이 되감겨 정정된다. 라운드 재료가 오기 전에는 null.
		/// </summary>
		public VersusRoundState Predicted { get; private set; }

		/// <summary>몇 번 되감았나 — 회선 상태를 화면에 보여 주거나 로그로 잴 때.</summary>
		public int RollbackCount => predictor != null ? predictor.RollbackCount : 0;

		/// <summary>「한 판 더」에 손 든 사람 수 / 필요한 수 — 기다리는 화면에 그대로 쓴다.</summary>
		public int RematchReady { get; private set; }
		public int RematchNeeded { get; private set; }

		/// <summary>방금 라운드가 끝났다면 그 승자(한 번만 참). 화면 연출에 쓴다.</summary>
		public int LastRoundWinner { get; private set; } = VersusMatchCore.NO_WINNER;
		public bool RoundJustEnded { get; private set; }

		/// <summary>
		/// 한 틱을 <b>미리 굴리고</b> 그 의도를 심판에게 보낸다 — 온라인에서 손맛을 지키는 핵심 한 줄.
		/// 라운드 재료가 아직 안 왔으면 보내기만 한다.
		/// </summary>
		public void StepAndSend(VersusInputFrame frame)
		{
			if (predictor != null)
				predictor.Step(frame, roundTimeLimitSeconds);

			localTick++;
			SendInput(frame, predictor != null ? predictor.CurrentTick : localTick);
		}

		/// <summary> 이번 틱의 의도를 보낸다. 위치는 절대 안 보낸다 — 그건 심판이 정한다. </summary>
		public void SendInput(VersusInputFrame frame, int tick)
		{
			if (transport.IsOpen == false)
				return;

			transport.Send(codec.Encode(new VersusInputMessage
			{
				tick = tick,
				moveX = frame.Move.x,
				moveY = frame.Move.y,
				aimX = frame.Aim.x,
				aimY = frame.Aim.y,
				fire = frame.Fire,
				dash = frame.Dash,
			}));
		}

		/// <summary> 카드를 골랐다고 알린다. </summary>
		public void SendPick(int offerIndex)
		{
			if (transport.IsOpen == false)
				return;

			transport.Send(codec.Encode(new VersusPickMessage { index = offerIndex }));
			Offer = null;
		}

		/// <summary> 「한 판 더」 하자고 말한다. 둘 다 말하면 심판이 새 판을 연다. </summary>
		public void SendRematch()
		{
			if (transport.IsOpen == false)
				return;

			transport.Send(codec.Encode(new VersusRematchMessage()));
		}

		/// <summary> 도착한 것을 모두 반영한다. 매 프레임 한 번 부르면 된다. </summary>
		public void Pump()
		{
			RoundJustEnded = false;
			transport.Drain(incoming);

			for (int index = 0; index < incoming.Count; index++)
				Apply(incoming[index]);
		}

		private void Apply(string message)
		{
			string type = codec.TypeOf(message);

			if (type == VersusMessageType.START)
			{
				VersusStartMessage start = codec.Decode<VersusStartMessage>(message);

				if (start != null)
					Seat = start.seat;

				return;
			}

			if (type == VersusMessageType.ROUND_START)
			{
				VersusRoundStartMessage start = codec.Decode<VersusRoundStartMessage>(message);

				if (start == null)
					return;

				// 심판과 <b>같은 판</b>을 스스로 짓는다 — 여기부터 내 조작이 즉시 반응한다.
				Predicted = new VersusRoundState(start.statsA, start.statsB, tuning,
					start.halfWidth, start.halfDepth,
					new Numerics.Vector2(start.spawnAX, start.spawnAY),
					new Numerics.Vector2(start.spawnBX, start.spawnBY));

				predictor = new VersusPredictor(Predicted, Seat);
				roundTimeLimitSeconds = start.roundTimeLimitSeconds;
				MatchWinner = VersusMatchCore.NO_WINNER;
				return;
			}

			if (type == VersusMessageType.SNAPSHOT)
			{
				VersusSnapshotMessage snapshot = codec.Decode<VersusSnapshotMessage>(message);

				if (snapshot == null || predictor == null)
					return;

				// 상대가 그 사이 무엇을 했는지 먼저 알려 준 뒤 되감는다 — 순서가 바뀌면 다시 굴릴 때 추측이 남는다.
				for (int index = 0; index < snapshot.opponentInputs.Length; index++)
				{
					VersusRemoteInput remote = snapshot.opponentInputs[index];
					predictor.ObserveOpponent(remote.tick, new VersusInputFrame
					{
						Move = new Numerics.Vector2(remote.moveX, remote.moveY),
						Aim = new Numerics.Vector2(remote.aimX, remote.aimY),
						Fire = remote.fire,
						Dash = remote.dash,
					});
				}

				predictor.ApplyAuthoritative(snapshot.snapshot, roundTimeLimitSeconds);
				ScoreMine = Seat == 0 ? snapshot.scoreA : snapshot.scoreB;
				ScoreTheirs = Seat == 0 ? snapshot.scoreB : snapshot.scoreA;
				return;
			}

			if (type == VersusMessageType.STATE)
			{
				VersusStateMessage state = codec.Decode<VersusStateMessage>(message);

				if (state == null)
					return;

				// 새 판이 서면 「이긴 사람」 표시가 남아 있으면 안 된다 — 그림이 다시 오는 것이 곧 새 판이다.
				if (MatchWinner != VersusMatchCore.NO_WINNER)
				{
					MatchWinner = VersusMatchCore.NO_WINNER;
					RematchReady = 0;
				}

				Fighters = state.fighters ?? new VersusBodyMessage[0];
				Shots = state.shots ?? new VersusBodyMessage[0];
				ScoreMine = Seat == 0 ? state.scoreA : state.scoreB;
				ScoreTheirs = Seat == 0 ? state.scoreB : state.scoreA;
				return;
			}

			if (type == VersusMessageType.ROUND_END)
			{
				VersusRoundEndMessage end = codec.Decode<VersusRoundEndMessage>(message);

				if (end == null)
					return;

				LastRoundWinner = end.winner;
				RoundJustEnded = true;
				ScoreMine = Seat == 0 ? end.scoreA : end.scoreB;
				ScoreTheirs = Seat == 0 ? end.scoreB : end.scoreA;
				return;
			}

			if (type == VersusMessageType.OFFER)
			{
				Offer = codec.Decode<VersusOfferMessage>(message);
				return;
			}

			if (type == VersusMessageType.MATCH_END)
			{
				VersusMatchEndMessage end = codec.Decode<VersusMatchEndMessage>(message);

				if (end != null)
					MatchWinner = end.winner;

				return;
			}

			if (type == VersusMessageType.REMATCH_STATE)
			{
				VersusRematchStateMessage rematch = codec.Decode<VersusRematchStateMessage>(message);

				if (rematch != null)
				{
					RematchReady = rematch.ready;
					RematchNeeded = rematch.needed;
				}

				return;
			}

			if (type == VersusMessageType.OPPONENT_LEFT)
				OpponentLeft = true;
		}
	}
}
