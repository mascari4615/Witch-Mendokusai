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
	/// <b>싸움도 세계가 판정한다</b> (TASK-WM-251).
	///
	/// ★ 창이 우길 수 있는 것 셋 — 얼마나 멀리서 · 얼마나 자주 · 누구를. 셋 다 세계가 본다.
	///   걸음을 시계로 심판한 것(WM-222)과 같은 자리다: 안 보면 창을 고쳐 초당 100번 때린다.
	/// </summary>
	public sealed class StrikingTests
	{
		private const int PORT = 5419;

		private static readonly Uri address = new Uri($"ws://127.0.0.1:{PORT}/ws");

		private WebApplication app;
		private WorldHost host;
		private string worldFile;

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-strike-" + Path.GetRandomFileName() + ".json");
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
		public async Task 옆_사람을_때리면_몸이_준다()
		{
			using ClientWebSocket attacker = await JoinAsync();
			using ClientWebSocket target = await JoinAsync();

			int targetId = SecondPersonId();

			await SendAsync(attacker, "{\"type\":\"" + Protocol.STRIKE + "\",\"targetId\":" + targetId + "}");

			string hurt = await ReadUntilAsync(attacker, text => text.Contains("\"type\":\"" + Protocol.HURT + "\""), 10);
			StringAssert.Contains("\"health\":" + (WitchMendokusai.Net.StrikeRule.FULL_HEALTH - WitchMendokusai.Net.StrikeRule.DAMAGE), hurt);
			Assert.AreEqual(WitchMendokusai.Net.StrikeRule.FULL_HEALTH - WitchMendokusai.Net.StrikeRule.DAMAGE,
				HealthOf(targetId), "세계가 아는 몸이 안 줄었다");
		}

		[Test]
		public async Task 멀리_있는_사람은_못_때린다()
		{
			using ClientWebSocket attacker = await JoinAsync();
			using ClientWebSocket target = await JoinAsync();

			int targetId = SecondPersonId();
			for (int step = 0; step < 10; step++)
				host.World.TryMove(targetId, new Vector3(1f, 0f, 0f));

			await Task.Delay(200);
			await SendAsync(attacker, "{\"type\":\"" + Protocol.STRIKE + "\",\"targetId\":" + targetId + "}");

			bool felt = await SawWithinAsync(attacker, Protocol.HURT, 1200);
			Assert.IsFalse(felt, "손이 안 닿는데 맞으면 그건 창이 우긴 것이다");
			Assert.AreEqual(WitchMendokusai.Net.StrikeRule.FULL_HEALTH, HealthOf(targetId));
		}

		[Test]
		public async Task 아무리_빨리_눌러도_팔이_돌아와야_때린다()
		{
			using ClientWebSocket attacker = await JoinAsync();
			using ClientWebSocket target = await JoinAsync();

			int targetId = SecondPersonId();
			for (int i = 0; i < 20; i++)
				await SendAsync(attacker, "{\"type\":\"" + Protocol.STRIKE + "\",\"targetId\":" + targetId + "}");

			await Task.Delay(500);

			int hit = (WitchMendokusai.Net.StrikeRule.FULL_HEALTH - HealthOf(targetId))
				/ WitchMendokusai.Net.StrikeRule.DAMAGE;
			Assert.LessOrEqual(hit, 2, $"0.5초에 {hit}대를 때렸다 — 창을 고치면 이긴다는 뜻이다");
			Assert.GreaterOrEqual(hit, 1, "한 대는 들어가야 한다");
		}

		[Test]
		public async Task 다_맞으면_다시_세워진다()
		{
			using ClientWebSocket attacker = await JoinAsync();
			using ClientWebSocket target = await JoinAsync();

			int targetId = SecondPersonId();
			int need = WitchMendokusai.Net.StrikeRule.FULL_HEALTH / WitchMendokusai.Net.StrikeRule.DAMAGE;

			bool down = false;
			for (int i = 0; i < need + 2 && down == false; i++)
			{
				await SendAsync(attacker, "{\"type\":\"" + Protocol.STRIKE + "\",\"targetId\":" + targetId + "}");
				await Task.Delay((int)WitchMendokusai.Net.StrikeRule.COOLDOWN_MS + 60);
				down = HealthOf(targetId) == WitchMendokusai.Net.StrikeRule.FULL_HEALTH
					&& host.World.PositionOf(targetId).x == 0f
					&& i >= need - 1;
			}

			Assert.IsTrue(down, "다 맞았는데 안 세워지면 그 사람은 게임에서 나간 것이 된다");
		}

		/// <summary>그 번호의 몸 — <b>순서로 찾지 않는다</b>(목록 순서는 세계의 약속이 아니다).</summary>
		private int HealthOf(int dollId)
		{
			foreach (WorldDoll one in host.World.Snapshot())
			{
				if (one.Id == dollId)
					return one.Health;
			}

			Assert.Fail($"{dollId} 번 사람이 세계에 없다");
			return 0;
		}

		/// <summary>둘째로 들어온 사람의 번호 — 번호가 큰 쪽이다.</summary>
		private int SecondPersonId()
		{
			WorldDoll[] people = host.World.Snapshot();
			Assert.GreaterOrEqual(people.Length, 2, "이 시험은 둘이 있는 세계를 본다");

			int highest = people[0].Id;
			foreach (WorldDoll one in people)
			{
				if (one.Id > highest)
					highest = one.Id;
			}

			return highest;
		}

		private static async Task<ClientWebSocket> JoinAsync()
		{
			ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);
			byte[] hello = Encoding.UTF8.GetBytes("{\"type\":\"hello\",\"secret\":\"\"}");
			await window.SendAsync(new ArraySegment<byte>(hello), WebSocketMessageType.Text, true, CancellationToken.None);
			return window;
		}

		private static async Task SendAsync(ClientWebSocket window, string json)
		{
			byte[] payload = Encoding.UTF8.GetBytes(json);
			await window.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, CancellationToken.None);
		}

		private static async Task<string> ReadUntilAsync(ClientWebSocket window, Func<string, bool> matches, int seconds)
		{
			using CancellationTokenSource timeout = TestTimeout.After(seconds);
			byte[] bin = new byte[65536];

			while (timeout.IsCancellationRequested == false)
			{
				WebSocketReceiveResult came;
				try
				{
					came = await window.ReceiveAsync(new ArraySegment<byte>(bin), timeout.Token);
				}
				catch (OperationCanceledException)
				{
					break;
				}

				if (came.MessageType == WebSocketMessageType.Close)
					break;

				string text = Encoding.UTF8.GetString(bin, 0, came.Count);
				if (matches(text))
					return text;
			}

			Assert.Fail("기다린 말이 안 왔다");
			return null;
		}

		private static async Task<bool> SawWithinAsync(ClientWebSocket window, string kind, int milliseconds)
		{
			using CancellationTokenSource stopping = new CancellationTokenSource(milliseconds);
			byte[] bin = new byte[65536];

			try
			{
				while (window.State == WebSocketState.Open)
				{
					WebSocketReceiveResult came = await window.ReceiveAsync(new ArraySegment<byte>(bin), stopping.Token);
					if (came.MessageType == WebSocketMessageType.Close)
						break;

					if (Encoding.UTF8.GetString(bin, 0, came.Count).Contains("\"type\":\"" + kind + "\""))
						return true;
				}
			}
			catch (OperationCanceledException)
			{
				// 안 왔다 — 그게 답이다.
			}

			return false;
		}
	}
}
