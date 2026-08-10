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
	/// <b>만든 것이 다음 재료가 된다</b> — 사슬 (TASK-WM-217).
	///
	/// ★ 왜: 「놀 수 있나」 다음 물음은 「계속 놀 이유가 있나」다. 주운 것으로 만든 것이
	///   <b>더 좋은 것의 재료</b>가 되어야 앞으로 나아가는 느낌이 생긴다.
	///   세계에는 그 사슬이 실재한다: 석탄을 주워 솥에 넣으면 <b>석재</b>가 나오고,
	///   모루는 석재로 짓는다. 여기가 끊기면 첫날 할 일을 마치고 나면 더 할 게 없다.
	/// </summary>
	public sealed class MaterialChainTests
	{
		private const int PORT = 5431;
		private const int ANVIL = 4001;   // 모루 — 석재로 짓는다
		private const int STONE = 10;     // 석재 — 솥에서 나온다

		private static readonly Uri address = new Uri($"ws://127.0.0.1:{PORT}/ws");

		private WebApplication app;
		private WorldHost host;
		private string worldFile;

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-chain-" + Path.GetRandomFileName() + ".json");
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
		public void 모루는_주워서는_못_짓고_만들어야_짓는다()
		{
			// 모루가 무엇을 요구하나 — 그것이 <b>들판에서 바로 줍는 것</b>이면 사슬이 아니다.
			Assert.IsTrue(ServerBuildingCatalog.Catalog.TryCost(ANVIL, out int itemId, out int amount));
			Assert.AreEqual(STONE, itemId, "모루가 석재를 안 쓰면 이 사슬 자체가 없다");
			Assert.Greater(amount, 0);

			foreach (GatherableNode node in host.World.Gatherables.Alive(0))
			{
				Assert.AreNotEqual(STONE, node.ItemId,
					"석재를 들판에서 바로 주우면 「만들어서 쓴다」가 뜻을 잃는다");
			}
		}

		[Test]
		public async Task 주운_것으로_만든_것이_다음_건물의_재료가_된다()
		{
			using ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);

			string welcome = await Read(window, "\"welcome\"");
			int dollId = JsonDocument.Parse(welcome).RootElement.GetProperty("id").GetInt32();

			await Send(window, "{\"type\":\"hello\",\"secret\":\"기기-사슬\"}");
			await Read(window, "\"identityId\"");

			// ① 솥을 짓는다(나무로).
			Vector3Int pot = new Vector3Int(70, 0, 70);
			ServerBuildingCatalog.Catalog.TryCost(WorldSim.CAULDRON_BUILDING_ID, out int woodId, out int woodCost);
			host.World.TryGather(dollId, ServerItemCatalog.Find(woodId), woodCost);
			Assert.IsTrue(host.World.TryPlaceBuilding(pot, WorldSim.CAULDRON_BUILDING_ID, host.World.Buildables));
			host.World.TryConsume(dollId, woodId, woodCost);
			WalkTo(dollId, pot.x, pot.z);

			// ② 석탄을 주워 솥에 넣는다 — 석탄은 「석재」 쪽으로 젓는 재료다.
			ServerBuildingCatalog.Catalog.TryCost(ANVIL, out int stoneId, out int stoneCost);
			// ★ 한 걸음으로는 석재에 안 닿는다 (실측 2026-08-10): 석탄 한 번은 (0, 0.5) 까지 가고
			//   석재는 (0, 2) 에 있다. <b>세 번 저어야</b> 그 쪽에 닿는다 — 그게 이 사슬의 실제 모양이고,
			//   한 번만 넣으면 가운데(치유 물약)가 나온다. 「재료를 모아 여러 번 젓는다」가 곧 진행이다.
			const int STIRS_TO_STONE = 3;

			int made = 0;
			for (int round = 0; round < 8 && made < stoneCost; round++)
			{
				for (int stir = 0; stir < STIRS_TO_STONE; stir++)
				{
					host.World.TryGather(dollId, ServerItemCatalog.Find(WorldSeeds.COAL), 1);

					await Send(window, "{\"type\":\"" + Protocol.BREW + "\",\"itemId\":" + WorldSeeds.COAL
						+ ",\"x\":" + pot.x + ",\"y\":0,\"z\":" + pot.z + "}");

					await Task.Delay(120);
				}

				await Send(window, "{\"type\":\"" + Protocol.BREW_COMPLETE + "\",\"x\":" + pot.x
					+ ",\"y\":0,\"z\":" + pot.z + "}");

				await Task.Delay(200);
				made = host.World.BagCount(dollId, stoneId);
			}

			Assert.GreaterOrEqual(made, stoneCost,
				$"솥에서 석재가 안 나오면 사슬이 첫 칸에서 끊긴다 (지금 {made}개)");

			// ③ 그 석재로 모루를 짓는다 — <b>만든 것이 다음 재료</b>다.
			await Send(window, "{\"type\":\"" + Protocol.PLACE + "\",\"x\":74,\"y\":0,\"z\":70,\"buildingId\":" + ANVIL + "}");
			await Task.Delay(300);

			bool standing = false;
			foreach (PlacedBuilding building in host.World.Buildings())
			{
				if (building.BuildingId == ANVIL)
					standing = true;
			}

			Assert.IsTrue(standing,
				"만든 것으로 다음 것을 못 지으면, 첫날 할 일을 마치고 나서 더 할 게 없다");

			Assert.AreEqual(made - stoneCost, host.World.BagCount(dollId, stoneId),
				"지었는데 재료가 안 빠지면 그건 공짜다");
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
