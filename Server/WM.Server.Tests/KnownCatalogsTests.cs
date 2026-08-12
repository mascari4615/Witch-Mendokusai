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
	/// 이미 들고 있는 낱말표는 <b>다시 안 보낸다</b> (TASK-WM-238).
	///
	/// ★ 왜: 낱말표·지을 것·솥 재료·마도서·제작표는 서버가 도는 동안 안 바뀐다. 그런데 붙을 때마다
	///   다시 나갔다 — 실측 2026-08-12: 한 번 붙는 데 30.1KB, 그중 <b>7.3KB</b> 가 이 다섯이다.
	///   회선이 나쁘면 다시 붙는 일이 잦고, 초당 4KB 회선에서는 그것만 2초다(그동안 세계는 안 흐른다).
	/// </summary>
	public sealed class KnownCatalogsTests
	{
		private const int PORT = 5415;

		private static readonly Uri address = new Uri($"ws://127.0.0.1:{PORT}/ws");

		private WebApplication app;
		private string worldFile;

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-known-" + Path.GetRandomFileName() + ".json");
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

			foreach (string path in new[] { worldFile, worldFile + ".bak", worldFile + ".tmp" })
			{
				if (File.Exists(path))
					File.Delete(path);
			}
		}

		[Test]
		public async Task 처음_온_창은_낱말표를_받고_도장도_받는다()
		{
			using ClientWebSocket window = await JoinAsync(knownStamp: null);
			(string stamp, bool gotCatalog) = await ListenAsync(window, 2500);

			Assert.IsNotEmpty(stamp ?? string.Empty, "도장을 안 주면 창은 다음에도 다 받아야 한다");
			Assert.IsTrue(gotCatalog, "낱말표가 없으면 화면이 「17450 3개」가 된다");
		}

		[Test]
		public async Task 도장을_들고_온_창에는_낱말표를_안_보낸다()
		{
			string stamp;
			using (ClientWebSocket first = await JoinAsync(knownStamp: null))
				(stamp, _) = await ListenAsync(first, 2000);

			using ClientWebSocket again = await JoinAsync(knownStamp: stamp);
			(_, bool gotCatalog) = await ListenAsync(again, 2500);

			Assert.IsFalse(gotCatalog,
				"같은 도장을 들고 왔는데 7.3KB 를 또 보냈다 — 회선이 나쁠수록 이게 값이다");
		}

		[Test]
		public async Task 인사를_안_하는_옛_창에도_낱말표는_간다()
		{
			// ⚠ 접속은 인사를 안 기다린다(옛 창이 그 자리에서 멈춰 섰던 적이 있다).
			//   그러니 인사가 안 와도 잠깐 뒤에는 줘야 한다.
			using ClientWebSocket quiet = new ClientWebSocket();
			await quiet.ConnectAsync(address, CancellationToken.None);
			(_, bool gotCatalog) = await ListenAsync(quiet, 3000);

			Assert.IsTrue(gotCatalog, "인사를 안 한다고 낱말표를 영영 안 주면 그 창은 못 논다");
		}

		private static async Task<ClientWebSocket> JoinAsync(string knownStamp)
		{
			ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);
			string hello = "{\"type\":\"hello\",\"secret\":\"\""
				+ (knownStamp == null ? string.Empty : ",\"knownCatalogs\":\"" + knownStamp + "\"")
				+ "}";
			await window.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(hello)),
				WebSocketMessageType.Text, true, CancellationToken.None);
			return window;
		}

		/// <summary>그 창에 도장이 왔나 · 낱말표가 왔나.</summary>
		private static async Task<(string Stamp, bool Catalog)> ListenAsync(ClientWebSocket window, int milliseconds)
		{
			using CancellationTokenSource stopping = new CancellationTokenSource(milliseconds);
			byte[] bin = new byte[65536];
			string stamp = null;
			bool catalog = false;

			try
			{
				while (window.State == WebSocketState.Open)
				{
					WebSocketReceiveResult came = await window.ReceiveAsync(new ArraySegment<byte>(bin), stopping.Token);
					if (came.MessageType == WebSocketMessageType.Close)
						break;

					string text = Encoding.UTF8.GetString(bin, 0, came.Count);
					if (text.Contains("\"type\":\"" + Protocol.CATALOG + "\""))
						catalog = true;

					const string mark = "\"catalogStamp\":\"";
					int at = text.IndexOf(mark, StringComparison.Ordinal);
					if (at >= 0)
					{
						int from = at + mark.Length;
						int to = text.IndexOf('"', from);
						if (to > from)
							stamp = text.Substring(from, to - from);
					}
				}
			}
			catch (OperationCanceledException)
			{
				// 잴 시간이 다 됐다 — 정상 종료다.
			}

			return (stamp, catalog);
		}
	}
}
