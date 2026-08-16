using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WitchMendokusai;
using WitchMendokusai.Net;

namespace WitchMendokusai.Server
{
	/// <summary>
	/// 1대1 대결의 <b>심판</b> (TASK-WM-411). 두 창을 한 방에 짝지어 두고, 규칙을 돌리고,
	/// 그 결과만 두 창에 되돌려 준다.
	///
	/// ★ 규칙은 여기에 없다 — <see cref="VersusRoundState"/>·<see cref="VersusMatchCore"/>(DomainSDK)를 그대로 쓴다.
	///   이 서버의 원칙이 「게임 규칙을 다시 구현하지 않는다」이고, 대결은 특히 그래야 한다:
	///   두 사람이 각자 자기 화면에서 이겼다고 하면 판 자체가 안 선다. <b>심판은 하나</b>여야 한다.
	///
	/// 창은 <b>의도</b>만 보낸다(누른 방향·겨눈 쪽). 위치는 서버가 정해서 내려 준다.
	/// </summary>
	public sealed class VersusHost
	{
		/// <summary>판정 틱 — DomainSDK 와 같은 60Hz. 여기서 어긋나면 화면과 서버가 다른 게임을 한다.</summary>
		private const int TICK_HZ = 60;

		/// <summary>화면에 보내는 횟수. 판정(60)보다 성기게 보낸다 — 사람 눈에는 충분하고 줄은 3배 가볍다.</summary>
		private const int SNAPSHOT_HZ = 20;

		/// <summary>상대를 이만큼 기다려도 안 오면 봇으로 채운다(방을 만들 때 청한 경우만).</summary>
		private const double BOT_FILL_SECONDS = 8;

		private static readonly JsonSerializerOptions JSON = new JsonSerializerOptions { IncludeFields = true };

		private readonly ConcurrentDictionary<string, VersusRoom> rooms = new ConcurrentDictionary<string, VersusRoom>();

		/// <summary> 지금 서 있는 방 수 — 건강검사에서 「대결이 몇 판 도나」를 보려고. </summary>
		public int RoomCount => rooms.Count;

		/// <summary> 창 하나를 받는다. 방이 차면 판이 시작되고, 이 함수는 그 창이 나갈 때까지 산다. </summary>
		public async Task ServeAsync(WebSocket socket, CancellationToken stopping)
		{
			VersusSeat seat = null;

			try
			{
				VersusJoinMessage join = await ReadJoinAsync(socket, stopping);

				if (join == null)
					return;

				string roomName = string.IsNullOrWhiteSpace(join.room) ? FindOpenRoom() : join.room.Trim();
				VersusRoom room = rooms.GetOrAdd(roomName, name => new VersusRoom(name, this));
				seat = room.TryTake(socket, join.fillWithBot);

				if (seat == null)
				{
					// 방이 이미 둘로 찼다 — 구경은 아직 없다. 조용히 끊지 말고 이유를 말한다.
					await SendAsync(socket, new DeniedMessage { what = VersusMessageType.JOIN, why = "방이 찼다" }, stopping);
					return;
				}

				await ReceiveLoopAsync(seat, stopping);
			}
			catch (OperationCanceledException)
			{
			}
			catch (WebSocketException)
			{
				// 창이 갑자기 닫히는 것은 일상이다 — 방만 정리하면 된다.
			}
			finally
			{
				if (seat != null)
					seat.Room.Leave(seat);
			}
		}

		internal void Remove(VersusRoom room)
		{
			rooms.TryRemove(room.Name, out _);
		}

		// 빈자리가 있는 방을 찾는다. 없으면 새 이름을 만든다 — 「아무나랑」 붙는 길.
		private string FindOpenRoom()
		{
			foreach (KeyValuePair<string, VersusRoom> pair in rooms)
			{
				if (pair.Value.HasOpenSeat)
					return pair.Key;
			}

			return "vs-" + Guid.NewGuid().ToString("N").Substring(0, 6);
		}

		private static async Task<VersusJoinMessage> ReadJoinAsync(WebSocket socket, CancellationToken stopping)
		{
			byte[] buffer = new byte[4096];
			WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), stopping);

			if (result.MessageType != WebSocketMessageType.Text)
				return null;

			string text = Encoding.UTF8.GetString(buffer, 0, result.Count);

			try
			{
				VersusJoinMessage join = JsonSerializer.Deserialize<VersusJoinMessage>(text, JSON);
				return join != null && join.type == VersusMessageType.JOIN ? join : null;
			}
			catch (JsonException)
			{
				return null;
			}
		}

		private async Task ReceiveLoopAsync(VersusSeat seat, CancellationToken stopping)
		{
			byte[] buffer = new byte[4096];

			while (seat.Socket.State == WebSocketState.Open && stopping.IsCancellationRequested == false)
			{
				WebSocketReceiveResult result = await seat.Socket.ReceiveAsync(new ArraySegment<byte>(buffer), stopping);

				if (result.MessageType == WebSocketMessageType.Close)
					return;

				if (result.MessageType != WebSocketMessageType.Text)
					continue;

				string text = Encoding.UTF8.GetString(buffer, 0, result.Count);
				seat.Room.Handle(seat, text);
			}
		}

		internal static async Task SendAsync(WebSocket socket, object message, CancellationToken stopping)
		{
			if (socket == null || socket.State != WebSocketState.Open)
				return;

			byte[] payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, message.GetType(), JSON));

			try
			{
				await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, stopping);
			}
			catch (WebSocketException)
			{
			}
			catch (OperationCanceledException)
			{
			}
		}

		internal static int TickHz => TICK_HZ;
		internal static int SnapshotHz => SNAPSHOT_HZ;
		internal static double BotFillSeconds => BOT_FILL_SECONDS;
		internal static JsonSerializerOptions JsonOptions => JSON;
	}

	/// <summary> 방에 앉은 한 사람(또는 빈 의자). </summary>
	public sealed class VersusSeat
	{
		public VersusSeat(VersusRoom room, WebSocket socket, int index)
		{
			Room = room;
			Socket = socket;
			Index = index;
		}

		public VersusRoom Room { get; }
		public WebSocket Socket { get; }
		public int Index { get; }

		/// <summary>가장 최근에 받은 의도. 창이 조용하면 마지막 것을 이어 쓴다(뚝뚝 끊기는 것보다 낫다).</summary>
		public VersusInputFrame Frame;

		/// <summary>이 사람이 고른 카드 번호. -1 = 아직 안 골랐다.</summary>
		public int PickedOffer = -1;
	}
}
