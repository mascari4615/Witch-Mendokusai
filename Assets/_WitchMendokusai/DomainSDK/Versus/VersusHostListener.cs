using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
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
		// ★ 왜 HttpListener 가 아닌가 (TASK-WM-415) — 유니티(Mono) 런타임에서 그 길은 막혀 있다.
		//   실측 2026-08-21: 요청 헤더는 멀쩡히 오는데(Upgrade/Connection/Sec-WebSocket-Key)
		//   HttpListenerRequest.IsWebSocketRequest 가 False 를 내고,
		//   HttpListenerContext.AcceptWebSocketAsync 는 NotImplementedException 을 던진다.
		//   그래서 TcpListener 로 직접 받아 RFC6455 악수를 손으로 한 뒤,
		//   그 스트림을 WebSocket.CreateFromStream 에 넘긴다(이건 이 런타임에서 정상 동작한다).
		private const string WEBSOCKET_GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

		private readonly TcpListener listener;
		private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
		private readonly ConcurrentQueue<VersusSocketTransport> arrivals = new ConcurrentQueue<VersusSocketTransport>();
		private readonly string path;
		private readonly bool localOnly;

		/// <summary>친구에게 알려 줄 주소. 같은 집이면 이 주소 그대로 붙는다.</summary>
		public string Url { get; }

		public bool IsListening { get; private set; }

		/// <summary>못 열었으면 왜 — 포트가 이미 쓰이거나 권한이 없을 때.</summary>
		public string LastError { get; private set; } = string.Empty;

		/// <param name="openToNetwork">
		/// false(기본) = 이 컴퓨터 안에서만 받는다. true = 밖에서도 받는다(친구가 진짜로 붙는 길).
		/// TcpListener 라 윈도우 urlacl 등록은 필요 없다 — 방화벽만 열면 된다.
		/// </param>
		public VersusHostListener(int port, string path = "/vs/", bool openToNetwork = false)
		{
			this.path = path;
			Url = openToNetwork ? $"ws://+:{port}{path}" : $"ws://localhost:{port}{path}";

			// ★ 언제나 IPv6Any + DualMode 로 연다 — 그래야 127.0.0.1 과 ::1 이 **둘 다** 붙는다.
			//   (IPv6Loopback 에 DualMode 를 걸어도 IPv4 는 안 붙는다 — 실측 2026-08-21.)
			//   집 안에서만 받을 때는 문을 좁히는 대신 손님 주소를 보고 되돌려보낸다(AcceptLoop).
			localOnly = openToNetwork == false;
			listener = new TcpListener(IPAddress.IPv6Any, port);
			listener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
		}

		/// <summary> 문을 연다. 실패하면 <see cref="LastError"/> 에 이유가 남는다(예외로 판을 세우지 않는다). </summary>
		public bool Start()
		{
			try
			{
				listener.Start();
				IsListening = true;
				AcceptLoop();
				return true;
			}
			catch (SocketException exception)
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
			IsListening = false;

			try
			{
				listener.Stop();
			}
			catch (SocketException)
			{
				// 이미 닫혔다 — 치우는 길에서 판을 세우지 않는다.
			}
		}

		// ★ 모든 await 에 ConfigureAwait(false) — 지우지 마라 (TASK-WM-414).
		// 유니티의 SynchronizationContext 는 이어달리기를 *메인 스레드*로 돌려보낸다. 이 루프가
		// 그러면 「메인 스레드가 붙기를 기다리는 동안 수락이 못 돈다」가 되어 서로를 막는다.
		// 실측(2026-08-20): EditMode 테스트 1개가 에디터를 영구 정지시켰다 — 강제 종료로만 복구.
		// 런타임에도 같은 이유로 위험하다(한 프레임이 길어지면 접속 수락이 밀린다).
		private async void AcceptLoop()
		{
			while (IsListening && cancellation.IsCancellationRequested == false)
			{
				TcpClient client;

				try
				{
					client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
				}
				catch (Exception exception)
				{
					LastError = exception.Message;
					return;
				}

				// 집 안에서만 받기로 했으면 바깥 손님은 여기서 돌려보낸다.
				if (localOnly == true && IsLoopback(client) == false)
				{
					client.Close();
					continue;
				}

				// 손님 하나가 악수하다 넘어져도 문은 계속 열어 둔다.
				HandshakeAsync(client);
			}
		}

		private async void HandshakeAsync(TcpClient client)
		{
			try
			{
				NetworkStream stream = client.GetStream();
				string request = await ReadHeadersAsync(stream).ConfigureAwait(false);
				string key = FindHeader(request, "Sec-WebSocket-Key");

				if (string.IsNullOrEmpty(key) || request.StartsWith("GET ", StringComparison.Ordinal) == false)
				{
					// 400 을 *끝까지 흘려보낸 뒤* 닫는다 — 바로 Close 하면 상대는 「응답 없음」으로 본다(실측).
					await RefuseAsync(stream, "400 Bad Request").ConfigureAwait(false);
					client.Client.Shutdown(SocketShutdown.Send);
					client.Close();
					return;
				}

				string accept = Convert.ToBase64String(
					System.Security.Cryptography.SHA1.Create()
						.ComputeHash(Encoding.UTF8.GetBytes(key + WEBSOCKET_GUID)));

				string response =
					"HTTP/1.1 101 Switching Protocols\r\n" +
					"Upgrade: websocket\r\n" +
					"Connection: Upgrade\r\n" +
					$"Sec-WebSocket-Accept: {accept}\r\n\r\n";

				byte[] bytes = Encoding.UTF8.GetBytes(response);
				await stream.WriteAsync(bytes, 0, bytes.Length, cancellation.Token).ConfigureAwait(false);

				WebSocket socket = WebSocket.CreateFromStream(stream, true, null, TimeSpan.FromSeconds(30));
				arrivals.Enqueue(new VersusSocketTransport(socket, cancellation.Token));
			}
			catch (Exception exception)
			{
				LastError = exception.Message;
				client.Close();
			}
		}

		/// <summary> 빈 줄이 나올 때까지가 머리다. 악수 한 번뿐이라 한 바이트씩 읽어도 싸다. </summary>
		private static async Task<string> ReadHeadersAsync(NetworkStream stream)
		{
			StringBuilder text = new StringBuilder();
			byte[] one = new byte[1];

			// 머리가 8KB 를 넘으면 우리가 아는 손님이 아니다 — 끝없이 읽지 않는다.
			while (text.Length < 8192 && text.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal) == false)
			{
				int read = await stream.ReadAsync(one, 0, 1).ConfigureAwait(false);

				if (read == 0)
					break;

				text.Append((char)one[0]);
			}

			return text.ToString();
		}

		private static string FindHeader(string request, string name)
		{
			foreach (string line in request.Split('\n'))
			{
				int colon = line.IndexOf(':');

				if (colon <= 0)
					continue;

				if (string.Equals(line.Substring(0, colon).Trim(), name, StringComparison.OrdinalIgnoreCase))
					return line.Substring(colon + 1).Trim();
			}

			return string.Empty;
		}

		private static async Task RefuseAsync(NetworkStream stream, string status)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(
				$"HTTP/1.1 {status}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
			await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
			await stream.FlushAsync().ConfigureAwait(false);
		}

		/// <summary> 손님이 이 컴퓨터 안에서 온 것인가 (IPv4 매핑 주소도 loopback 으로 센다). </summary>
		private static bool IsLoopback(TcpClient client)
		{
			IPEndPoint endpoint = client.Client.RemoteEndPoint as IPEndPoint;

			if (endpoint == null)
				return false;

			IPAddress address = endpoint.Address;

			if (address.IsIPv4MappedToIPv6 == true)
				address = address.MapToIPv4();

			return IPAddress.IsLoopback(address);
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
