using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 유니티 창에서 대결 서버(또는 P2P 호스트)로 이어지는 <b>줄</b> (TASK-WM-411).
	/// 판정 층이 보는 것은 <see cref="IVersusTransport"/> 뿐이라, 이 파일이 통째로 바뀌어도
	/// (웹소켓 → WebRTC 등) 규칙·심판·손님 코드는 한 줄도 안 바뀐다.
	///
	/// 받은 것은 큐에 쌓아 두고 <see cref="Drain"/> 에서 게임 스레드로 넘긴다 —
	/// 유니티 객체를 다른 스레드에서 만지면 그 자리에서 죽는다.
	/// </summary>
	public sealed class VersusClientLink : IVersusTransport, IDisposable
	{
		private readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
		private readonly ClientWebSocket socket = new ClientWebSocket();
		private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

		public bool IsOpen => socket.State == WebSocketState.Open;

		/// <summary>줄이 붙었나 — 화면에 「연결 중」을 띄우는 데 쓴다.</summary>
		public bool IsConnecting { get; private set; }

		/// <summary>못 붙었으면 왜 — 사람에게 그대로 보여 줄 수 있는 짧은 말.</summary>
		public string LastError { get; private set; } = string.Empty;

		/// <summary> 붙는다. 붙으면 곧바로 「끼워 줘」를 보낸다. </summary>
		public async void Connect(string url, string room, bool fillWithBot, IVersusCodec codec)
		{
			IsConnecting = true;

			try
			{
				await socket.ConnectAsync(new Uri(url), cancellation.Token);
				Send(codec.Encode(new Net.VersusJoinMessage { room = room ?? string.Empty, fillWithBot = fillWithBot }));
				ReceiveLoop();
			}
			catch (Exception exception)
			{
				LastError = exception.Message;
			}
			finally
			{
				IsConnecting = false;
			}
		}

		public void Send(string message)
		{
			if (IsOpen == false)
				return;

			byte[] payload = Encoding.UTF8.GetBytes(message);
			_ = socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, cancellation.Token);
		}

		public void Drain(List<string> into)
		{
			into.Clear();

			while (inbox.TryDequeue(out string message))
				into.Add(message);
		}

		public void Dispose()
		{
			cancellation.Cancel();
			socket.Dispose();
		}

		private async void ReceiveLoop()
		{
			byte[] buffer = new byte[8192];

			try
			{
				while (IsOpen && cancellation.IsCancellationRequested == false)
				{
					WebSocketReceiveResult result =
						await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation.Token);

					if (result.MessageType == WebSocketMessageType.Close)
						return;

					if (result.MessageType != WebSocketMessageType.Text)
						continue;

					inbox.Enqueue(Encoding.UTF8.GetString(buffer, 0, result.Count));
				}
			}
			catch (Exception exception)
			{
				LastError = exception.Message;
			}
		}
	}

	/// <summary>
	/// 유니티가 쓰는 글자 변환기 — <see cref="JsonUtility"/>. 서버는 System.Text.Json 을 쓰지만
	/// 말의 모양(DomainSDK/Net)이 같아서 오가는 글자는 같다(필드가 public 인 이유가 이것이다).
	/// </summary>
	public sealed class UnityVersusCodec : IVersusCodec
	{
		[Serializable]
		private class TypeProbe
		{
			public string type = string.Empty;
		}

		public string Encode(object message) => JsonUtility.ToJson(message);

		public string TypeOf(string message)
		{
			try
			{
				TypeProbe probe = JsonUtility.FromJson<TypeProbe>(message);
				return probe != null ? probe.type : string.Empty;
			}
			catch (ArgumentException)
			{
				return string.Empty;
			}
		}

		public T Decode<T>(string message) where T : class
		{
			try
			{
				return JsonUtility.FromJson<T>(message);
			}
			catch (ArgumentException)
			{
				return null;
			}
		}
	}
}
