using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using NUnit.Framework;
using WitchMendokusai.Server;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// <b>오래 돌아도 세계가 부풀지 않는다</b> (TASK-WM-217/218).
	///
	/// ★ 왜: 서버는 껐다 켜지 않고 며칠씩 돈다. 사람은 들어왔다 나가기를 수없이 되풀이한다.
	///   그때마다 장부에 한 줄씩 쌓이면 <b>저장 파일이 끝없이 커지고</b> 되살리기가 느려진다 —
	///   증상은 「어느 날부터 서버가 무거워졌다」이고, 원인을 찾기가 가장 어려운 부류다.
	///   빈손으로 스쳐간 손님은 지우고, <b>뭔가 남긴 사람은 절대 안 지운다</b>(그건 세계를 지우는 짓이다).
	/// </summary>
	public sealed class WorldDoesNotBloatTests
	{
		private const int PORT = 5415;
		private const int VISITS = 30;

		private static readonly Uri address = new Uri($"ws://127.0.0.1:{PORT}/ws");

		private WebApplication app;
		private WorldHost host;
		private string worldFile;

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-bloat-" + Path.GetRandomFileName() + ".json");
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
		public async Task 들어왔다_나가기를_되풀이해도_세계에_사람이_안_쌓인다()
		{
			for (int visit = 0; visit < VISITS; visit++)
			{
				using ClientWebSocket window = new ClientWebSocket();
				await window.ConnectAsync(address, CancellationToken.None);
				await Read(window, "\"welcome\"");
			}

			await Task.Delay(500);

			Assert.LessOrEqual(host.World.Snapshot().Length, 2,
				"나간 사람이 세계에 남으면, 며칠 돌린 서버에는 유령이 수백 명 서 있게 된다");
		}

		[Test]
		public async Task 빈손으로_스쳐간_손님은_장부에서_지워진다()
		{
			for (int visit = 0; visit < VISITS; visit++)
			{
				using ClientWebSocket window = new ClientWebSocket();
				await window.ConnectAsync(address, CancellationToken.None);
				await Read(window, "\"welcome\"");
				await Send(window, "{\"type\":\"hello\",\"secret\":\"스쳐간손님-" + visit + "\"}");
				await Read(window, "\"identityId\"");
			}

			await Task.Delay(500);
			int before = host.Identities.Count;
			Assert.GreaterOrEqual(before, VISITS, "손님이 장부에 안 적히면 이 시험은 뜻이 없다");

			// 세계의 시간이 넉넉히 흐른 뒤 — 빈손으로 스쳐간 사람은 지운다.
			int forgotten = host.Identities.PruneGuests(500, 90, host.World.OwnsSomething);

			Assert.Greater(forgotten, 0, "아무도 안 지워지면 장부는 영영 커지기만 한다");
			Assert.Less(host.Identities.Count, before);
		}

		[Test]
		public async Task 뭔가_남긴_사람은_오래_안_와도_안_지워진다()
		{
			using ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);

			string welcome = await Read(window, "\"welcome\"");
			int dollId = JsonDocument.Parse(welcome).RootElement.GetProperty("id").GetInt32();

			await Send(window, "{\"type\":\"hello\",\"secret\":\"뭔가-남긴-사람\"}");
			string mine = await Read(window, "\"identityId\"");
			int identityId = JsonDocument.Parse(mine).RootElement.GetProperty("identityId").GetInt32();

			// 가방에 뭔가 있는 채로 나간다 — 그건 「세계에 남긴 것」이다.
			host.World.TryGather(dollId, ServerItemCatalog.Find(WorldSeeds.WOOD), 3);
			window.Dispose();
			await Task.Delay(400);

			int forgotten = host.Identities.PruneGuests(500, 90, host.World.OwnsSomething);

			Assert.IsNotNull(host.Identities.Find(identityId),
				$"가진 사람을 지우면 그 사람의 세계가 통째로 사라진다 (지운 수={forgotten})");
		}

		[Test]
		public async Task 사람이_수없이_다녀가도_저장이_감당할_크기다()
		{
			for (int visit = 0; visit < VISITS; visit++)
			{
				using ClientWebSocket window = new ClientWebSocket();
				await window.ConnectAsync(address, CancellationToken.None);
				await Read(window, "\"welcome\"");
				await Send(window, "{\"type\":\"hello\",\"secret\":\"다녀간사람-" + visit + "\"}");
				await Read(window, "\"identityId\"");
			}

			await StopAsync();

			long bytes = new FileInfo(worldFile).Length;

			// 사람 하나에 수 KB 씩 붙으면 며칠 만에 수십 MB 가 된다 — 되살리기가 느려지는 자리다.
			Assert.Less(bytes, 200 * 1024,
				$"서른 명 다녀간 세계가 {bytes} 바이트다 — 사람마다 붙는 것이 너무 크다");
		}

		private async Task StopAsync()
		{
			if (app == null)
				return;

			await app.StopAsync();
			await app.DisposeAsync();
			app = null;
		}

		private static async Task Send(ClientWebSocket socket, string json)
		{
			byte[] payload = Encoding.UTF8.GetBytes(json);
			await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, CancellationToken.None);
		}

		private static async Task<string> Read(ClientWebSocket socket, string needle)
		{
			using CancellationTokenSource timeout = TestTimeout.After(10);
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
