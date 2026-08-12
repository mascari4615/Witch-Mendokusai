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
	/// <b>하루가 지나도 세계가 맞나</b> (TASK-WM-217/218).
	///
	/// ★ 왜: 시계·재생·저장은 각각 초록인데 <b>같이 흐르면</b> 어긋난다. 이미 한 번 밟았다 —
	///   시각은 사람이 없어도 흐르는데 저장은 사람이 있을 때만 해서, 서버를 껐다 켜면
	///   <b>시계가 뒤로 감겼다</b>(7:34 → 6:45). 지은 건 남는데 시간만 되돌아가는 세계는 이상하다.
	///   그리고 뽑아 간 들판이 「다시 자랄 때」를 잘못 적으면, 껐다 켠 뒤 영영 안 자라거나
	///   한꺼번에 다 자란다.
	/// </summary>
	public sealed class WorldOverTimeTests
	{
		private const int PORT = 5407;
		private static readonly Uri address = new Uri($"ws://127.0.0.1:{PORT}/ws");

		private WebApplication app;
		private WorldHost host;
		private string worldFile;

		[SetUp]
		public void SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-time-" + Path.GetRandomFileName() + ".json");
		}

		[TearDown]
		public async Task TearDown()
		{
			await StopAsync();

			foreach (string path in new[] { worldFile, worldFile + ".bak", worldFile + ".tmp" })
			{
				if (File.Exists(path))
					File.Delete(path);
			}
		}

		[Test]
		public async Task 시계는_껐다_켜도_뒤로_안_감긴다()
		{
			await StartAsync();

			// 아무도 없는 사이에도 세계의 시간은 흐른다 — 그게 서버가 굴리는 이유다.
			host.World.AdvanceMinutes(60 * 8);
			int before = host.World.Calendar.TotalMinutes();

			await StopAsync();
			await StartAsync();

			Assert.GreaterOrEqual(host.World.Calendar.TotalMinutes(), before,
				"껐다 켜니 시계가 뒤로 갔다 — 지은 건 남는데 시간만 되돌아가는 세계는 이상하다");
		}

		[Test]
		public async Task 뽑아_간_자리는_껐다_켜도_때가_되면_자란다()
		{
			await StartAsync();

			GatherableNode node = host.World.Gatherables.Alive(host.World.Calendar.TotalMinutes())[0];
			Assert.IsTrue(host.World.Gatherables.TryTake(node.Id, node.X, node.Z,
				host.World.Calendar.TotalMinutes(), out int _, out int _));

			Assert.IsFalse(Standing(host, node.Id), "방금 뽑았는데 그대로 서 있다");

			await StopAsync();
			await StartAsync();

			// 되살린 세계도 「아직 때가 아니다」를 알아야 한다.
			Assert.IsFalse(Standing(host, node.Id), "껐다 켜니 뽑아 간 것이 공짜로 돌아왔다");

			// 하루를 넘겨 흐르면 다시 서 있어야 한다(재생 시간은 씨앗이 정한다: 가장 긴 것도 6시간).
			host.World.AdvanceMinutes(60 * 24);
			Assert.IsTrue(Standing(host, node.Id), "때가 지났는데도 안 자라면 들판이 한 번 쓰고 끝나는 세계다");
		}

		[Test]
		public async Task 오래_돌아도_사람과_집이_함께_남는다()
		{
			await StartAsync();

			using (ClientWebSocket window = new ClientWebSocket())
			{
				await window.ConnectAsync(address, CancellationToken.None);
				string welcome = await Read(window, "\"welcome\"");
				int dollId = JsonDocument.Parse(welcome).RootElement.GetProperty("id").GetInt32();

				await Send(window, "{\"type\":\"hello\",\"secret\":\"기기-오래\"}");
				mine = KeyIn(await Read(window, "\"identityId\""));

				host.World.TryGather(dollId, ServerItemCatalog.Find(WorldSeeds.WOOD), 5);
				ServerBuildingCatalog.Catalog.TryCost(WorldSim.CAULDRON_BUILDING_ID, out int _, out int cost);

				await Send(window, "{\"type\":\"" + Protocol.PLACE + "\",\"x\":50,\"y\":0,\"z\":50,\"buildingId\":"
					+ WorldSim.CAULDRON_BUILDING_ID + "}");

				await Read(window, "\"type\":\"bag\"");
				Assert.AreEqual(5 - cost, host.World.BagCount(dollId, WorldSeeds.WOOD), "짓기가 재료를 안 썼다");
			}

			// 사흘이 흐른다.
			host.World.AdvanceMinutes(60 * 24 * 3);

			await StopAsync();
			await StartAsync();

			Assert.AreEqual(1, host.World.Buildings().Length, "사흘 지나니 지은 집이 사라졌다");

			using (ClientWebSocket today = new ClientWebSocket())
			{
				await today.ConnectAsync(address, CancellationToken.None);
				await Read(today, "\"welcome\"");
				await Send(today, "{\"type\":\"hello\",\"secret\":\"" + mine + "\"}");
				await Read(today, "\"identityId\"");

				await Send(today, "{\"type\":\"" + Protocol.BAG_ASK + "\"}");
				string bag = await Read(today, "\"type\":\"bag\"");

				StringAssert.Contains("\"itemId\":" + WorldSeeds.WOOD, bag,
					"사흘 뒤에 오니 가방이 비었다 — 오래 안 오면 잃는 세계는 아무도 안 논다");
			}
		}

		private string mine;

		private static string KeyIn(string welcome)
		{
			string key = JsonDocument.Parse(welcome).RootElement.GetProperty("secret").GetString();
			Assert.IsNotEmpty(key, "서버가 열쇠를 안 줬다 — 다시 들어올 길이 없다");
			return key;
		}

		private static bool Standing(WorldHost world, int nodeId)
		{
			foreach (GatherableNode node in world.World.Gatherables.Alive(world.World.Calendar.TotalMinutes()))
			{
				if (node.Id == nodeId)
					return true;
			}

			return false;
		}

		private async Task StartAsync()
		{
			host = new WorldHost(new WorldStore(worldFile));
			app = host.Build(Array.Empty<string>(), $"http://127.0.0.1:{PORT}");
			await app.StartAsync();
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
