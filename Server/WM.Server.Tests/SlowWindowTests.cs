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
	/// <b>안 읽는 창</b>이 생기면 세계는 그 창에게 사람 수를 줄여 준다 (TASK-WM-228).
	///
	/// ★ 왜 이렇게 재나: 진짜 좁은 회선으로는 이 자리가 안 눌린다 — 세계가 이미 너무 잘 줄여
	///   보내서(바뀐 사람만 + 줄 위 압축) 200명 광장도 <b>초당 1KB</b> 면 따라온다(실측 2026-08-12).
	///   그래서 눌리는 걸 보려면 회선을 비현실적으로 조이는 대신 <b>아예 안 읽는 창</b>을 만든다 —
	///   현실에서도 이게 진짜 모습이다(멈춘 노트북 · 잠긴 화면 · 죽어 가는 와이파이).
	///   창이 안 읽으면 TCP 창이 닫히고, 세계의 보내기가 거기서 막힌다.
	/// </summary>
	public sealed class SlowWindowTests
	{
		private const int PORT = 5413;
		private const int CROWD = 60;

		private static readonly Uri address = new Uri($"ws://127.0.0.1:{PORT}/ws");

		private WebApplication app;
		private WorldHost host;
		private string worldFile;

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-slow-" + Path.GetRandomFileName() + ".json");
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
		public async Task 안_읽는_창에게는_사람_수를_줄여_준다()
		{
			ClientWebSocket[] crowd = new ClientWebSocket[CROWD];
			ClientWebSocket deaf = null;

			try
			{
				// 광장을 채운다 — 판이 커야 안 읽는 창의 줄이 막힌다.
				for (int i = 0; i < CROWD; i++)
					crowd[i] = await JoinAsync(read: true);

				// 이 창은 인사만 하고 <b>영영 안 읽는다</b>.
				deaf = await JoinAsync(read: false);

				// ⚠ 광장이 <b>움직여야</b> 판이 커진다 — 다들 가만히 서 있으면 세계는 「바뀐 사람만」
				//   보내므로 판이 거의 비어(수십 바이트) 안 읽는 창의 줄도 안 막힌다(실측 2026-08-12).
				using CancellationTokenSource walking = new CancellationTokenSource();
				_ = KeepWalkingAsync(crowd, walking.Token);

				using CancellationTokenSource timeout = TestTimeout.After(20);
				int narrowed = 0;
				while (timeout.IsCancellationRequested == false)
				{
					narrowed = host.NarrowedWindowCount;
					if (narrowed > 0)
						break;

					await Task.Delay(200);
				}

				walking.Cancel();
				Assert.Greater(narrowed, 0,
					"안 읽는 창이 있는데도 세계가 그대로 48명을 밀어 넣는다 — 그 창은 영영 못 따라잡는다");
			}
			finally
			{
				foreach (ClientWebSocket one in crowd)
				{
					if (one != null)
						one.Dispose();
				}

				deaf?.Dispose();
			}
		}

		/// <summary>광장 사람들을 계속 걷게 한다 — 그래야 판에 실릴 것이 생긴다.</summary>
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
						if (one == null || one.State != WebSocketState.Open)
							continue;

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

		private static async Task<ClientWebSocket> JoinAsync(bool read)
		{
			ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);
			byte[] hello = Encoding.UTF8.GetBytes("{\"type\":\"hello\",\"secret\":\"\"}");
			await window.SendAsync(new ArraySegment<byte>(hello), WebSocketMessageType.Text, true, CancellationToken.None);

			if (read)
				_ = KeepReadingAsync(window);

			return window;
		}

		/// <summary>보통 창 — 오는 대로 읽어 버린다(그래야 이 창들이 세계를 안 막는다).</summary>
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
				// 시험이 끝나며 닫히는 것은 사고가 아니다.
			}
		}
	}
}
