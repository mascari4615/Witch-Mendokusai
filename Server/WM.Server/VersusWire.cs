using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using WitchMendokusai;

namespace WitchMendokusai.Server
{
	/// <summary>
	/// 서버 쪽 <b>구멍</b> — 웹소켓 하나를 <see cref="IVersusTransport"/> 로 감싼다 (TASK-WM-411).
	/// 판정 층은 이게 웹소켓인지 P2P 인지 모른다. 여기가 그 경계다.
	/// </summary>
	public sealed class WebSocketVersusTransport : IVersusTransport
	{
		private readonly WebSocket socket;
		private readonly Queue<string> inbox = new Queue<string>();
		private readonly object gate = new object();

		public WebSocketVersusTransport(WebSocket socket)
		{
			this.socket = socket;
		}

		public bool IsOpen => socket != null && socket.State == WebSocketState.Open;

		public void Send(string message)
		{
			if (IsOpen == false)
				return;

			byte[] payload = Encoding.UTF8.GetBytes(message);

			// 보내기는 기다리지 않는다 — 심판의 시계가 한 창의 느린 회선에 끌려가면 안 된다.
			_ = socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, CancellationToken.None);
		}

		public void Drain(List<string> into)
		{
			into.Clear();

			lock (gate)
			{
				while (inbox.Count > 0)
					into.Add(inbox.Dequeue());
			}
		}

		/// <summary> 받기 루프가 넣어 준다. </summary>
		public void Deliver(string message)
		{
			lock (gate)
			{
				// 밀린 것이 너무 쌓이면 오래된 것부터 버린다 — 지나간 의도는 살릴 값이 없다.
				if (inbox.Count > 240)
					inbox.Dequeue();

				inbox.Enqueue(message);
			}
		}
	}

	/// <summary> 서버가 쓰는 글자 변환기. 유니티는 JsonUtility, 웹은 JSON 을 꽂는다 — 모양은 같다. </summary>
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
}
