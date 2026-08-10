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
	/// <b>날이 바뀌고, 그게 창에 실린다</b> (TASK-WM-217).
	///
	/// ★ 왜: 시각이 흐르는 것과 <b>날이 넘어가는 것</b>은 다른 일이다. 시가 24를 넘겨도 날이 안 바뀌면
	///   밤이 영원히 계속되고, 「내일 오면 다시 자라 있다」는 약속도 깨진다.
	///   그리고 넘어간 사실이 <b>창까지</b> 가야 사람이 안다 — 세계만 알고 화면이 모르면 없는 일이다.
	/// </summary>
	public sealed class DayTurnsTests
	{
		private const int PORT = 5427;
		private static readonly Uri address = new Uri($"ws://127.0.0.1:{PORT}/ws");

		private WebApplication app;
		private WorldHost host;
		private string worldFile;

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-day-" + Path.GetRandomFileName() + ".json");
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
		public async Task 하루가_지나면_날이_바뀌고_창이_안다()
		{
			using ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);
			await Read(window, "\"welcome\"");

			string firstTime = await Read(window, "\"time\"");
			int beforeDay = DayIn(firstTime);

			// 하루를 통째로 민다(세계의 하루 = 24시간).
			host.World.AdvanceMinutes(60 * 24);

			string afterTime = await Read(window, "\"day\":" + (beforeDay + 1));
			Assert.AreEqual(beforeDay + 1, DayIn(afterTime),
				"시각만 흐르고 날이 안 바뀌면 밤이 영원히 계속된다");
		}

		[Test]
		public async Task 시가_스물넷을_넘으면_다음_날_아침이다()
		{
			// 오늘 저녁부터 시작해 여덟 시간을 민다 — 자정을 넘긴다.
			host.World.Calendar.Set(1, 0, 1, 20, 0);
			int beforeDay = host.World.Calendar.Day;

			host.World.AdvanceMinutes(60 * 8);

			Assert.AreEqual(beforeDay + 1, host.World.Calendar.Day, "자정을 넘겼는데 같은 날이다");
			Assert.AreEqual(4, host.World.Calendar.Hour, "20시 + 8시간 = 다음 날 4시여야 한다");
		}

		[Test]
		public async Task 날이_바뀌어도_지은_것과_가진_것은_그대로다()
		{
			using ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);

			string welcome = await Read(window, "\"welcome\"");
			int dollId = JsonDocument.Parse(welcome).RootElement.GetProperty("id").GetInt32();

			ServerBuildingCatalog.Catalog.TryCost(WorldSim.CAULDRON_BUILDING_ID, out int itemId, out int amount);
			host.World.TryGather(dollId, ServerItemCatalog.Find(itemId), amount + 3);
			Assert.IsTrue(host.World.TryPlaceBuilding(
				new WitchMendokusai.Numerics.Vector3Int(60, 0, 60), WorldSim.CAULDRON_BUILDING_ID, host.World.Buildables));

			host.World.TryConsume(dollId, itemId, amount);
			host.World.AdvanceMinutes(60 * 24 * 2);

			Assert.AreEqual(1, host.World.Buildings().Length, "이틀 지나니 지은 것이 사라졌다");
			Assert.AreEqual(3, host.World.BagCount(dollId, itemId), "이틀 지나니 가진 것이 줄었다");
		}

		private static int DayIn(string snapshot)
		{
			using JsonDocument world = JsonDocument.Parse(snapshot);
			return world.RootElement.GetProperty("time").GetProperty("day").GetInt32();
		}

		private static async Task<string> Read(ClientWebSocket socket, string needle)
		{
			using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			byte[] buffer = new byte[65536];
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
