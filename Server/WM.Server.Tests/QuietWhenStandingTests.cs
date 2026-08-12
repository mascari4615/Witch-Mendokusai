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
	/// 가만히 서 있으면 <b>조용하다</b> (TASK-WM-236).
	///
	/// ★ 무엇이었나: 몰린 칸에서 공유 소식에 자기가 빠진 창에게는 「네 자리는 여기다」(me)를 따로
	///   보낸다. 그런데 그게 <b>매 판</b> 나갔다 — 가만히 선 사람에게도 초당 20번, 사람 수만큼.
	///   「바뀐 것만 보낸다」는 이 세계의 규칙인데(TASK-WM-220) 이 한 자리만 예외였다.
	/// </summary>
	public sealed class QuietWhenStandingTests
	{
		private const int PORT = 5414;
		private const int CROWD = 60;

		private static readonly Uri address = new Uri($"ws://127.0.0.1:{PORT}/ws");

		private WebApplication app;
		private WorldHost host;
		private string worldFile;

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-quiet-" + Path.GetRandomFileName() + ".json");
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
		public async Task 몰린_광장에_가만히_서_있으면_내_자리를_되풀이해_말하지_않는다()
		{
			ClientWebSocket[] crowd = new ClientWebSocket[CROWD];

			try
			{
				for (int i = 0; i < CROWD; i++)
					crowd[i] = await JoinAsync();

				ClientWebSocket window = await JoinAsync();
				int said = await CountMeAsync(window, 2000);

				// 들어올 때 한 번은 말해 줘야 한다(내 자리를 모르면 화면이 통째로 멎는다).
				// 그 뒤로는 안 움직였으니 조용해야 한다 — 2초면 판이 40번 지나간다.
				Assert.LessOrEqual(said, 3,
					$"가만히 서 있는데 2초에 「네 자리는 여기다」를 {said}번 말했다 — 안 바뀐 것을 되풀이한다");

				window.Dispose();
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
			return window;
		}

		/// <summary>그 창에 「네 자리는 여기다」가 몇 번 왔나 — 읽어 버려야 줄이 안 막힌다.</summary>
		private static async Task<int> CountMeAsync(ClientWebSocket window, int milliseconds)
		{
			using CancellationTokenSource stopping = new CancellationTokenSource(milliseconds);
			byte[] bin = new byte[65536];
			int said = 0;

			try
			{
				while (window.State == WebSocketState.Open)
				{
					WebSocketReceiveResult came = await window.ReceiveAsync(new ArraySegment<byte>(bin), stopping.Token);
					if (came.MessageType == WebSocketMessageType.Close)
						break;

					if (Encoding.UTF8.GetString(bin, 0, came.Count).Contains("\"type\":\"" + Protocol.ME + "\""))
						said += 1;
				}
			}
			catch (OperationCanceledException)
			{
				// 잴 시간이 다 됐다 — 정상 종료다.
			}

			return said;
		}
	}
}
