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
	/// <b>지은 것을 무를 수 있나</b> — 부수기 왕복 (TASK-WM-217 ④).
	///
	/// ★ 왜: 짓기만 재고 부수기를 안 재면 「지은 것을 무를 수 없는 세계」가 초록으로 지나간다.
	///   그리고 부순 자리가 안 비면 <b>그 칸에는 이제 아무것도 못 짓는다</b> — 사람 눈에는
	///   「거기만 이상하다」로 보인다. 되돌려주는 재료도 규칙이 있다: 전액이면 남의 집을 부숴
	///   재료를 버는 길이 열리고, 0이면 잘못 지었을 때 손해만 남아 아무도 안 짓는다.
	/// </summary>
	public sealed class BuildRemoveRoundTripTests
	{
		private const int PORT = 5409;
		private const int BIG_BUILDING = 3; // 2×2 임시 블럭 — 여러 칸 건물이 통째로 사라지나까지 본다.

		private static readonly Uri address = new Uri($"ws://127.0.0.1:{PORT}/ws");

		private WebApplication app;
		private WorldHost host;
		private string worldFile;

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-remove-" + Path.GetRandomFileName() + ".json");
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
		public async Task 부수면_여러_칸_건물이_통째로_사라진다()
		{
			using ClientWebSocket window = new ClientWebSocket();
			int dollId = await Join(window);

			Cost(out int itemId, out int amount);
			host.World.TryGather(dollId, ServerItemCatalog.Find(itemId), amount);

			await Send(window, Place(20, 20));
			await Task.Delay(300);
			Assert.AreEqual(1, host.World.Buildings().Length, "짓지도 못했다 — 이 시험은 그 다음을 재는 것이다");
			Assert.AreEqual(2, host.World.Buildings()[0].Size.x, "2×2 를 지었는데 세계가 그 크기로 모른다");

			// 모서리가 아니라 <b>가운데 아무 칸</b>을 찍어도 그 건물이 사라져야 한다
			// (사람은 「건물」을 부수지 「칸」을 부수지 않는다).
			await Send(window, Remove(19, 21));
			await Task.Delay(300);

			Assert.AreEqual(0, host.World.Buildings().Length,
				"부순 건물이 남아 있으면 그 칸에는 이제 아무것도 못 짓는다");
		}

		[Test]
		public async Task 부수면_재료를_절반_돌려받는다()
		{
			using ClientWebSocket window = new ClientWebSocket();
			int dollId = await Join(window);

			Cost(out int itemId, out int amount);
			host.World.TryGather(dollId, ServerItemCatalog.Find(itemId), amount);

			await Send(window, Place(30, 30));
			await Task.Delay(300);
			Assert.AreEqual(0, host.World.BagCount(dollId, itemId), "짓기가 재료를 안 썼다");

			await Send(window, Remove(30, 30));
			await Task.Delay(300);

			Assert.AreEqual(amount / 2, host.World.BagCount(dollId, itemId),
				"전액이면 남의 집을 부숴 재료를 버는 길이 열리고, 0이면 잘못 지었을 때 손해만 남는다");
		}

		[Test]
		public async Task 빈_칸을_부수면_아무_일도_안_일어난다()
		{
			using ClientWebSocket window = new ClientWebSocket();
			int dollId = await Join(window);

			Cost(out int itemId, out int _);
			await Send(window, Remove(77, 77));
			await Task.Delay(300);

			Assert.AreEqual(0, host.World.BagCount(dollId, itemId),
				"빈 칸을 찍어 재료가 생기면 그건 무한히 캐는 길이다");
		}

		[Test]
		public async Task 부순_자리에_다시_지을_수_있다()
		{
			using ClientWebSocket window = new ClientWebSocket();
			int dollId = await Join(window);

			Cost(out int itemId, out int amount);
			host.World.TryGather(dollId, ServerItemCatalog.Find(itemId), amount * 3);

			await Send(window, Place(40, 40));
			await Task.Delay(250);
			await Send(window, Remove(40, 40));
			await Task.Delay(250);
			await Send(window, Place(40, 40));
			await Task.Delay(300);

			Assert.AreEqual(1, host.World.Buildings().Length,
				"부순 자리에 다시 못 지으면, 사람 눈에는 「거기만 이상한 땅」이 된다");
		}

		private static void Cost(out int itemId, out int amount)
		{
			ServerBuildingCatalog.Catalog.TryCost(BIG_BUILDING, out itemId, out amount);
			Assert.Greater(amount, 0, "공짜로 지어지면 부수기 환급을 잴 수가 없다");
		}

		private static string Place(int x, int z)
		{
			return "{\"type\":\"" + Protocol.PLACE + "\",\"x\":" + x + ",\"y\":0,\"z\":" + z
				+ ",\"buildingId\":" + BIG_BUILDING + "}";
		}

		private static string Remove(int x, int z)
		{
			return "{\"type\":\"" + Protocol.REMOVE + "\",\"x\":" + x + ",\"y\":0,\"z\":" + z + "}";
		}

		private static async Task<int> Join(ClientWebSocket window)
		{
			await window.ConnectAsync(address, CancellationToken.None);
			string welcome = await Read(window, "\"welcome\"");
			int dollId = JsonDocument.Parse(welcome).RootElement.GetProperty("id").GetInt32();

			await Send(window, "{\"type\":\"hello\",\"secret\":\"기기-부수기\"}");
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
