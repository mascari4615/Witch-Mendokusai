using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using NUnit.Framework;
using WitchMendokusai.Numerics;
using WitchMendokusai.Server;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// <b>끊겼다 다시 붙어도 내 것이 그대로인가</b> (TASK-WM-218).
	///
	/// ★ 왜: 서버 재시작은 드물지만 <b>창이 끊기는 일은 늘 있다</b> — 와이파이가 끊기고, 노트북이 잠들고,
	///   탭이 죽는다. 그때 가방·자리·이름이 날아가면 사람은 「이 게임은 못 믿겠다」고 느낀다.
	///   서버는 살아 있고 창만 다시 붙는 이 경우가, 실제로 가장 자주 밟는 자리다.
	/// </summary>
	/// ⏱ 이 시험들이 각각 30초씩 걸리던 때가 있었다(2026-08-10). 시험이 느린 게 아니라
	///   <b>서버가 안 멎는 것</b>이었다 — 수신 대기가 「영원히」라 종료가 그 대기를 붙잡았다.
	///   그건 배포마다 세계가 30초 닫힌다는 뜻이라, 시험 속도가 아니라 운영 결함이었다.
	///   <see cref="ShutdownTests"/> 가 그 자리를 재고 <c>WorldHost</c> 가 종료 신호를 수신에 물린 뒤
	///   30초 → 0.5초가 됐다. 「느리다」는 신호를 그냥 넘기지 않아서 잡은 것이다.
	public sealed class ReconnectTests
	{
		private const int PORT = 5417;
		private static readonly Uri address = new Uri($"ws://127.0.0.1:{PORT}/ws");

		private WebApplication app;
		private WorldHost host;
		private string worldFile;
		private string mine;

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-recon-" + Path.GetRandomFileName() + ".json");
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
		public async Task 끊겼다_다시_붙으면_가방이_그대로다()
		{
			// ⚠ 순서가 전부다 (실측 2026-08-10): 끊긴 <뒤>에 그 인형을 만지면 이미 세계에서 빠진 뒤라
			//   아무 일도 안 일어난다 — 내 첫 시험이 그래서 「가방이 비었다」로 거짓 실패했다.
			//   사람이 하는 순서 그대로: 붙어서 줍고, 그 다음 끊긴다.
			(ClientWebSocket window, int dollId) = await JoinKeeping("기기-끊김");
			host.World.TryGather(dollId, ServerItemCatalog.Find(WorldSeeds.WOOD), 4);

			// 창이 죽는다(인사도 없이 사라진다 — 진짜 끊김은 그렇다).
			window.Abort(); // 진짜 끊김은 인사가 없다 — Dispose 보다 즉시 끊긴다
			await Task.Delay(300);

			int again = await JoinAgain();
			Assert.AreEqual(4, host.World.BagCount(again, WorldSeeds.WOOD),
				"끊겼다 붙었더니 가방이 비면, 사람은 그 세계를 못 믿는다");
		}

		[Test]
		public async Task 끊겼다_다시_붙으면_서_있던_자리가_그대로다()
		{
			(ClientWebSocket window, int dollId) = await JoinKeeping("기기-자리");

			// 저만치 걸어간 뒤 끊긴다.
			for (int step = 0; step < 20; step++)
				host.World.TryMove(dollId, new Vector3(1.5f, 0f, 0f));

			Vector3 before = host.World.PositionOf(dollId);
			Assert.Greater(before.x, 5f, "안 걸었으면 자리를 잴 수 없다");

			window.Abort(); // 진짜 끊김은 인사가 없다 — Dispose 보다 즉시 끊긴다
			await Task.Delay(300);

			int again = await JoinAgain();
			Vector3 after = host.World.PositionOf(again);

			Assert.AreEqual(before.x, after.x, 0.5f, "돌아왔더니 원점이면, 멀리 간 사람일수록 손해다");
			Assert.AreEqual(before.z, after.z, 0.5f);
		}

		[Test]
		public async Task 끊겼다_다시_붙어도_이름이_그대로다()
		{
			int _ = await JoinFresh("기기-이름유지");

			using (ClientWebSocket naming = new ClientWebSocket())
			{
				await naming.ConnectAsync(address, CancellationToken.None);
				await Read(naming, "\"welcome\"");
				await Send(naming, "{\"type\":\"hello\",\"secret\":\"" + mine + "\"}");
				await Read(naming, "\"identityId\"");

				await Send(naming, "{\"type\":\"" + Protocol.RENAME + "\",\"name\":\"링\"}");
				await Read(naming, "\"name\":\"링\"");
			}

			await Task.Delay(300);

			using ClientWebSocket back = new ClientWebSocket();
			await back.ConnectAsync(address, CancellationToken.None);
			await Read(back, "\"welcome\"");
			await Send(back, "{\"type\":\"hello\",\"secret\":\"" + mine + "\"}");
			await Read(back, "\"identityId\"");

			string world = await Read(back, "\"name\":\"링\"");
			StringAssert.Contains("\"name\":\"링\"", world, "돌아올 때마다 이름을 다시 정해야 하면 그건 내 이름이 아니다");
		}

		[Test]
		public async Task 끊긴_사람은_남의_화면에서_사라진다()
		{
			int _ = await JoinFresh("기기-사라짐");

			using ClientWebSocket watcher = new ClientWebSocket();
			await watcher.ConnectAsync(address, CancellationToken.None);
			await Read(watcher, "\"welcome\"");

			await Task.Delay(500);

			// 끊긴 사람이 남의 화면에 유령으로 서 있으면, 사람들은 「저기 누가 있다」고 착각한다.
			Assert.LessOrEqual(host.World.Snapshot().Length, 1,
				"끊긴 사람이 세계에 남아 있다");
		}

		/// <summary>새 사람으로 들어와 열쇠를 받고 <b>붙어 있는 채로</b> 돌려준다 — 끊는 건 부르는 쪽이 정한다.</summary>
		private async Task<(ClientWebSocket window, int dollId)> JoinKeeping(string deviceSecret)
		{
			ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);

			string welcome = await Read(window, "\"welcome\"");
			int dollId = JsonDocument.Parse(welcome).RootElement.GetProperty("id").GetInt32();

			await Send(window, "{\"type\":\"hello\",\"secret\":\"" + deviceSecret + "\"}");
			string said = await Read(window, "\"identityId\"");
			mine = JsonDocument.Parse(said).RootElement.GetProperty("secret").GetString();
			if (string.IsNullOrEmpty(mine))
				mine = deviceSecret;

			return (window, dollId);
		}

		/// <summary>새 사람으로 들어와 열쇠를 받고, 그 창을 <b>그냥 죽인다</b>(진짜 끊김).</summary>
		private async Task<int> JoinFresh(string deviceSecret)
		{
			ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);

			string welcome = await Read(window, "\"welcome\"");
			int dollId = JsonDocument.Parse(welcome).RootElement.GetProperty("id").GetInt32();

			await Send(window, "{\"type\":\"hello\",\"secret\":\"" + deviceSecret + "\"}");
			string said = await Read(window, "\"identityId\"");
			mine = JsonDocument.Parse(said).RootElement.GetProperty("secret").GetString();
			if (string.IsNullOrEmpty(mine))
				mine = deviceSecret;

			window.Dispose(); // 인사 없이 끊는다
			return dollId;
		}

		/// <summary>같은 열쇠로 다시 붙는다 — 세계가 준 새 인형 번호를 돌려준다.</summary>
		private async Task<int> JoinAgain()
		{
			ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);

			string welcome = await Read(window, "\"welcome\"");
			int dollId = JsonDocument.Parse(welcome).RootElement.GetProperty("id").GetInt32();

			await Send(window, "{\"type\":\"hello\",\"secret\":\"" + mine + "\"}");
			await Read(window, "\"identityId\"");
			await Task.Delay(200);
			return dollId;
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
