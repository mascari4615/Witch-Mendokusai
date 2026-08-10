using System;
using System.Collections.Generic;
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
	/// <b>가방이 꽉 찼을 때</b> — 놀이가 조용히 깨지는 자리 (TASK-WM-217).
	///
	/// ★ 왜: 줍기는 루프의 첫 칸이다. 가방이 차면 세 가지가 갈릴 수 있다 —
	///   ① 세계의 것이 사라지고 손에도 안 들어온다(그냥 잃는다)
	///   ② 아무 일도 안 일어나는데 <b>왜인지 안 알려 준다</b>(사람은 버튼이 고장 난 줄 안다)
	///   ③ 일부만 들어가고 나머지가 증발한다
	///   셋 다 화면은 멀쩡하고 시험도 초록이다 — 그래서 여기서 잰다.
	/// </summary>
	public sealed class FullBagTests
	{
		private const int PORT = 5432;

		private static readonly Uri address = new Uri($"ws://127.0.0.1:{PORT}/ws");

		private WebApplication app;
		private WorldHost host;
		private string worldFile;

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-fullbag-" + Path.GetRandomFileName() + ".json");
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
		public async Task 가방이_꽉_차면_세계의_것이_사라지지_않고_이유도_알려_준다()
		{
			using ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);

			string welcome = await Read(window, "\"welcome\"");
			int dollId = JsonDocument.Parse(welcome).RootElement.GetProperty("id").GetInt32();

			GatherableNode node = Nearest(dollId);
			WalkTo(dollId, node.X, node.Z);

			FillBag(dollId);
			Assert.IsFalse(host.World.CanReceive(dollId, ServerItemCatalog.Find(node.ItemId), node.Amount),
				"가방을 못 채웠으면 이 시험은 아무것도 안 재는 것이다");

			await Send(window, "{\"type\":\"" + Protocol.GATHER + "\",\"nodeId\":" + node.Id + "}");
			await Task.Delay(300);

			// ① 세계의 것이 그대로 서 있어야 한다 — 자리도 비고 손도 비면 그냥 잃은 것이다.
			Assert.IsTrue(StillStanding(node.Id),
				"가방이 꽉 찼는데 주울 것이 세계에서 사라졌다 — 그건 그냥 없어진 것이다");

			// ② 왜 안 되는지 말해 줘야 한다 — 아무 말도 없으면 사람은 버튼이 고장 난 줄 안다.
			string denied = await Read(window, "\"what\":\"" + Protocol.DENIED_GATHER + "\"");
			string why = JsonDocument.Parse(denied).RootElement.GetProperty("why").GetString();

			Assert.IsTrue(why.Contains("가방"),
				$"거절 이유가 「{why}」 — 가방 때문인 줄 모르면 사람은 계속 누르기만 한다");
		}

		[Test]
		public async Task 자리는_있는데_다_못_받으면_남는_것이_증발하지_않는다()
		{
			using ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);

			string welcome = await Read(window, "\"welcome\"");
			int dollId = JsonDocument.Parse(welcome).RootElement.GetProperty("id").GetInt32();

			// 여러 개 나오는 자리를 고른다 — 한 개짜리는 「일부만」이 성립하지 않는다.
			GatherableNode node = Nearest(dollId, atLeast: 2);
			WalkTo(dollId, node.X, node.Z);

			// 한 개만 들어갈 만큼 남기고 채운다.
			FillBagLeaving(dollId, ServerItemCatalog.Find(node.ItemId), 1);

			int before = host.World.BagCount(dollId, node.ItemId);
			await Send(window, "{\"type\":\"" + Protocol.GATHER + "\",\"nodeId\":" + node.Id + "}");
			await Task.Delay(300);

			int got = host.World.BagCount(dollId, node.ItemId) - before;
			Assert.Greater(got, 0, "자리가 있는데 하나도 못 받았다");

			int leftOnGround = AmountStanding(node.Id);
			Assert.AreEqual(node.Amount, got + leftOnGround,
				$"{node.Amount}개 중 {got}개만 받고 땅에도 {leftOnGround}개뿐 — 나머지가 증발했다");
		}

		/// <summary>그 사람에게 가장 가까운 주울 것 (몇 개 이상 나오는 것으로).</summary>
		private GatherableNode Nearest(int dollId, int atLeast = 1)
		{
			Vector3 standing = host.World.PositionOf(dollId);
			GatherableNode best = default;
			float bestDistance = float.MaxValue;
			bool found = false;

			foreach (GatherableNode node in host.World.Gatherables.Alive(host.World.Calendar.TotalMinutes()))
			{
				if (node.Amount < atLeast)
					continue;

				float dx = node.X - standing.x;
				float dz = node.Z - standing.z;
				float distance = (dx * dx) + (dz * dz);
				if (distance >= bestDistance)
					continue;

				best = node;
				bestDistance = distance;
				found = true;
			}

			Assert.IsTrue(found, $"{atLeast}개 이상 나오는 주울 것이 세계에 없다 — 그러면 줍기가 놀이가 아니다");
			return best;
		}

		private bool StillStanding(int nodeId) => AmountStanding(nodeId) > 0;

		private int AmountStanding(int nodeId)
		{
			foreach (GatherableNode node in host.World.Gatherables.Alive(host.World.Calendar.TotalMinutes()))
			{
				if (node.Id == nodeId)
					return node.Amount;
			}

			return 0;
		}

		/// <summary>가방을 더 못 받을 때까지 채운다 — 세계가 아는 물건으로.</summary>
		private void FillBag(int dollId)
		{
			foreach (IItemData item in Fillers())
			{
				for (int round = 0; round < 200; round++)
				{
					if (host.World.TryGather(dollId, item, item.MaxAmount) > 0)
						break;
				}
			}
		}

		/// <summary>그 물건 <paramref name="room"/> 개만 들어갈 자리를 남기고 채운다.</summary>
		private void FillBagLeaving(int dollId, IItemData item, int room)
		{
			// ⚠ 그 물건부터 먼저 넣는다 — 목록 순서대로 채우면 칸이 먼저 동나서
			//   정작 이 물건은 가방에 <b>한 개도 없는</b> 상태가 된다(그러면 덜어 낼 것도 없다).
			host.World.TryGather(dollId, item, item.MaxAmount);
			FillBag(dollId);

			// 꽉 채운 뒤 그 물건을 room 개만큼 덜어 낸다 — 그 자리에만 빈틈이 생긴다.
			Assert.AreEqual(0, host.World.TryConsume(dollId, item.ID, room),
				"채운 물건을 덜어 내지 못했다 — 시험이 원하는 상태를 못 만든다");
		}

		private static IEnumerable<IItemData> Fillers()
		{
			// 한 종류로는 칸이 다 안 찰 수 있다(쌓이는 개수 한도 때문에) — 아는 것을 모두 쓴다.
			foreach (KeyValuePair<int, string> known in ServerItemCatalog.Catalog.Names())
			{
				IItemData item = ServerItemCatalog.Find(known.Key);
				if (item != null)
					yield return item;
			}
		}

		private void WalkTo(int dollId, float x, float z)
		{
			for (int step = 0; step < 400; step++)
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
