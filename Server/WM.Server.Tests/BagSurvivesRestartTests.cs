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
	/// <b>어제 주운 게 오늘도 내 가방에 있나</b> — 서버를 껐다 켜는 왕복 (TASK-WM-217 / 218).
	///
	/// ★ 왜 이걸 따로 재나: 사람의 가방을 적고 되살리는 길은 이미 있었지만
	///   (<c>SavePeople</c> / <c>LoadPeople</c> / <c>Adopt</c>), 그 길이 <b>진짜 서버를 껐다 켜는
	///   경로에서 이어져 있는지</b>는 아무도 안 쟀다. 그 사이 어디 한 군데만 끊겨도 증상은 하나다 —
	///   서버를 재시작한 날, 모두의 가방이 빈다. 그건 「오늘 한 게 내일 없다」와 같은 말이고,
	///   그 세계는 아무도 안 논다.
	///
	/// 진짜 소켓 · 진짜 저장 파일 · 진짜 두 번째 서버로 잰다(가짜 전송·목 없음).
	/// </summary>
	public sealed class BagSurvivesRestartTests
	{
		private const int PORT = 5399;
		private const string SECRET = "기기-어제";

		private static readonly Uri address = new Uri($"ws://127.0.0.1:{PORT}/ws");

		private WebApplication app;
		private WorldHost host;
		private string mine;
		private string worldFile;

		[SetUp]
		public void SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-bag-" + Path.GetRandomFileName() + ".json");
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
		public async Task 어제_주운_것이_서버를_껐다_켜도_가방에_남는다()
		{
			int gathered;
			int itemId;

			await StartAsync();
			using (ClientWebSocket yesterday = new ClientWebSocket())
			{
				await yesterday.ConnectAsync(address, CancellationToken.None);
				await Read(yesterday, "\"welcome\"");
				await Send(yesterday, "{\"type\":\"hello\",\"secret\":\"" + SECRET + "\"}");
				mine = KeyIn(await Read(yesterday, "\"identityId\""));

				(itemId, gathered) = await GatherOnce(yesterday);
				Assert.Greater(gathered, 0, "줍지도 못했다 — 이 시험은 그 다음을 재는 것이다");
			}

			// 여기서 세계가 디스크로 내려간다(멈출 때 한 번 적는다).
			await StopAsync();

			Assert.IsTrue(File.Exists(worldFile), "세계가 파일로 안 내려갔다 — 되살릴 것이 없다");

			// 두 번째 서버 = 새 살림. 같은 파일만 물려받는다.
			await StartAsync();
			using (ClientWebSocket today = new ClientWebSocket())
			{
				await today.ConnectAsync(address, CancellationToken.None);
				await Read(today, "\"welcome\"");
				// ★ 어제 <b>서버가 준 열쇠</b>로 들어온다 (실측 2026-08-10): 창이 지어낸 열쇠는
				//   그 사람이 아니다 — 서버가 새 사람으로 맞아들이고, 어제의 가방은 남의 것이 된다.
				//   진짜 창도 이렇게 한다(welcome 의 secret 을 적어 두고 다음에 그걸 내민다).
				await Send(today, "{\"type\":\"hello\",\"secret\":\"" + mine + "\"}");
				await Read(today, "\"identityId\"");

				await Send(today, "{\"type\":\"" + Protocol.BAG_ASK + "\"}");
				string bag = await Read(today, "\"" + Protocol.BAG + "\"");

				Assert.AreEqual(gathered, CountInBag(bag, itemId),
					"껐다 켜니 어제 주운 것이 사라졌다 — 오늘 한 게 내일 없는 세계다: " + bag);
			}
		}

		[Test]
		public async Task 남의_열쇠로_들어오면_내_가방이_안_보인다()
		{
			await StartAsync();
			int gathered;
			int itemId;

			using (ClientWebSocket window = new ClientWebSocket())
			{
				await window.ConnectAsync(address, CancellationToken.None);
				await Read(window, "\"welcome\"");
				await Send(window, "{\"type\":\"hello\",\"secret\":\"" + SECRET + "\"}");
				await Read(window, "\"identityId\"");

				(itemId, gathered) = await GatherOnce(window);
				Assert.Greater(gathered, 0, "줍지도 못했다");
			}

			await StopAsync();
			await StartAsync();

			using (ClientWebSocket stranger = new ClientWebSocket())
			{
				await stranger.ConnectAsync(address, CancellationToken.None);
				await Read(stranger, "\"welcome\"");
				await Send(stranger, "{\"type\":\"hello\",\"secret\":\"기기-남\"}");
				await Read(stranger, "\"identityId\"");

				await Send(stranger, "{\"type\":\"" + Protocol.BAG_ASK + "\"}");
				string bag = await Read(stranger, "\"" + Protocol.BAG + "\"");

				Assert.AreEqual(0, CountInBag(bag, itemId),
					"남의 열쇠로 들어왔는데 내 가방이 딸려 나왔다 — 되살리기가 사람을 안 가린다: " + bag);
			}
		}

		/// <summary>가장 가까운 주울 것까지 걸어가 한 번 줍는다 — 무엇이 얼마나 들어왔는지 돌려준다.</summary>
		private async Task<(int itemId, int amount)> GatherOnce(ClientWebSocket window)
		{
			string snapshot = await Read(window, "\"gatherables\"");
			using JsonDocument world = JsonDocument.Parse(snapshot);
			JsonElement nodes = world.RootElement.GetProperty("gatherables");
			Assert.Greater(nodes.GetArrayLength(), 0, "세계에 주울 것이 하나도 없다");

			JsonElement node = nodes[0];
			int nodeId = node.GetProperty("id").GetInt32();
			double x = node.GetProperty("x").GetDouble();
			double z = node.GetProperty("z").GetDouble();

			// ★ 걸어가는 것은 이 시험의 주제가 아니다 — 주제는 「주운 것이 껐다 켜도 남나」다.
			//   그리고 소켓으로 걷는 길은 이제 <b>시계가 심판한다</b>(MoveAllowance, TASK-WM-222):
			//   걸음을 몰아 보내면 걸어서 갈 수 있는 만큼까지만 간다 — 시험이 목적지에 영영 못 닿는다.
			//   그 계약을 지키는 것은 NoTeleportTests 의 몫이고, 여기서는 세계의 손으로 데려다 놓는다.
			WalkThere(x, z);
			await Task.Delay(400);
			await Send(window, "{\"type\":\"" + Protocol.GATHER + "\",\"nodeId\":" + nodeId + "}");

			string bag = await Read(window, "\"" + Protocol.BAG + "\"");
			using JsonDocument carried = JsonDocument.Parse(bag);
			JsonElement items = carried.RootElement.GetProperty("items");
			Assert.Greater(items.GetArrayLength(), 0, "주웠는데 가방이 비어 있다: " + bag);

			return (items[0].GetProperty("itemId").GetInt32(), items[0].GetProperty("amount").GetInt32());
		}

		/// <summary>환영 인사에 실려 온 열쇠 — 다음에 「나」로 들어올 때 이걸 내민다.</summary>
		private static string KeyIn(string welcome)
		{
			using JsonDocument said = JsonDocument.Parse(welcome);
			string key = said.RootElement.GetProperty("secret").GetString();
			Assert.IsNotEmpty(key, "서버가 열쇠를 안 줬다 — 다시 들어올 길이 없다: " + welcome);
			return key;
		}

		private static int CountInBag(string bag, int itemId)
		{
			using JsonDocument carried = JsonDocument.Parse(bag);
			JsonElement items = carried.RootElement.GetProperty("items");
			for (int i = 0; i < items.GetArrayLength(); i++)
			{
				if (items[i].GetProperty("itemId").GetInt32() == itemId)
					return items[i].GetProperty("amount").GetInt32();
			}

			return 0;
		}

		/// <summary>세계의 손으로 그 자리까지 데려다 놓는다 — 소켓 걸음(심판 있음)이 아니라 발판이다.</summary>
		private void WalkThere(double x, double z)
		{
			WorldDoll[] people = host.World.Snapshot();
			Assert.AreEqual(1, people.Length, "이 시험은 혼자 있는 세계를 본다");
			int dollId = people[0].Id;

			// 세계도 한 걸음을 자른다(MAX_STEP) — 그래서 여러 번 나눠 딛는다.
			for (int i = 0; i < 400; i++)
			{
				Vector3 now = host.World.PositionOf(dollId);
				float gapX = (float)x - now.x;
				float gapZ = (float)z - now.z;
				if (new Vector3(gapX, 0f, gapZ).magnitude <= 0.05f)
					break;

				host.World.TryMove(dollId, new Vector3(gapX, 0f, gapZ));
			}
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

		// ★ 읽은 말은 버리지 않는다 (실측 2026-08-10): 세계는 「접속 직후 전체 그림 한 번」에만
		//   주울 것 목록을 싣고, 그 뒤로는 바뀐 것만 보낸다. 기다리는 말과 안 맞는다고 그림을
		//   흘려보내면 목록이 영영 안 온다 — 시험이 「세계에 주울 것이 없다」로 거짓 실패한다.
		private static readonly System.Collections.Generic.Dictionary<ClientWebSocket, System.Collections.Generic.List<string>> heard =
			new System.Collections.Generic.Dictionary<ClientWebSocket, System.Collections.Generic.List<string>>();

		/// <summary>그 말이 올 때까지 읽는다 — 이미 들은 말부터 본다. 조각난 알림도 이어 붙인다(8KB 를 넘으면 갈라져 온다).</summary>
		private static async Task<string> Read(ClientWebSocket socket, string needle)
		{
			if (heard.TryGetValue(socket, out System.Collections.Generic.List<string> already) == false)
			{
				already = new System.Collections.Generic.List<string>();
				heard[socket] = already;
			}

			for (int i = 0; i < already.Count; i++)
			{
				if (already[i].Contains(needle) == false)
					continue;

				string remembered = already[i];
				already.RemoveAt(i);
				return remembered;
			}

			using CancellationTokenSource timeout = TestTimeout.After(10);
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
					// 기다리다 시간이 다 됐다 — 무엇을 기다렸는지 말해 준다(안 그러면 취소 예외만 남는다).
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

				already.Add(text);
			}

			// 무엇을 기다렸는지 + 그 대신 무엇이 왔는지 같이 남긴다 — 안 그러면 「안 왔다」만 남아 못 좁힌다.
			StringBuilder instead = new StringBuilder();
			for (int i = 0; i < already.Count; i++)
				instead.Append("\n      ").Append(already[i].Length > 300 ? already[i].Substring(0, 300) : already[i]);

			Assert.Fail($"「{needle}」 가 안 왔다. 대신 온 것:{instead}");
			return null;
		}
	}
}
