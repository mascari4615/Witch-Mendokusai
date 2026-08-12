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
	/// <b>걸어서 옆 세계로 넘어간다</b> — 가방을 들고 (TASK-WM-254).
	///
	/// ★ 왜 이 자리인가: 세계를 나누는 이유는 한 기계의 회선이 벽이기 때문이다(실측 800명 65Mbps).
	///   그런데 나누기만 하고 못 넘어가면 그건 두 개의 다른 게임이다. 이어져야 하나의 세계다.
	///
	/// ★ 여기서 <b>진짜로</b> 두 세계를 띄운다 — 한쪽이 내보내고 다른 쪽이 받는 것까지.
	/// </summary>
	public sealed class WalkingToTheNextZoneTests
	{
		private const int EAST_PORT = 5421;
		private const int WEST_PORT = 5422;
		private const string SECRET = "두 세계만 아는 말";

		private WebApplication east;
		private WebApplication west;
		private WorldHost eastHost;
		private WorldHost westHost;
		private string eastFile;
		private string westFile;

		[SetUp]
		public async Task SetUp()
		{
			eastFile = Path.Combine(Path.GetTempPath(), "wm-east-" + Path.GetRandomFileName() + ".json");
			westFile = Path.Combine(Path.GetTempPath(), "wm-west-" + Path.GetRandomFileName() + ".json");

			// 동쪽 세계: 0..40 을 맡고, 서쪽(-40..0)이 이웃이다.
			Environment.SetEnvironmentVariable("WM_ZONE", "동:0,-40,40,40");
			Environment.SetEnvironmentVariable("WM_ZONE_NEIGHBOURS", $"서:-40,-40,0,40=ws://127.0.0.1:{WEST_PORT}/ws");
			Environment.SetEnvironmentVariable("WM_ZONE_SECRET", SECRET);
			eastHost = new WorldHost(new WorldStore(eastFile));
			east = eastHost.Build(Array.Empty<string>(), $"http://127.0.0.1:{EAST_PORT}");
			await east.StartAsync();

			// 서쪽 세계: -40..0 을 맡는다.
			Environment.SetEnvironmentVariable("WM_ZONE", "서:-40,-40,0,40");
			Environment.SetEnvironmentVariable("WM_ZONE_NEIGHBOURS", $"동:0,-40,40,40=ws://127.0.0.1:{EAST_PORT}/ws");
			westHost = new WorldHost(new WorldStore(westFile));
			west = westHost.Build(Array.Empty<string>(), $"http://127.0.0.1:{WEST_PORT}");
			await west.StartAsync();
		}

		[TearDown]
		public async Task TearDown()
		{
			Environment.SetEnvironmentVariable("WM_ZONE", null);
			Environment.SetEnvironmentVariable("WM_ZONE_NEIGHBOURS", null);
			Environment.SetEnvironmentVariable("WM_ZONE_SECRET", null);

			foreach (WebApplication one in new[] { east, west })
			{
				if (one == null)
					continue;

				await one.StopAsync();
				await one.DisposeAsync();
			}

			east = null;
			west = null;

			foreach (string path in new[] { eastFile, westFile })
			{
				foreach (string one in new[] { path, path + ".bak", path + ".tmp" })
				{
					if (one != null && File.Exists(one))
						File.Delete(one);
				}
			}
		}

		[Test]
		public async Task 서쪽_끝까지_걸으면_옆_세계가_받아_준다()
		{
			using ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(new Uri($"ws://127.0.0.1:{EAST_PORT}/ws"), CancellationToken.None);
			await SendAsync(window, "{\"type\":\"hello\",\"secret\":\"\"}");

			WorldDoll[] people = eastHost.World.Snapshot();
			Assert.AreEqual(1, people.Length);
			int dollId = people[0].Id;

			// 동쪽 세계의 서쪽 끝(x=0)까지 세계의 손으로 데려다 놓고, 거기서 한 걸음 더 간다.
			eastHost.World.TryMove(dollId, new Vector3(-100f, 0f, 0f));
			await SendAsync(window, "{\"type\":\"move\",\"x\":-1.0,\"z\":0}");

			string moveOn = await ReadUntilAsync(window, text => text.Contains("\"type\":\"" + Protocol.MOVE_ON + "\""), 10);

			StringAssert.Contains("\"zone\":\"서\"", moveOn, "어느 세계로 가는지 안 알려 주면 창은 갈 데를 모른다");
			StringAssert.Contains($"127.0.0.1:{WEST_PORT}", moveOn);
			StringAssert.Contains("\"pass\":\"", moveOn, "통행증이 없으면 저쪽은 이 사람을 모른다");

			// 창이 하는 일 그대로: 통행증을 들고 옆 세계에 인사한다.
			string pass = Between(moveOn, "\"pass\":\"", "\"");
			using ClientWebSocket next = new ClientWebSocket();
			await next.ConnectAsync(new Uri($"ws://127.0.0.1:{WEST_PORT}/ws"), CancellationToken.None);
			await SendAsync(next, "{\"type\":\"hello\",\"secret\":\"\",\"pass\":\"" + pass + "\"}");

			await Task.Delay(600);

			WorldDoll[] overThere = westHost.World.Snapshot();
			Assert.AreEqual(1, overThere.Length, "옆 세계가 안 받아 주면 그 사람은 사라진다");
			Assert.LessOrEqual(overThere[0].Position.x, 0f, "받은 자리는 그 세계의 땅 안이어야 한다");

			// 그리고 원래 세계에서는 나갔다 — 둘 다 데리고 있으면 두 세계에 동시에 있게 된다.
			Assert.AreEqual(0, eastHost.World.Snapshot().Length,
				"보낸 세계가 계속 데리고 있으면 그 사람은 두 세계에 동시에 있다(가방이 복사된다)");
		}

		[Test]
		public async Task 다친_몸으로_넘어가면_다친_채로_선다()
		{
			using ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(new Uri($"ws://127.0.0.1:{EAST_PORT}/ws"), CancellationToken.None);
			await SendAsync(window, "{\"type\":\"hello\",\"secret\":\"\"}");
			await Task.Delay(300);

			int dollId = eastHost.World.Snapshot()[0].Id;

			// 세계의 손으로 몸을 깎는다(때리기 판정을 재는 자리가 아니다).
			using ClientWebSocket hitter = new ClientWebSocket();
			await hitter.ConnectAsync(new Uri($"ws://127.0.0.1:{EAST_PORT}/ws"), CancellationToken.None);
			await SendAsync(hitter, "{\"type\":\"hello\",\"secret\":\"\"}");
			await Task.Delay(300);
			eastHost.World.TryStrike(eastHost.World.Snapshot()[1].Id, dollId,
				System.Environment.TickCount64, out int left, out _);
			Assert.Less(left, WitchMendokusai.Net.StrikeRule.FULL_HEALTH, "먼저 다치게 해 놓아야 재는 뜻이 있다");

			eastHost.World.TryMove(dollId, new Vector3(-100f, 0f, 0f));
			await SendAsync(window, "{\"type\":\"move\",\"x\":-1.0,\"z\":0}");

			string moveOn = await ReadUntilAsync(window, text => text.Contains("\"type\":\"" + Protocol.MOVE_ON + "\""), 10);
			string pass = Between(moveOn, "\"pass\":\"", "\"");

			using ClientWebSocket next = new ClientWebSocket();
			await next.ConnectAsync(new Uri($"ws://127.0.0.1:{WEST_PORT}/ws"), CancellationToken.None);
			await SendAsync(next, "{\"type\":\"hello\",\"secret\":\"\",\"pass\":\"" + pass + "\"}");
			await Task.Delay(600);

			WorldDoll[] overThere = westHost.World.Snapshot();
			Assert.AreEqual(1, overThere.Length);
			Assert.AreEqual(left, overThere[0].Health,
				"국경을 넘으니 몸이 가득 찼다 — 맞기 직전에 넘어갔다 오면 회복되는 세계다");

			hitter.Dispose();
		}

		[Test]
		public async Task 지어낸_통행증으로는_안_받아_준다()
		{
			using ClientWebSocket cheat = new ClientWebSocket();
			await cheat.ConnectAsync(new Uri($"ws://127.0.0.1:{WEST_PORT}/ws"), CancellationToken.None);

			// 창이 스스로 만든 통행증 — 도장이 없다.
			await SendAsync(cheat, "{\"type\":\"hello\",\"secret\":\"\",\"pass\":\"9999;10,10;0;1|deadbeef\"}");
			await Task.Delay(500);

			WorldDoll[] here = westHost.World.Snapshot();
			Assert.AreEqual(1, here.Length);
			Assert.AreEqual(0f, here[0].Position.x, 0.01f, "지어낸 통행증이 통하면 아무 자리에나 나타날 수 있다");
			Assert.AreEqual(0f, here[0].Position.z, 0.01f);
		}

		private static string Between(string text, string from, string to)
		{
			int at = text.IndexOf(from, StringComparison.Ordinal) + from.Length;
			int end = text.IndexOf(to, at, StringComparison.Ordinal);
			return text.Substring(at, end - at);
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
	}
}
