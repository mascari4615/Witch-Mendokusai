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
	/// 사람이 몰리고 <b>저장이 돌아도</b> 세계가 멎지 않는다 (TASK-WM-242).
	///
	/// ★ 왜 이 자리인가: 세계는 5초마다 자기를 통째로 파일에 적는다. 사람이 늘수록 그 파일이
	///   커지므로, 적는 동안 세계가 멎으면 <b>모두가 동시에</b> 끊긴 느낌을 받는다.
	///   평균 Hz 는 그런 순간을 감춘다(20Hz 중 한 번 200ms 멎어도 평균은 멀쩡하다).
	///   그래서 <b>가장 크게 벌어진 순간</b>을 본다.
	///
	/// ★ 기준을 1초로 둔 이유: 이건 <b>제품 주장</b>이다 — 「세계가 1초씩 멎지는 않는다」.
	///   느린 기계에서 몇십 ms 더 벌어지는 것은 환경이지 고장이 아니다(실측: 800명에서 94ms).
	/// </summary>
	public sealed class WorldNeverFreezesTests
	{
		private const int PORT = 5416;
		private const int CROWD = 60;
		private const long LONGEST_ALLOWED_MS = 1000;

		private static readonly Uri address = new Uri($"ws://127.0.0.1:{PORT}/ws");

		private WebApplication app;
		private WorldHost host;
		private string worldFile;

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-freeze-" + Path.GetRandomFileName() + ".json");
			host = new WorldHost(new WorldStore(worldFile));
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

			foreach (string path in new[] { worldFile, worldFile + ".bak", worldFile + ".tmp" })
			{
				if (File.Exists(path))
					File.Delete(path);
			}
		}

		[Test]
		public async Task 사람이_몰리고_저장이_돌아도_세계가_1초씩_멎지_않는다()
		{
			ClientWebSocket[] crowd = new ClientWebSocket[CROWD];

			try
			{
				for (int i = 0; i < CROWD; i++)
					crowd[i] = await JoinAsync();

				// 저장은 5초마다다 — 그보다 길게 걸어야 그 순간이 이 창(window) 안에 들어온다.
				using CancellationTokenSource walking = new CancellationTokenSource();
				_ = KeepWalkingAsync(crowd, walking.Token);
				await Task.Delay(7000);
				walking.Cancel();

				Assert.IsTrue(File.Exists(worldFile), "이 시험은 저장이 도는 동안을 보는 것이다");
				Assert.LessOrEqual(host.LongestTickGapMs, LONGEST_ALLOWED_MS,
					$"세계가 한 번에 {host.LongestTickGapMs}ms 멎었다 — 모두가 동시에 끊긴 느낌을 받는다");
			}
			finally
			{
				foreach (ClientWebSocket one in crowd)
				{
					if (one != null)
						one.Dispose();
				}
			}
		}

		private static async Task<ClientWebSocket> JoinAsync()
		{
			ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);
			byte[] hello = Encoding.UTF8.GetBytes("{\"type\":\"hello\",\"secret\":\"\"}");
			await window.SendAsync(new ArraySegment<byte>(hello), WebSocketMessageType.Text, true, CancellationToken.None);
			_ = KeepReadingAsync(window);
			return window;
		}

		private static async Task KeepWalkingAsync(ClientWebSocket[] crowd, CancellationToken stopping)
		{
			byte[] step = Encoding.UTF8.GetBytes("{\"type\":\"move\",\"x\":0.12,\"z\":0}");
			byte[] back = Encoding.UTF8.GetBytes("{\"type\":\"move\",\"x\":-0.12,\"z\":0}");
			bool forward = true;

			try
			{
				while (stopping.IsCancellationRequested == false)
				{
					foreach (ClientWebSocket one in crowd)
					{
						if (one != null && one.State == WebSocketState.Open)
							await one.SendAsync(new ArraySegment<byte>(forward ? step : back),
								WebSocketMessageType.Text, true, stopping);
					}

					forward = forward == false;
					await Task.Delay(50, stopping);
				}
			}
			catch (Exception)
			{
				// 시험이 끝나며 닫히는 것은 사고가 아니다.
			}
		}

		private static async Task KeepReadingAsync(ClientWebSocket window)
		{
			byte[] bin = new byte[65536];
			try
			{
				while (window.State == WebSocketState.Open)
					await window.ReceiveAsync(new ArraySegment<byte>(bin), CancellationToken.None);
			}
			catch (Exception)
			{
				// 끝나며 닫힌다.
			}
		}
	}
}
