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
	/// <b>한 바퀴가 아니라 계속 돌 수 있나</b> (TASK-WM-217).
	///
	/// ★ 왜: 관문은 한 바퀴를 잰다 — 줍고, 짓고, 조리하고, 끝. 그런데 게임은 <b>두 번째 바퀴부터</b>가
	///   진짜다. 같은 솥을 다시 쓰고, 같은 상자에 또 넣고, 재료가 쌓였다 줄어든다.
	///   여기서 어긋나면 「처음엔 되는데 두 번째부터 이상한」 세계가 된다 — 그건 아무도 안 논다.
	/// </summary>
	public sealed class KeepPlayingTests
	{
		private const int PORT = 5413;
		private static readonly Uri address = new Uri($"ws://127.0.0.1:{PORT}/ws");

		private WebApplication app;
		private WorldHost host;
		private string worldFile;

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-keep-" + Path.GetRandomFileName() + ".json");
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
		public async Task 같은_솥으로_두_번_조리한다()
		{
			using ClientWebSocket window = new ClientWebSocket();
			int dollId = await Join(window, "기기-두바퀴");

			Vector3Int pot = new Vector3Int(12, 0, 12);
			BuildPot(dollId, pot);

			// 첫 바퀴 — 나무 한 걸음이면 치유 물약 쪽에 닿는다.
			int first = await BrewOnce(window, dollId, pot);
			Assert.AreNotEqual(0, first, "첫 바퀴부터 못 만들면 이 시험은 뜻이 없다");

			// 둘째 바퀴 — 같은 솥, 같은 사람.
			int second = await BrewOnce(window, dollId, pot);
			Assert.AreEqual(first, second,
				"두 번째 바퀴에서 다른 게 나오면 「처음엔 되는데 그다음이 이상한」 세계다");

			Assert.AreEqual(2, host.World.BagCount(dollId, first), "두 번 만들었으면 두 개여야 한다");
		}

		[Test]
		public async Task 완성한_솥은_비어서_다시_쓸_수_있다()
		{
			using ClientWebSocket window = new ClientWebSocket();
			int dollId = await Join(window, "기기-빈솥");

			Vector3Int pot = new Vector3Int(16, 0, 16);
			BuildPot(dollId, pot);
			await BrewOnce(window, dollId, pot);

			// 가져간 뒤 그 솥은 <b>비어</b> 있어야 한다 — 안 비면 다음 사람이 남의 자국 위에 젓는다.
			Assert.AreEqual(0, host.World.Cauldrons.At(pot).State.StepCount,
				"완성한 솥에 자국이 남으면 다음 조리가 엉뚱한 데서 시작한다");
		}

		[Test]
		public async Task 같은_상자에_넣고_빼기를_반복해도_수가_맞는다()
		{
			using ClientWebSocket window = new ClientWebSocket();
			int dollId = await Join(window, "기기-상자반복");

			ServerBuildingCatalog.Catalog.TryCost(4005, out int costItem, out int costAmount);
			host.World.TryGather(dollId, ServerItemCatalog.Find(costItem), costAmount);
			await Send(window, "{\"type\":\"" + Protocol.PLACE + "\",\"x\":20,\"y\":0,\"z\":20,\"buildingId\":4005}");
			await Task.Delay(250);

			host.World.TryGather(dollId, ServerItemCatalog.Find(WorldSeeds.WOOD), 6);

			for (int round = 0; round < 3; round++)
			{
				await Send(window, Chest(Protocol.CHEST_PUT, WorldSeeds.WOOD, 2));
				await Task.Delay(150);
				await Send(window, Chest(Protocol.CHEST_TAKE, WorldSeeds.WOOD, 2));
				await Task.Delay(150);
			}

			Assert.AreEqual(6, host.World.BagCount(dollId, WorldSeeds.WOOD),
				"넣고 빼기를 되풀이하며 수가 흔들리면, 오래 논 사람일수록 손해를 본다");
		}

		private void BuildPot(int dollId, Vector3Int cell)
		{
			ServerBuildingCatalog.Catalog.TryCost(WorldSim.CAULDRON_BUILDING_ID, out int itemId, out int amount);
			host.World.TryGather(dollId, ServerItemCatalog.Find(itemId), amount);
			Assert.IsTrue(host.World.TryPlaceBuilding(cell, WorldSim.CAULDRON_BUILDING_ID, host.World.Buildables),
				"솥을 못 지으면 조리를 잴 수 없다");

			host.World.TryConsume(dollId, itemId, amount);

			// 손이 닿아야 젓는다 — 세계가 거리를 본다.
			WalkTo(dollId, cell.x, cell.z);
		}

		/// <summary>그 자리까지 걸어간다 — 한 걸음은 잘린다(순간이동 금지).</summary>
		private void WalkTo(int dollId, float x, float z)
		{
			for (int step = 0; step < 200; step++)
			{
				Vector3 standing = host.World.PositionOf(dollId);
				float dx = x - standing.x;
				float dz = z - standing.z;
				if ((dx * dx) + (dz * dz) < 0.01f)
					return;

				host.World.TryMove(dollId, new Vector3(dx, 0f, dz));
			}
		}

		/// <summary>한 바퀴 — 재료 하나 넣고 완성을 가져간다. 무엇이 나왔는지 돌려준다.</summary>
		private async Task<int> BrewOnce(ClientWebSocket window, int dollId, Vector3Int pot)
		{
			host.World.TryGather(dollId, ServerItemCatalog.Find(WorldSeeds.WOOD), 1);

			await Send(window, "{\"type\":\"" + Protocol.BREW + "\",\"itemId\":" + WorldSeeds.WOOD
				+ ",\"x\":" + pot.x + ",\"y\":0,\"z\":" + pot.z + "}");

			await Task.Delay(200);

			await Send(window, "{\"type\":\"" + Protocol.BREW_COMPLETE + "\",\"x\":" + pot.x
				+ ",\"y\":0,\"z\":" + pot.z + "}");

			string taken = await Read(window, "\"type\":\"" + Protocol.BREW_TAKEN + "\"");
			return JsonDocument.Parse(taken).RootElement.GetProperty("itemId").GetInt32();
		}

		private static string Chest(string kind, int itemId, int amount)
		{
			return "{\"type\":\"" + kind + "\",\"x\":20,\"y\":0,\"z\":20,\"itemId\":" + itemId
				+ ",\"amount\":" + amount + "}";
		}

		private async Task<int> Join(ClientWebSocket window, string secret)
		{
			await window.ConnectAsync(address, CancellationToken.None);
			string welcome = await Read(window, "\"welcome\"");
			int dollId = JsonDocument.Parse(welcome).RootElement.GetProperty("id").GetInt32();

			await Send(window, "{\"type\":\"hello\",\"secret\":\"" + secret + "\"}");
			await Read(window, "\"identityId\"");
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
