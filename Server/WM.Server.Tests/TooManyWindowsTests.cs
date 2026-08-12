using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using NUnit.Framework;
using WitchMendokusai.Server;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// <b>한 곳이 창을 무한히 열어 세계를 잠그지 못한다</b> (TASK-WM-220).
	///
	/// ★ 왜: 인사 안 한 손님도 접속마다 인형과 신원을 받는다. 한 사람이 소켓을 계속 열면
	///   세계의 기억과 품이 그만큼 늘어난다 — 창 하나로 세계를 재우는 길이다.
	/// </summary>
	public sealed class TooManyWindowsTests
	{
		private const int PORT = 5433;

		private WebApplication app;
		private WorldHost host;
		private string worldFile;

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-many-windows-" + Path.GetRandomFileName() + ".json");
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
		public async Task 한_곳에서_창을_너무_많이_열면_더_안_받는다()
		{
			System.Collections.Generic.List<ClientWebSocket> windows = new System.Collections.Generic.List<ClientWebSocket>();

			try
			{
				// 상한까지는 다 받아 준다 — 사람이 창 몇 개 여는 건 정상이다.
				int accepted = 0;
				for (int i = 0; i < 12; i++)
				{
					// 바깥에서 온 창인 척한다 — 같은 기계에서 온 창은 안 센다(그건 세계 주인이다).
					ClientWebSocket window = new ClientWebSocket();
					window.Options.SetRequestHeader("CF-Connecting-IP", "203.0.113.9");
					windows.Add(window);
					try
					{
						await window.ConnectAsync(new Uri($"ws://127.0.0.1:{PORT}/ws"), CancellationToken.None);
						accepted++;
					}
					catch (WebSocketException)
					{
						break;
					}
				}

				// 상한까지는 받아 주고, 그 다음은 거절이다(12번 시도해도 8개까지만 붙는다).
				Assert.That(accepted, Is.EqualTo(8), "상한을 넘겨도 계속 받아 준다 — 창 하나로 세계를 재울 수 있다");


				// 하나 닫으면 자리가 난다 — 「영영 못 들어옴」이 되면 안 된다.
				// ⚠ 자리는 <b>세계가 끊긴 걸 알아챈 뒤</b>에 난다(창이 사라져도 서버는 곧바로는 모른다).
				//   그래서 몇 번 두드려 본다 — 곧바로 되기를 기대하면 시험만 흔들린다.
				windows[0].Dispose();

				bool gotIn = false;
				for (int i = 0; i < 10 && gotIn == false; i++)
				{
					await Task.Delay(500);
					ClientWebSocket again = new ClientWebSocket();
					again.Options.SetRequestHeader("CF-Connecting-IP", "203.0.113.9");
					windows.Add(again);
					try
					{
						await again.ConnectAsync(new Uri($"ws://127.0.0.1:{PORT}/ws"), CancellationToken.None);
						gotIn = again.State == WebSocketState.Open;
					}
					catch (WebSocketException)
					{
						// 아직 자리가 안 났다 — 다시 두드린다.
					}
				}

				Assert.That(gotIn, Is.True, "창 하나가 닫혔는데 자리가 안 난다 — 그 곳은 영영 못 들어온다");
			}
			finally
			{
				foreach (ClientWebSocket window in windows)
				{
					try { window.Dispose(); }
					catch (ObjectDisposedException) { /* 이미 닫혔다 */ }
				}
			}
		}
	}
}
