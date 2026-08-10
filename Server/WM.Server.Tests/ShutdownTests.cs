using System;
using System.Diagnostics;
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
	/// <b>서버가 제때 멎는가</b> (TASK-WM-217).
	///
	/// ★ 왜: 배포는 「멈추고 다시 켜기」다. 멎는 데 오래 걸리면 그 시간만큼 세계가 닫혀 있고,
	///   자동 배포는 그걸 「죽었다」로 오해해 두 번 죽이기도 한다.
	///   끊긴 창(와이파이가 나간 사람)이 남아 있을 때가 특히 위험하다 — 서버가 그 사람의
	///   다음 말을 <b>영원히</b> 기다릴 수 있기 때문이다.
	/// </summary>
	public sealed class ShutdownTests
	{
		private const int PORT = 5419;
		private static readonly Uri address = new Uri($"ws://127.0.0.1:{PORT}/ws");

		private WebApplication app;
		private string worldFile;

		[SetUp]
		public void SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-stop-" + Path.GetRandomFileName() + ".json");
		}

		[TearDown]
		public void TearDown()
		{
			foreach (string path in new[] { worldFile, worldFile + ".bak", worldFile + ".tmp" })
			{
				if (File.Exists(path))
					File.Delete(path);
			}
		}

		[Test]
		public async Task 아무도_없으면_곧바로_멎는다()
		{
			await StartAsync();

			Stopwatch clock = Stopwatch.StartNew();
			await StopAsync();
			clock.Stop();

			Assert.Less(clock.ElapsedMilliseconds, 5000,
				$"빈 세계를 닫는 데 {clock.ElapsedMilliseconds}ms 가 걸린다");
		}

		[Test]
		public async Task 사람이_붙어_있어도_제때_멎는다()
		{
			await StartAsync();

			using ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);
			await Read(window, "\"welcome\"");

			Stopwatch clock = Stopwatch.StartNew();
			await StopAsync();
			clock.Stop();

			Assert.Less(clock.ElapsedMilliseconds, 5000,
				$"사람 한 명 있는 세계를 닫는 데 {clock.ElapsedMilliseconds}ms 가 걸린다 — 배포마다 그만큼 멈춘다");
		}

		[Test]
		public async Task 끊긴_창이_있어도_제때_멎는다()
		{
			await StartAsync();

			// 인사 없이 사라진 창 — 서버는 이 사람의 다음 말을 기다리는 중이다.
			ClientWebSocket gone = new ClientWebSocket();
			await gone.ConnectAsync(address, CancellationToken.None);
			await Read(gone, "\"welcome\"");
			gone.Abort();

			await Task.Delay(300);

			Stopwatch clock = Stopwatch.StartNew();
			await StopAsync();
			clock.Stop();

			// ★ 여기가 실제로 오래 걸리던 자리다 (실측 2026-08-10: 재접속 시험 두 개가 각각 30초).
			Assert.Less(clock.ElapsedMilliseconds, 5000,
				$"끊긴 창 하나 때문에 닫는 데 {clock.ElapsedMilliseconds}ms 가 걸린다 — "
				+ "와이파이 나간 사람 하나가 배포를 붙잡는 셈이다");
		}

		private async Task StartAsync()
		{
			WorldHost host = new WorldHost(new WorldStore(worldFile));
			app = host.Build(Array.Empty<string>(), $"http://127.0.0.1:{PORT}");
			await app.StartAsync();
		}

		private async Task StopAsync()
		{
			if (app == null)
				return;

			await app.StopAsync();
			await app.DisposeAsync();
			app = null;
		}

		private static async Task<string> Read(ClientWebSocket socket, string needle)
		{
			using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			byte[] buffer = new byte[32768];
			StringBuilder pending = new StringBuilder();

			while (timeout.IsCancellationRequested == false)
			{
				WebSocketReceiveResult received;
				try
				{
					received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
				}
				catch (OperationCanceledException)
				{
					break;
				}

				if (received.MessageType == WebSocketMessageType.Close)
					break;

				pending.Append(Encoding.UTF8.GetString(buffer, 0, received.Count));
				if (received.EndOfMessage == false)
					continue;

				string text = pending.ToString();
				pending.Clear();

				if (text.Contains(needle))
					return text;
			}

			Assert.Fail($"「{needle}」 가 안 왔다.");
			return null;
		}
	}
}
