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
	/// <b>둘이 같은 것을 노릴 때</b> 세계가 한쪽만 준다 (TASK-WM-217).
	///
	/// ★ 왜: 지금까지 잰 것은 「혼자 다 되나」였다. 그런데 같이 노는 세계에서 진짜 무서운 자리는
	///   <b>겹치는 순간</b>이다 — 같은 칸에 둘이 짓고, 같은 것을 둘이 줍고, 같은 이름을 둘이 쓴다.
	///   여기서 둘 다 되면 물건이 복제되고, 둘 다 안 되면 게임이 멈춘다. 한쪽만 돼야 한다.
	/// </summary>
	public sealed class TwoPeopleContentionTests
	{
		private const int PORT = 5403;
		private static readonly Uri address = new Uri($"ws://127.0.0.1:{PORT}/ws");

		private WebApplication app;
		private WorldHost host;
		private string worldFile;

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-two-" + Path.GetRandomFileName() + ".json");
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
		public async Task 같은_칸에_둘이_지으면_한_채만_선다()
		{
			(ClientWebSocket first, int firstDoll) = await Join("기기-첫째");
			(ClientWebSocket second, int secondDoll) = await Join("기기-둘째");

			using (first)
			using (second)
			{
				// 재료는 세계가 쥐여 준다 — 여기서 재는 것은 「줍기」가 아니라 「겹침」이다.
				GiveBuildCost(firstDoll);
				GiveBuildCost(secondDoll);

				await Send(first, Place(4, 4));
				await Send(second, Place(4, 4));
				await Task.Delay(300);

				Assert.AreEqual(1, host.World.Buildings().Length,
					"둘 다 서면 한 칸에 두 채가 겹친다 — 그 뒤로 짓기 판정이 영영 이상해진다");
			}
		}

		[Test]
		public async Task 진_쪽은_재료를_돌려받는다()
		{
			(ClientWebSocket first, int firstDoll) = await Join("기기-A");
			(ClientWebSocket second, int secondDoll) = await Join("기기-B");

			using (first)
			using (second)
			{
				GiveBuildCost(firstDoll);
				GiveBuildCost(secondDoll);

				await Send(first, Place(6, 6));
				await Task.Delay(200);
				await Send(second, Place(6, 6));
				await Task.Delay(300);

				// 못 지었는데 재료만 사라지면, 사람 눈엔 「도둑맞았다」다.
				int leftover = host.World.BagCount(secondDoll, CostItemId()) + host.World.BagCount(firstDoll, CostItemId());
				Assert.AreEqual(CostAmount(), leftover,
					"진 쪽 재료가 안 돌아오면 겹칠 때마다 재료가 조용히 사라진다");
			}
		}

		[Test]
		public async Task 같은_것을_둘이_주우면_한_사람만_가진다()
		{
			(ClientWebSocket first, int firstDoll) = await Join("기기-C");
			(ClientWebSocket second, int secondDoll) = await Join("기기-D");

			using (first)
			using (second)
			{
				// 둘을 같은 자리에 세운다 — 손이 닿아야 줍힌다(자리는 세계가 본다).
				System.Collections.Generic.List<GatherableNode> alive =
					host.World.Gatherables.Alive(host.World.Calendar.TotalMinutes());

				GatherableNode node = alive[0];
				WalkTo(firstDoll, node.X, node.Z);
				WalkTo(secondDoll, node.X, node.Z);

				await Send(first, "{\"type\":\"" + Protocol.GATHER + "\",\"nodeId\":" + node.Id + "}");
				await Send(second, "{\"type\":\"" + Protocol.GATHER + "\",\"nodeId\":" + node.Id + "}");
				await Task.Delay(300);

				int mine = host.World.BagCount(firstDoll, node.ItemId);
				int theirs = host.World.BagCount(secondDoll, node.ItemId);

				Assert.AreEqual(node.Amount, mine + theirs,
					"둘 다 받으면 물건이 복제된다 — 주울 이유가 사라진다");
			}
		}

		[Test]
		public async Task 같은_이름은_나중_사람이_거절당한다()
		{
			(ClientWebSocket first, int _) = await Join("기기-E");
			(ClientWebSocket second, int _2) = await Join("기기-F");

			using (first)
			using (second)
			{
				await Send(first, "{\"type\":\"" + Protocol.RENAME + "\",\"name\":\"링\"}");
				await Task.Delay(200);

				await Send(second, "{\"type\":\"" + Protocol.RENAME + "\",\"name\":\"링\"}");
				string denied = await Read(second, "\"type\":\"denied\"");

				StringAssert.Contains("이미 그렇게 불리는", denied,
					"둘이 같은 이름이면 「누가 누군지」가 무너진다");
			}
		}

		/// <summary>
		/// 그 자리까지 <b>걸어간다</b> — 한 걸음은 잘린다(순간이동 금지).
		/// ★ 목적지 좌표를 한 통에 담아 보내면 1.5 만큼만 간다(실측 2026-08-10, 두 번째로 밟은 함정).
		/// </summary>
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

		private static int CostItemId()
		{
			ServerBuildingCatalog.Catalog.TryCost(WorldSim.CAULDRON_BUILDING_ID, out int itemId, out int _);
			return itemId;
		}

		private static int CostAmount()
		{
			ServerBuildingCatalog.Catalog.TryCost(WorldSim.CAULDRON_BUILDING_ID, out int _, out int amount);
			return amount;
		}

		private void GiveBuildCost(int dollId)
		{
			host.World.TryGather(dollId, ServerItemCatalog.Find(CostItemId()), CostAmount());
		}

		private static string Place(int x, int z)
		{
			return "{\"type\":\"" + Protocol.PLACE + "\",\"x\":" + x + ",\"y\":0,\"z\":" + z
				+ ",\"buildingId\":" + WorldSim.CAULDRON_BUILDING_ID + "}";
		}

		private static async Task<(ClientWebSocket socket, int dollId)> Join(string secret)
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
			byte[] buffer = new byte[16384];
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
