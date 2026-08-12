using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using NUnit.Framework;
using WitchMendokusai.Numerics;
using WitchMendokusai.Server;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// <b>빨리 보낸다고 빨리 가지지 않는다</b> (TASK-WM-222).
	///
	/// ★ 있던 구멍: 세계는 한 <b>번</b>의 걸음만 1.5m 로 잘랐다. 1초에 몇 번인지는 안 봤다.
	///   그래서 창을 고쳐 1초에 500번 보내면 1초에 750m 를 갔다 — 남들 화면에서는 순간이동이다.
	///   정상 창으로는 절대 안 밟히는 자리라, 사람 눈으로는 영영 안 보인다. 기계가 대신 속인다.
	/// </summary>
	public sealed class NoTeleportTests
	{
		private const int PORT = 5411;

		private static readonly Uri address = new Uri($"ws://127.0.0.1:{PORT}/ws");

		private WebApplication app;
		private WorldHost host;
		private string worldFile;

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-teleport-" + Path.GetRandomFileName() + ".json");
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
		public async Task 걸음을_퍼부어도_걸어서_갈_수_있는_거리까지만_간다()
		{
			ClientWebSocket window = await JoinAsync();

			try
			{
				int dollId = OnlyDollId();
				Vector3 start = host.World.PositionOf(dollId);

				long since = Environment.TickCount64;
				for (int i = 0; i < 500; i++)
					await Send(window, "{\"type\":\"" + Protocol.MOVE + "\",\"x\":1.5,\"z\":0}");

				// 마지막 말까지 세계가 읽을 틈을 준다 — 안 읽힌 걸 「안 갔다」로 세면 거짓 초록이다.
				await Task.Delay(500);
				float spentSeconds = (Environment.TickCount64 - since) / 1000f;

				Vector3 now = host.World.PositionOf(dollId);
				float went = new Vector3(now.x - start.x, 0f, now.z - start.z).magnitude;

				float couldWalk = WitchMendokusai.Net.MoveAllowance.BURST_DISTANCE
					+ (WitchMendokusai.Net.MoveAllowance.ALLOWED_SPEED * spentSeconds);

				Assert.Greater(went, 0f, "정상적인 걸음까지 막으면 그건 고장이다");
				Assert.LessOrEqual(went, couldWalk + 0.5f,
					$"500번 조르니 {went:F1}m 갔다 — {spentSeconds:F2}초 동안 걸어서 갈 수 있는 건 {couldWalk:F1}m 다");
			}
			finally
			{
				await CloseAsync(window);
			}
		}

		[Test]
		public async Task 정상_속도로_걸으면_한_걸음도_안_잘린다()
		{
			ClientWebSocket window = await JoinAsync();

			try
			{
				int dollId = OnlyDollId();
				Vector3 start = host.World.PositionOf(dollId);

				// 웹 창이 실제로 보내는 모양 — 50ms 마다 3m/s 로 걸은 만큼(0.15m).
				for (int i = 0; i < 10; i++)
				{
					await Send(window, "{\"type\":\"" + Protocol.MOVE + "\",\"x\":0.15,\"z\":0}");
					await Task.Delay(50);
				}

				await Task.Delay(300);
				Vector3 now = host.World.PositionOf(dollId);
				float went = new Vector3(now.x - start.x, 0f, now.z - start.z).magnitude;

				Assert.GreaterOrEqual(went, 1.4f, $"정상 창이 1.5m 걸었는데 {went:F2}m 밖에 못 갔다 — 심판이 정상 창을 잡고 있다");
			}
			finally
			{
				await CloseAsync(window);
			}
		}

		private int OnlyDollId()
		{
			WorldDoll[] people = host.World.Snapshot();
			Assert.AreEqual(1, people.Length, "이 시험은 혼자 있는 세계를 본다");
			return people[0].Id;
		}

		private static async Task<ClientWebSocket> JoinAsync()
		{
			ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);
			await Read(window, "\"welcome\"");
			await Send(window, "{\"type\":\"hello\",\"secret\":\"\"}");
			await Read(window, "\"identityId\"");
			return window;
		}

		private static async Task Send(ClientWebSocket socket, string json)
		{
			byte[] payload = Encoding.UTF8.GetBytes(json);
			await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, CancellationToken.None);
		}

		private static async Task CloseAsync(ClientWebSocket socket)
		{
			try
			{
				await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "끝", CancellationToken.None);
			}
			catch (WebSocketException)
			{
				// 이미 닫힌 창 — 정리 중이라 문제될 게 없다.
			}
		}

		private static async Task<string> Read(ClientWebSocket socket, string needle)
		{
			using CancellationTokenSource timeout = TestTimeout.After(15);
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

			Assert.Fail($"기다린 말이 안 왔다: {needle}");
			return null;
		}
	}
}
