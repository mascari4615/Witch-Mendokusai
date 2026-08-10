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
	/// <b>둘이 함께 쓴다</b> — 상자와 솥 (TASK-WM-217).
	///
	/// ★ 왜: 「같이 노는 세계」의 알맹이는 <b>나눔</b>이다. 한 사람이 넣은 것을 다른 사람이 꺼내고,
	///   한 사람이 저은 솥을 다른 사람이 이어 젓는다. 여기가 안 되면 둘이 붙어 있어도
	///   각자 혼자 노는 것과 같다 — 접속만 되는 세계다.
	///   순간 경합(같은 것을 동시에)은 따로 재고, 여기서는 <b>주고받기</b>를 잰다.
	/// </summary>
	public sealed class SharingTests
	{
		private const int PORT = 5429;
		private const int CHEST = 4005;

		private static readonly Uri address = new Uri($"ws://127.0.0.1:{PORT}/ws");

		private WebApplication app;
		private WorldHost host;
		private string worldFile;

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-share-" + Path.GetRandomFileName() + ".json");
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
		public async Task 내가_넣은_것을_남이_꺼내_간다()
		{
			(ClientWebSocket giver, int giverId) = await Join("기기-주는사람");
			(ClientWebSocket taker, int takerId) = await Join("기기-받는사람");

			using (giver)
			using (taker)
			{
				Vector3Int chest = new Vector3Int(30, 0, 30);
				BuildChest(giverId, chest);
				WalkTo(takerId, chest.x, chest.z);

				host.World.TryGather(giverId, ServerItemCatalog.Find(WorldSeeds.WOOD), 5);

				await Send(giver, ChestMessage(Protocol.CHEST_PUT, chest, WorldSeeds.WOOD, 5));
				await Task.Delay(250);

				await Send(taker, ChestMessage(Protocol.CHEST_TAKE, chest, WorldSeeds.WOOD, 5));
				await Task.Delay(250);

				Assert.AreEqual(0, host.World.BagCount(giverId, WorldSeeds.WOOD), "준 사람 손에 그대로 남아 있다");
				Assert.AreEqual(5, host.World.BagCount(takerId, WorldSeeds.WOOD),
					"남이 넣어 둔 것을 못 꺼내면, 둘이 붙어 있어도 각자 혼자 노는 것이다");
			}
		}

		[Test]
		public async Task 남이_저은_솥을_이어_젓는다()
		{
			(ClientWebSocket first, int firstId) = await Join("기기-먼저젓는사람");
			(ClientWebSocket second, int secondId) = await Join("기기-이어젓는사람");

			using (first)
			using (second)
			{
				Vector3Int pot = new Vector3Int(35, 0, 35);
				BuildPot(firstId, pot);
				WalkTo(secondId, pot.x, pot.z);

				host.World.TryGather(firstId, ServerItemCatalog.Find(WorldSeeds.WOOD), 1);
				host.World.TryGather(secondId, ServerItemCatalog.Find(WorldSeeds.WOOD), 1);

				await Send(first, Brew(pot, WorldSeeds.WOOD));
				await Task.Delay(250);

				int afterFirst = host.World.Cauldrons.At(pot).State.StepCount;
				Assert.AreEqual(1, afterFirst, "첫 사람이 넣은 것이 솥에 안 들어갔다");

				await Send(second, Brew(pot, WorldSeeds.WOOD));
				await Task.Delay(250);

				Assert.AreEqual(2, host.World.Cauldrons.At(pot).State.StepCount,
					"남이 저은 솥을 이어 못 저으면 같이 만드는 일이 없다");
			}
		}

		[Test]
		public async Task 함께_저은_솥의_완성은_한_사람만_가져간다()
		{
			(ClientWebSocket first, int firstId) = await Join("기기-완성-A");
			(ClientWebSocket second, int secondId) = await Join("기기-완성-B");

			using (first)
			using (second)
			{
				Vector3Int pot = new Vector3Int(40, 0, 40);
				BuildPot(firstId, pot);
				WalkTo(secondId, pot.x, pot.z);

				host.World.TryGather(firstId, ServerItemCatalog.Find(WorldSeeds.WOOD), 1);
				await Send(first, Brew(pot, WorldSeeds.WOOD));
				await Task.Delay(250);

				// 둘이 동시에 「가져가겠다」 — 세계는 선착순 한 사람에게만 준다.
				await Send(first, Complete(pot));
				await Send(second, Complete(pot));
				await Task.Delay(400);

				int mine = host.World.BagCount(firstId, WorldSeeds.HEALING_POTION);
				int theirs = host.World.BagCount(secondId, WorldSeeds.HEALING_POTION);

				Assert.AreEqual(1, mine + theirs,
					"둘 다 받으면 물건이 복제되고, 둘 다 못 받으면 그 판이 통째로 사라진다");
			}
		}

		private void BuildChest(int dollId, Vector3Int cell)
		{
			ServerBuildingCatalog.Catalog.TryCost(CHEST, out int itemId, out int amount);
			host.World.TryGather(dollId, ServerItemCatalog.Find(itemId), amount);
			Assert.IsTrue(host.World.TryPlaceBuilding(cell, CHEST, host.World.Buildables), "상자를 못 지었다");
			host.World.TryConsume(dollId, itemId, amount);
			WalkTo(dollId, cell.x, cell.z);
		}

		private void BuildPot(int dollId, Vector3Int cell)
		{
			ServerBuildingCatalog.Catalog.TryCost(WorldSim.CAULDRON_BUILDING_ID, out int itemId, out int amount);
			host.World.TryGather(dollId, ServerItemCatalog.Find(itemId), amount);
			Assert.IsTrue(host.World.TryPlaceBuilding(cell, WorldSim.CAULDRON_BUILDING_ID, host.World.Buildables),
				"솥을 못 지었다");

			host.World.TryConsume(dollId, itemId, amount);
			WalkTo(dollId, cell.x, cell.z);
		}

		/// <summary>그 자리까지 걸어간다 — 한 걸음은 잘린다(순간이동 금지).</summary>
		private void WalkTo(int dollId, float x, float z)
		{
			for (int step = 0; step < 300; step++)
			{
				Vector3 standing = host.World.PositionOf(dollId);
				float dx = x - standing.x;
				float dz = z - standing.z;
				if ((dx * dx) + (dz * dz) < 0.01f)
					return;

				host.World.TryMove(dollId, new Vector3(dx, 0f, dz));
			}
		}

		private static string ChestMessage(string kind, Vector3Int cell, int itemId, int amount)
		{
			return "{\"type\":\"" + kind + "\",\"x\":" + cell.x + ",\"y\":0,\"z\":" + cell.z
				+ ",\"itemId\":" + itemId + ",\"amount\":" + amount + "}";
		}

		private static string Brew(Vector3Int pot, int itemId)
		{
			return "{\"type\":\"" + Protocol.BREW + "\",\"itemId\":" + itemId
				+ ",\"x\":" + pot.x + ",\"y\":0,\"z\":" + pot.z + "}";
		}

		private static string Complete(Vector3Int pot)
		{
			return "{\"type\":\"" + Protocol.BREW_COMPLETE + "\",\"x\":" + pot.x + ",\"y\":0,\"z\":" + pot.z + "}";
		}

		private async Task<(ClientWebSocket window, int dollId)> Join(string secret)
		{
			ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);

			string welcome = await Read(window, "\"welcome\"");
			int dollId = JsonDocument.Parse(welcome).RootElement.GetProperty("id").GetInt32();

			await Send(window, "{\"type\":\"hello\",\"secret\":\"" + secret + "\"}");
			await Read(window, "\"identityId\"");
			return (window, dollId);
		}

		private static async Task Send(ClientWebSocket socket, string json)
		{
			byte[] payload = Encoding.UTF8.GetBytes(json);
			await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, CancellationToken.None);
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
