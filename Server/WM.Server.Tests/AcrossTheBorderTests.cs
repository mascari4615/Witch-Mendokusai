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
	/// <b>국경 너머가 보인다</b> (TASK-WM-263).
	///
	/// ★ 왜: 세계를 나눠 놓고(WM-252~259) 국경에 서면 <b>1m 옆</b>의 사람이 안 보였다.
	///   저 사람은 옆 세계에 있고 이 세계는 그를 모르기 때문이다 — 그러면 한 세계가 아니라
	///   벽으로 갈린 두 게임이다. 사람은 국경 언저리를 「고장 난 자리」로 느낀다.
	///
	/// ★ 여기서 진짜로 두 세계를 띄우고, 둘이 서로의 국경 띠를 알려 주는 것까지 잰다.
	/// </summary>
	public sealed class AcrossTheBorderTests
	{
		private const int EAST_PORT = 5431;
		private const int WEST_PORT = 5432;
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
			eastFile = Path.Combine(Path.GetTempPath(), "wm-be-" + Path.GetRandomFileName() + ".json");
			westFile = Path.Combine(Path.GetTempPath(), "wm-bw-" + Path.GetRandomFileName() + ".json");

			Environment.SetEnvironmentVariable("WM_ZONE_SECRET", SECRET);

			Environment.SetEnvironmentVariable("WM_ZONE", "동:0,-40,40,40");
			Environment.SetEnvironmentVariable("WM_ZONE_NEIGHBOURS", $"서:-40,-40,0,40=ws://127.0.0.1:{WEST_PORT}/ws");
			eastHost = new WorldHost(new WorldStore(eastFile));
			east = eastHost.Build(Array.Empty<string>(), $"http://127.0.0.1:{EAST_PORT}");
			await east.StartAsync();

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
		public async Task 국경에_서면_저_세계_사람이_보인다()
		{
			using ClientWebSocket inTheEast = new ClientWebSocket();
			await inTheEast.ConnectAsync(new Uri($"ws://127.0.0.1:{EAST_PORT}/ws"), CancellationToken.None);
			await SendAsync(inTheEast, "{\"type\":\"hello\",\"secret\":\"\"}");

			using ClientWebSocket inTheWest = new ClientWebSocket();
			await inTheWest.ConnectAsync(new Uri($"ws://127.0.0.1:{WEST_PORT}/ws"), CancellationToken.None);
			await SendAsync(inTheWest, "{\"type\":\"hello\",\"secret\":\"\"}");
			await Task.Delay(500);

			// 둘 다 국경(x=0)에 바짝 붙여 세운다 — 1m 사이다.
			int eastDoll = eastHost.World.Snapshot()[0].Id;
			int westDoll = westHost.World.Snapshot()[0].Id;
			eastHost.World.TryMove(eastDoll, new Vector3(0.5f, 0f, 0f));
			westHost.World.TryMove(westDoll, new Vector3(-100f, 0f, 0f));   // 제 땅 끝(x=0)으로 당겨진다

			// 두 세계가 서로 국경 띠를 알려 줄 틈 (100ms 마다 오간다).
			string sawSomeone = await ReadUntilAsync(inTheEast,
				text => text.Contains("\"type\":\"world\"") && text.Contains("\"id\":-"), 15);

			StringAssert.Contains("\"id\":-", sawSomeone,
				"국경 너머 사람이 안 보인다 — 1m 옆인데 벽이 서 있는 셈이다");

			// 반대쪽에서도 보여야 한다 — 한쪽만 보이면 그건 더 이상한 세계다.
			string seenBack = await ReadUntilAsync(inTheWest,
				text => text.Contains("\"type\":\"world\"") && text.Contains("\"id\":-"), 15);
			StringAssert.Contains("\"id\":-", seenBack);
		}

		[Test]
		public async Task 국경_너머_사람은_못_때린다()
		{
			using ClientWebSocket inTheEast = new ClientWebSocket();
			await inTheEast.ConnectAsync(new Uri($"ws://127.0.0.1:{EAST_PORT}/ws"), CancellationToken.None);
			await SendAsync(inTheEast, "{\"type\":\"hello\",\"secret\":\"\"}");

			using ClientWebSocket inTheWest = new ClientWebSocket();
			await inTheWest.ConnectAsync(new Uri($"ws://127.0.0.1:{WEST_PORT}/ws"), CancellationToken.None);
			await SendAsync(inTheWest, "{\"type\":\"hello\",\"secret\":\"\"}");
			await Task.Delay(500);

			eastHost.World.TryMove(eastHost.World.Snapshot()[0].Id, new Vector3(0.5f, 0f, 0f));
			int westDoll = westHost.World.Snapshot()[0].Id;
			westHost.World.TryMove(westDoll, new Vector3(-100f, 0f, 0f));

			string sawSomeone = await ReadUntilAsync(inTheEast,
				text => text.Contains("\"type\":\"world\"") && text.Contains("\"id\":-"), 15);
			int shadowId = ShadowIdIn(sawSomeone);
			Assert.Less(shadowId, 0, "그림자를 못 찾았으면 이 시험은 뜻이 없다");

			// 그림자를 때려 본다 — 저 세계 사람의 몸이 깎이면 그건 두 세계의 판정이 갈린 것이다.
			await SendAsync(inTheEast, "{\"type\":\"strike\",\"targetId\":" + shadowId + "}");
			await Task.Delay(600);

			Assert.AreEqual(WitchMendokusai.Net.StrikeRule.FULL_HEALTH,
				westHost.World.HealthOf(westDoll),
				"국경 너머 사람이 맞았다 — 보이기만 해야 하는 그림자를 세계가 사람으로 다뤘다");
		}

		[Test]
		public async Task 옆_세계가_꺼지면_그림자도_사라진다()
		{
			using ClientWebSocket inTheEast = new ClientWebSocket();
			await inTheEast.ConnectAsync(new Uri($"ws://127.0.0.1:{EAST_PORT}/ws"), CancellationToken.None);
			await SendAsync(inTheEast, "{\"type\":\"hello\",\"secret\":\"\"}");

			using ClientWebSocket inTheWest = new ClientWebSocket();
			await inTheWest.ConnectAsync(new Uri($"ws://127.0.0.1:{WEST_PORT}/ws"), CancellationToken.None);
			await SendAsync(inTheWest, "{\"type\":\"hello\",\"secret\":\"\"}");
			await Task.Delay(500);

			eastHost.World.TryMove(eastHost.World.Snapshot()[0].Id, new Vector3(0.5f, 0f, 0f));
			westHost.World.TryMove(westHost.World.Snapshot()[0].Id, new Vector3(-100f, 0f, 0f));
			await ReadUntilAsync(inTheEast, text => text.Contains("\"id\":-"), 15);

			// 서쪽 세계를 닫는다 — 안 지우면 국경에 <b>유령</b>이 영영 서 있는다.
			await west.StopAsync();
			west = null;
			await Task.Delay((int)NeighbourShadows.FADE_AFTER_MS + 1500);

			Assert.AreEqual(0, eastHost.ShadowCount,
				"옆 세계가 꺼졌는데 그림자가 남아 있다 — 국경에 유령이 선다");

			// ⚠ 창의 줄에는 <b>꺼지기 전의 판</b>들이 아직 쌓여 있다 — 그걸 읽고 「유령이다」라고
			//   하면 거짓 빨강이다. 밀린 것을 다 흘려보낸 뒤의 판으로 본다.
			string plate = await FreshPlateAsync(inTheEast);
			StringAssert.DoesNotContain("\"id\":-", plate,
				"밀린 판을 다 비운 뒤에도 국경에 그림자가 서 있다");
		}

		/// <summary>
		/// 쌓인 판을 흘려보내고 <b>마지막</b> 판을 준다.
		/// ⚠ 「조용해질 때까지」로 적으면 안 끝난다 — 세계는 초당 20번 계속 말한다(첫 판에 걸렸다).
		/// </summary>
		private static async Task<string> FreshPlateAsync(ClientWebSocket window)
		{
			byte[] bin = new byte[65536];
			string last = string.Empty;
			DateTime until = DateTime.UtcNow.AddMilliseconds(1500);

			while (DateTime.UtcNow < until)
			{
				using CancellationTokenSource quiet = new CancellationTokenSource(400);
				try
				{
					WebSocketReceiveResult came = await window.ReceiveAsync(new ArraySegment<byte>(bin), quiet.Token);
					if (came.MessageType == WebSocketMessageType.Close)
						return last;

					string text = Encoding.UTF8.GetString(bin, 0, came.Count);
					if (text.Contains("\"type\":\"world\""))
						last = text;
				}
				catch (OperationCanceledException)
				{
					return last;
				}
			}

			return last;
		}

		private static int ShadowIdIn(string plate)
		{
			int at = plate.IndexOf("\"id\":-", StringComparison.Ordinal) + "\"id\":".Length;
			int end = at;
			while (end < plate.Length && (plate[end] == '-' || char.IsDigit(plate[end])))
				end += 1;

			return int.Parse(plate.Substring(at, end - at));
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

			return string.Empty;
		}
	}
}
