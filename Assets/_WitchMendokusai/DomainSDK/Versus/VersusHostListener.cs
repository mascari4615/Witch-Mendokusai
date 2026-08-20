using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WitchMendokusai
{
	/// <summary>
	/// <b>서버 없이</b> 친구를 직접 받는 문 (TASK-WM-411, P2P 호스트).
	///
	/// 한쪽 창이 이걸 열면 그 창이 곧 심판이다(<see cref="VersusAuthority"/>) — 서버비 0, 지연 최소.
	/// 대신 그 사람이 켜 있어야 하고, 집 밖에서 붙으려면 포트를 열어야 한다. 그래서 서버 방식과 <b>둘 다</b> 둔다:
	/// 판정·심판·손님 코드는 하나고, 여기와 서버는 그 코드에 꽂히는 <b>문</b>이 다를 뿐이다.
	///
	/// 엔진을 안 쓴다(유니티·서버·웹 도구 어디서나 그대로 돈다).
	/// </summary>
	public sealed class VersusHostListener : IDisposable
	{
		private readonly HttpListener listener = new HttpListener();
		private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
		private readonly ConcurrentQueue<VersusSocketTransport> arrivals = new ConcurrentQueue<VersusSocketTransport>();

		/// <summary>친구에게 알려 줄 주소. 같은 집이면 이 주소 그대로 붙는다.</summary>
		public string Url { get; }

		public bool IsListening => listener.IsListening;

		/// <summary>못 열었으면 왜 — 포트가 이미 쓰이거나 권한이 없을 때.</summary>
		public string LastError { get; private set; } = string.Empty;

		/// <param name="openToNetwork">
		/// false(기본) = 이 컴퓨터 안에서만 받는다. 관리자 권한이 필요 없다.
		/// true = 밖에서도 받는다(친구가 진짜로 붙는 길). 윈도우에서는 <b>관리자 권한 또는 urlacl 등록</b>이 필요하다:
		/// <c>netsh http add urlacl url=http://+:PORT/vs/ user=Everyone</c> — 안 하면 <see cref="Start"/> 가 false 를 낸다.
		/// </param>
		public VersusHostListener(int port, string path = "/vs/", bool openToNetwork = false)
		{
			Url = openToNetwork ? $"http://+:{port}{path}" : $"http://localhost:{port}{path}";
			listener.Prefixes.Add(Url);
		}

		/// <summary> 문을 연다. 실패하면 <see cref="LastError"/> 에 이유가 남는다(예외로 판을 세우지 않는다). </summary>
		public bool Start()
		{
			try
			{
				listener.Start();
				AcceptLoop();
				return true;
			}
			catch (HttpListenerException exception)
			{
				LastError = exception.Message;
				return false;
			}
		}

		/// <summary> 새로 들어온 손님이 있으면 준다. 없으면 null — 매 프레임 물어보면 된다. </summary>
		public IVersusTransport TryAccept()
		{
			return arrivals.TryDequeue(out VersusSocketTransport transport) ? transport : null;
		}

		public void Dispose()
		{
			cancellation.Cancel();

			if (listener.IsListening)
				listener.Stop();

			listener.Close();
		}

		// ★ 모든 await 에 ConfigureAwait(false) — 지우지 마라 (TASK-WM-414).
		// 유니티의 SynchronizationContext 는 이어달리기를 *메인 스레드*로 돌려보낸다. 이 루프가
		// 그러면 「메인 스레드가 붙기를 기다리는 동안 수락이 못 돈다」가 되어 서로를 막는다.
		// 실측(2026-08-20): EditMode 테스트 1개가 에디터를 영구 정지시켰다 — 강제 종료로만 복구.
		// 런타임에도 같은 이유로 위험하다(한 프레임이 길어지면 접속 수락이 밀린다).
		private async void AcceptLoop()
		{
			while (listener.IsListening && cancellation.IsCancellationRequested == false)
			{
				HttpListenerContext context;

				try
				{
					context = await listener.GetContextAsync().ConfigureAwait(false);
				}
				catch (Exception exception)
				{
					LastError = exception.Message;
					return;
				}

				if (context.Request.IsWebSocketRequest == false)
				{
					context.Response.StatusCode = 400;
					context.Response.Close();
					continue;
				}

				HttpListenerWebSocketContext socketContext = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
				VersusSocketTransport transport = new VersusSocketTransport(socketContext.WebSocket, cancellation.Token);
				arrivals.Enqueue(transport);
			}
		}
	}

	/// <summary>
	/// 웹소켓 하나를 <see cref="IVersusTransport"/> 로 감싼다 — 호스트 쪽·손님 쪽 양쪽에서 쓴다.
	/// 받은 것은 큐에 쌓아 두고 <see cref="Drain"/> 에서 넘긴다(다른 스레드에서 판을 만지지 않게).
	/// </summary>
	public sealed class VersusSocketTransport : IVersusTransport, IDisposable
	{
		private readonly WebSocket socket;
		private readonly CancellationToken cancellation;
		private readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();

		public VersusSocketTransport(WebSocket socket, CancellationToken cancellation)
		{
			this.socket = socket;
			this.cancellation = cancellation;
			ReceiveLoop();
		}

		public bool IsOpen => socket != null && socket.State == WebSocketState.Open;

		public void Send(string message)
		{
			if (IsOpen == false)
				return;

			byte[] payload = Encoding.UTF8.GetBytes(message);

			// 기다리지 않는다 — 심판의 시계가 한 사람의 느린 회선에 끌려가면 안 된다.
			_ = socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, cancellation);
		}

		public void Drain(List<string> into)
		{
			into.Clear();

			while (inbox.TryDequeue(out string message))
				into.Add(message);
		}

		public void Dispose()
		{
			socket?.Dispose();
		}

		private async void ReceiveLoop()
		{
			byte[] buffer = new byte[8192];

			try
			{
				while (IsOpen && cancellation.IsCancellationRequested == false)
				{
					WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation).ConfigureAwait(false);

					if (result.MessageType == WebSocketMessageType.Close)
						return;

					if (result.MessageType != WebSocketMessageType.Text)
						continue;

					// 밀린 것이 너무 쌓이면 오래된 것부터 버린다 — 지나간 의도는 살릴 값이 없다.
					if (inbox.Count > 240)
						inbox.TryDequeue(out _);

					inbox.Enqueue(Encoding.UTF8.GetString(buffer, 0, result.Count));
				}
			}
			catch (Exception)
			{
				// 줄이 갑자기 끊기는 것은 일상이다 — 판은 「상대가 나갔다」로 처리한다.
			}
		}
	}
}
