using System;
using System.Collections.Generic;
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
	/// <b>세 세계가 만나는 모서리</b> (TASK-WM-265).
	///
	/// ★ 왜: 국경 너머 보기(WM-263)·말하기(WM-264)는 <b>이웃 하나</b>로만 재봤다. 그런데 땅을 나누면
	///   반드시 <b>모서리</b>가 생긴다 — 거기 서면 이웃이 둘이다. 그 자리에서
	///   ① 두 세계 사람이 <b>같이</b> 보이나 ② 두 세계에서 온 사람의 번호가 <b>안 겹치나</b>
	///   ③ 그림자가 다시 옆 세계로 <b>비쳐 나가지 않나</b>(비치면 한 사람이 여러 겹으로 늘어난다).
	///
	/// ★ 모서리는 나눈 세계에서 가장 흔한 「이상한 자리」다 — 광장·길목이 대개 경계에 걸린다.
	/// </summary>
	public sealed class WhereThreeWorldsMeetTests
	{
		private const int EAST_PORT = 5451;
		private const int WEST_PORT = 5452;
		private const int NORTH_PORT = 5453;
		private const string SECRET = "세 세계만 아는 말";

		// 동 = 오른쪽 아래 · 서 = 왼쪽 아래 · 북 = 위쪽 전부. (0, 40) 이 셋이 만나는 모서리다.
		private const string EAST_LAND = "동:0,-40,40,40";
		private const string WEST_LAND = "서:-40,-40,0,40";
		private const string NORTH_LAND = "북:-40,40,40,80";

		private readonly List<WebApplication> worlds = new List<WebApplication>();
		private readonly List<string> files = new List<string>();

		private WorldHost eastHost;
		private WorldHost westHost;
		private WorldHost northHost;

		[SetUp]
		public async Task SetUp()
		{
			Environment.SetEnvironmentVariable("WM_ZONE_SECRET", SECRET);

			eastHost = await RaiseAsync(EAST_PORT, EAST_LAND,
				$"{WEST_LAND}=ws://127.0.0.1:{WEST_PORT}/ws;{NORTH_LAND}=ws://127.0.0.1:{NORTH_PORT}/ws");
			westHost = await RaiseAsync(WEST_PORT, WEST_LAND,
				$"{EAST_LAND}=ws://127.0.0.1:{EAST_PORT}/ws;{NORTH_LAND}=ws://127.0.0.1:{NORTH_PORT}/ws");
			northHost = await RaiseAsync(NORTH_PORT, NORTH_LAND,
				$"{EAST_LAND}=ws://127.0.0.1:{EAST_PORT}/ws;{WEST_LAND}=ws://127.0.0.1:{WEST_PORT}/ws");
		}

		private async Task<WorldHost> RaiseAsync(int port, string land, string neighbours)
		{
			string file = Path.Combine(Path.GetTempPath(), "wm-corner-" + Path.GetRandomFileName() + ".json");
			files.Add(file);

			Environment.SetEnvironmentVariable("WM_ZONE", land);
			Environment.SetEnvironmentVariable("WM_ZONE_NEIGHBOURS", neighbours);

			WorldHost host = new WorldHost(new WorldStore(file));
			WebApplication app = host.Build(Array.Empty<string>(), $"http://127.0.0.1:{port}");
			await app.StartAsync();
			worlds.Add(app);
			return host;
		}

		[TearDown]
		public async Task TearDown()
		{
			Environment.SetEnvironmentVariable("WM_ZONE", null);
			Environment.SetEnvironmentVariable("WM_ZONE_NEIGHBOURS", null);
			Environment.SetEnvironmentVariable("WM_ZONE_SECRET", null);

			foreach (WebApplication one in worlds)
			{
				await one.StopAsync();
				await one.DisposeAsync();
			}

			worlds.Clear();

			foreach (string path in files)
			{
				foreach (string one in new[] { path, path + ".bak", path + ".tmp" })
				{
					if (File.Exists(one))
						File.Delete(one);
				}
			}

			files.Clear();
		}

		[Test]
		public async Task 모서리에_서면_두_세계_사람이_같이_보인다()
		{
			using ClientWebSocket atCorner = await JoinAsync(EAST_PORT);
			using ClientWebSocket toTheWest = await JoinAsync(WEST_PORT);
			using ClientWebSocket toTheNorth = await JoinAsync(NORTH_PORT);
			await Task.Delay(500);

			// 셋 다 모서리(0, 40) 쪽으로 걸어 세운다 — 한 걸음은 MAX_STEP 까지라 여러 번 부른다.
			WalkTo(eastHost, new Vector3(1f, 0f, 39f));
			WalkTo(westHost, new Vector3(-1f, 0f, 39f));
			WalkTo(northHost, new Vector3(0f, 0f, 41f));

			string plate = await ReadUntilAsync(atCorner, text => ShadowsIn(text).Count >= 2, 15);
			HashSet<int> seen = ShadowsIn(plate);

			Assert.GreaterOrEqual(seen.Count, 2,
				"모서리에서 한쪽 세계만 보인다 — 이웃이 둘인 자리를 이웃 하나처럼 다뤘다");

			// 두 세계에서 온 사람의 번호가 겹치면 한 사람이 다른 한 사람을 덮어 지운다.
			Assert.AreEqual(2, seen.Count, "두 세계에서 왔는데 번호가 하나로 겹쳤다");
		}

		[Test]
		public async Task 그림자는_다시_옆_세계로_안_비친다()
		{
			// ★ 이게 무너지면 한 사람이 세계를 건너다니며 <b>여러 겹</b>으로 늘어난다(거울 두 장 사이).
			using ClientWebSocket atCorner = await JoinAsync(EAST_PORT);
			using ClientWebSocket toTheWest = await JoinAsync(WEST_PORT);
			await Task.Delay(500);

			WalkTo(eastHost, new Vector3(1f, 0f, 39f));
			WalkTo(westHost, new Vector3(-1f, 0f, 39f));
			await Task.Delay(1500);

			// 북쪽 세계는 <b>제 사람이 없다</b>. 그런데도 그림자가 있으면, 그건 동/서가 서로의
			// 그림자를 다시 퍼뜨린 것이다.
			Assert.AreEqual(0, northHost.World.Snapshot().Length, "북쪽에 사람을 안 넣었다");
			Assert.AreEqual(2, northHost.ShadowCount,
				"북쪽이 동·서의 <b>진짜 사람</b> 둘만 비쳐 봐야 한다 — 더 있으면 그림자가 다시 비친 것이다");
		}

		private static HashSet<int> ShadowsIn(string plate)
		{
			HashSet<int> found = new HashSet<int>();
			int at = 0;
			while (true)
			{
				at = plate.IndexOf("\"id\":-", at, StringComparison.Ordinal);
				if (at < 0)
					return found;

				at += "\"id\":".Length;
				int end = at;
				while (end < plate.Length && (plate[end] == '-' || char.IsDigit(plate[end])))
					end += 1;

				found.Add(int.Parse(plate.Substring(at, end - at)));
				at = end;
			}
		}

		private static void WalkTo(WorldHost host, Vector3 spot)
		{
			int doll = host.World.Snapshot()[0].Id;
			for (int step = 0; step < 80; step++)
			{
				Vector3 now = host.World.PositionOf(doll);
				host.World.TryMove(doll, new Vector3(spot.x - now.x, 0f, spot.z - now.z));
			}
		}

		private static async Task<ClientWebSocket> JoinAsync(int port)
		{
			ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws"), CancellationToken.None);

			byte[] hello = Encoding.UTF8.GetBytes("{\"type\":\"hello\",\"secret\":\"\"}");
			await window.SendAsync(new ArraySegment<byte>(hello), WebSocketMessageType.Text, true, CancellationToken.None);
			return window;
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
