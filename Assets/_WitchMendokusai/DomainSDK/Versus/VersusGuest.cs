using System.Collections.Generic;
using WitchMendokusai.Net;

namespace WitchMendokusai
{
	/// <summary>
	/// 심판이 아닌 쪽 (TASK-WM-411) — <b>의도를 보내고 그림을 받는다</b>. 규칙을 스스로 돌리지 않는다.
	///
	/// 심판이 서버든 상대 컴퓨터(P2P 호스트)든 이 코드는 같다. 나르는 방법도 모른다
	/// (<see cref="IVersusTransport"/> 가 밖에서 꽂힌다) — 그래서 유니티·웹이 이걸 그대로 쓴다.
	/// </summary>
	public sealed class VersusGuest
	{
		private readonly IVersusTransport transport;
		private readonly IVersusCodec codec;
		private readonly List<string> incoming = new List<string>();

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

		/// <summary>방금 라운드가 끝났다면 그 승자(한 번만 참). 화면 연출에 쓴다.</summary>
		public int LastRoundWinner { get; private set; } = VersusMatchCore.NO_WINNER;
		public bool RoundJustEnded { get; private set; }

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

			if (type == VersusMessageType.STATE)
			{
				VersusStateMessage state = codec.Decode<VersusStateMessage>(message);

				if (state == null)
					return;

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

			if (type == VersusMessageType.OPPONENT_LEFT)
				OpponentLeft = true;
		}
	}
}
