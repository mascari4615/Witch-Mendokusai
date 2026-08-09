using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using NUnit.Framework;
using WitchMendokusai.Server;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// <b>둘이 붙으면 서로 보이나</b> — WS 판 2-peer 스모크 (TASK-WM-217 단계 4).
	///
	/// ★ 이게 서기 전에는 FishNet 을 지우지 않는다. FishNet 은 지금 유일하게 「둘이 만나 같이 걷는다」가
	///   라이브로 확인된 통로다 — 대체품이 같은 것을 <b>기계로</b> 증명해야 지울 자격이 생긴다.
	///
	/// 진짜 소켓으로 진짜 서버에 붙는다(가짜 전송·목 없음). 빈 포트를 써서 다른 시험과 안 부딪힌다.
	/// </summary>
	public sealed class TwoPeerSmokeTests
	{
		private const int PORT = 5391;
		private static readonly Uri address = new Uri($"ws://127.0.0.1:{PORT}/ws");

		private WebApplication app;
		private string worldFile;

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-smoke-" + Path.GetRandomFileName() + ".json");

			// 시험마다 자기 세계·자기 저장 파일 — 서로를 오염시키지 않는다.
			WorldHost host = new WorldHost(new WorldStore(worldFile));
			app = host.Build(Array.Empty<string>(), $"http://127.0.0.1:{PORT}");
			await app.StartAsync();
		}

		[TearDown]
		public async Task TearDown()
		{
			if (app != null)
			{
				await app.StopAsync();
				await app.DisposeAsync();
				app = null;
			}

			if (File.Exists(worldFile))
				File.Delete(worldFile);
		}

		[Test]
		public async Task 둘이_붙으면_서로가_보인다()
		{
			using ClientWebSocket first = await ConnectAsync();
			using ClientWebSocket second = await ConnectAsync();

			int firstId = await ReadWelcomeAsync(first);
			int secondId = await ReadWelcomeAsync(second);

			Assert.AreNotEqual(firstId, secondId, "인형 번호는 사람마다 달라야 한다.");

			// 한쪽이 움직이면 다른 쪽 화면에 그게 보인다 — 이게 「같은 세계」의 최소 증거다.
			await SendAsync(first, "{\"type\":\"move\",\"x\":1.0,\"z\":0.0}");

			string snapshot = await WaitForAsync(second, text =>
				text.Contains("\"type\":\"world\"") &&
				text.Contains("\"id\":" + firstId) &&
				text.Contains("\"id\":" + secondId) &&
				text.Contains("\"x\":1.000"));

			StringAssert.Contains("\"buildings\"", snapshot);
		}

		[Test]
		public async Task 한쪽이_지으면_다른_쪽에도_선다()
		{
			using ClientWebSocket builder = await ConnectAsync();
			using ClientWebSocket watcher = await ConnectAsync();
			await ReadWelcomeAsync(builder);
			await ReadWelcomeAsync(watcher);

			await SendAsync(builder, "{\"type\":\"place\",\"x\":3,\"y\":0,\"z\":4,\"w\":1,\"l\":1,\"buildingId\":77}");

			await WaitForAsync(watcher, text => text.Contains("\"buildingId\":77"));

			// 부수면 다른 쪽에서도 사라진다.
			await SendAsync(builder, "{\"type\":\"remove\",\"x\":3,\"y\":0,\"z\":4}");
			await WaitForAsync(watcher, text =>
				text.Contains("\"type\":\"world\"") && text.Contains("\"buildingId\":77") == false);
		}

		[Test]
		public async Task 한쪽이_저으면_같은_솥에_쌓인다()
		{
			using ClientWebSocket stirrer = await ConnectAsync();
			using ClientWebSocket watcher = await ConnectAsync();
			await ReadWelcomeAsync(stirrer);
			await ReadWelcomeAsync(watcher);

			await SendAsync(stirrer, "{\"type\":\"brew\",\"dx\":1.0,\"dy\":0.0,\"grind\":1.0}");
			await SendAsync(stirrer, "{\"type\":\"brew\",\"dx\":0.0,\"dy\":1.0,\"grind\":1.0}");

			// 두 번 저은 것이 다른 쪽 화면에도 두 번으로 보인다(마커뿐 아니라 저은 길까지).
			await WaitForAsync(watcher, text => text.Contains("\"steps\":2") && text.Contains("\"path\":[{"));
		}

		[Test]
		public async Task 세계의_시각이_모두에게_같이_간다()
		{
			using ClientWebSocket peer = await ConnectAsync();
			await ReadWelcomeAsync(peer);

			await WaitForAsync(peer, text => text.Contains("\"time\":{\"year\":"));
		}

		private static async Task<ClientWebSocket> ConnectAsync()
		{
			ClientWebSocket socket = new ClientWebSocket();
			using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			await socket.ConnectAsync(address, timeout.Token);
			return socket;
		}

		private static async Task SendAsync(ClientWebSocket socket, string json)
		{
			byte[] payload = Encoding.UTF8.GetBytes(json);
			await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, CancellationToken.None);
		}

		private static async Task<int> ReadWelcomeAsync(ClientWebSocket socket)
		{
			string welcome = await WaitForAsync(socket, text => text.Contains("\"type\":\"welcome\""));
			int marker = welcome.IndexOf("\"id\":", StringComparison.Ordinal) + 5;
			int end = welcome.IndexOf('}', marker);
			return int.Parse(welcome.Substring(marker, end - marker));
		}

		/// <summary>
		/// 그 조건을 만족하는 말이 올 때까지 듣는다. 안 오면 <b>기다리다 실패한다</b> —
		/// 「받았겠지」로 넘어가면 조용히 안 되는 통로가 초록으로 보인다.
		/// </summary>
		private static async Task<string> WaitForAsync(ClientWebSocket socket, Func<string, bool> matches)
		{
			using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			byte[] buffer = new byte[16384];

			while (timeout.IsCancellationRequested == false)
			{
				WebSocketReceiveResult received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
				if (received.MessageType == WebSocketMessageType.Close)
					break;

				string text = Encoding.UTF8.GetString(buffer, 0, received.Count);
				if (matches(text))
					return text;
			}

			Assert.Fail("기다리던 말이 10초 안에 안 왔다 — 통로가 조용히 죽었다는 뜻이다.");
			return null;
		}
	}
}
