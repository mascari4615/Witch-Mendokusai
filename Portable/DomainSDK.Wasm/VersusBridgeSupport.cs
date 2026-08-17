using System.Collections.Generic;
using System.Text.Json;
using WitchMendokusai;

namespace WitchMendokusai.Wasm
{
	/// <summary>
	/// 브라우저용 구멍 — 웹소켓은 <b>자바스크립트가</b> 잡고, 여기는 글자만 주고받는다 (TASK-WM-411).
	/// 판정 층이 보는 것은 <see cref="IVersusTransport"/> 뿐이라 이 얇은 껍데기로 충분하다.
	/// </summary>
	public sealed class VersusSocketBridgeTransport : IVersusTransport
	{
		private readonly Queue<string> inbox = new Queue<string>();
		private readonly Queue<string> outbox = new Queue<string>();

		public bool IsOpen { get; set; } = true;

		public void Send(string message)
		{
			outbox.Enqueue(message);
		}

		public void Drain(List<string> into)
		{
			into.Clear();

			while (inbox.Count > 0)
				into.Add(inbox.Dequeue());
		}

		/// <summary> 자바스크립트가 받은 줄을 넣는다. </summary>
		public void Deliver(string message)
		{
			inbox.Enqueue(message);
		}

		/// <summary> 보낼 줄을 꺼내 간다(여러 개면 줄바꿈으로 잇는다). </summary>
		public string TakeOutgoing()
		{
			if (outbox.Count == 0)
				return string.Empty;

			string joined = string.Join("\n", outbox);
			outbox.Clear();
			return joined;
		}
	}

	/// <summary> 브라우저가 쓰는 글자 변환기 — 서버와 같은 도구(System.Text.Json). </summary>
	public sealed class JsonVersusCodec : IVersusCodec
	{
		private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };

		public string Encode(object message) => JsonSerializer.Serialize(message, message.GetType(), Options);

		public string TypeOf(string message)
		{
			try
			{
				using JsonDocument document = JsonDocument.Parse(message);
				return document.RootElement.TryGetProperty("type", out JsonElement type) ? type.GetString() : string.Empty;
			}
			catch (JsonException)
			{
				return string.Empty;
			}
		}

		public T Decode<T>(string message) where T : class
		{
			try
			{
				return JsonSerializer.Deserialize<T>(message, Options);
			}
			catch (JsonException)
			{
				return null;
			}
		}
	}

	/// <summary> 그릴 것만 담은 꾸러미 — 캔버스가 이 모양만 알면 된다. </summary>
	public sealed class VersusViewPacket
	{
		public int seat;
		public float halfWidth;
		public float halfDepth;
		public VersusBody[] fighters = new VersusBody[0];
		public VersusBody[] shots = new VersusBody[0];

		public sealed class VersusBody
		{
			public float x;
			public float y;
			public float r;
			public int owner;
			public bool alive;
		}

		public static VersusViewPacket From(VersusGuest guest)
		{
			VersusRoundState round = guest.Predicted;
			VersusViewPacket packet = new VersusViewPacket
			{
				seat = guest.Seat,
				halfWidth = VersusDuelSim.ARENA_HALF_WIDTH,
				halfDepth = VersusDuelSim.ARENA_HALF_DEPTH,
				fighters = new VersusBody[MatchConstants.VERSUS_PLAYER_COUNT],
			};

			for (int index = 0; index < packet.fighters.Length; index++)
			{
				Numerics.Vector2 position = round.PositionOf(index);
				packet.fighters[index] = new VersusBody
				{
					x = position.x,
					y = position.y,
					r = round.RadiusOf(index),
					owner = index,
					alive = round.IsAlive(index),
				};
			}

			List<VersusBodyView> shots = new List<VersusBodyView>();
			round.CollectShots(shots);
			packet.shots = new VersusBody[shots.Count];

			for (int index = 0; index < shots.Count; index++)
			{
				packet.shots[index] = new VersusBody
				{
					x = shots[index].Position.x,
					y = shots[index].Position.y,
					r = shots[index].Radius,
					owner = shots[index].Owner,
					alive = true,
				};
			}

			return packet;
		}
	}

	/// <summary> 화면 위쪽 표시 — 점수·카드 후보·끝났나·되감은 횟수. </summary>
	public sealed class VersusHudPacket
	{
		public int scoreMine;
		public int scoreTheirs;
		public int matchWinner = MatchConstants.NO_WINNER;
		public bool opponentLeft;
		public int rollbacks;
		public int rematchReady;
		public int rematchNeeded;
		public string[] offer = new string[0];

		public static VersusHudPacket From(VersusGuest guest)
		{
			return new VersusHudPacket
			{
				scoreMine = guest.ScoreMine,
				scoreTheirs = guest.ScoreTheirs,
				matchWinner = guest.MatchWinner,
				opponentLeft = guest.OpponentLeft,
				rollbacks = guest.RollbackCount,
				rematchReady = guest.RematchReady,
				rematchNeeded = guest.RematchNeeded,
				offer = guest.Offer != null ? guest.Offer.texts : new string[0],
			};
		}
	}
}
