using System;
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
	/// <b>걸어서 옆 세계로 넘어간다</b> — 가방을 들고 (TASK-WM-254).
	///
	/// ★ 왜 이 자리인가: 세계를 나누는 이유는 한 기계의 회선이 벽이기 때문이다(실측 800명 65Mbps).
	///   그런데 나누기만 하고 못 넘어가면 그건 두 개의 다른 게임이다. 이어져야 하나의 세계다.
	///
	/// ★ 여기서 <b>진짜로</b> 두 세계를 띄운다 — 한쪽이 내보내고 다른 쪽이 받는 것까지.
	/// </summary>
	public sealed class WalkingToTheNextZoneTests
	{
		private const int EAST_PORT = 5421;
		private const int WEST_PORT = 5422;
		private const string SECRET = "두 세계만 아는 말";

		/// <summary>국경을 넘는 사람이 들고 가는 짐 — 몇 개가 <b>몇 개로</b> 도착하나를 본다.</summary>
		private const int TRAVELLING_ITEMS = 5;

		private WebApplication east;
		private WebApplication west;
		private WorldHost eastHost;
		private WorldHost westHost;
		private string eastFile;
		private string westFile;

		[SetUp]
		public async Task SetUp()
		{
			eastFile = Path.Combine(Path.GetTempPath(), "wm-east-" + Path.GetRandomFileName() + ".json");
			westFile = Path.Combine(Path.GetTempPath(), "wm-west-" + Path.GetRandomFileName() + ".json");

			// 동쪽 세계: 0..40 을 맡고, 서쪽(-40..0)이 이웃이다.
			Environment.SetEnvironmentVariable("WM_ZONE", "동:0,-40,40,40");
			Environment.SetEnvironmentVariable("WM_ZONE_NEIGHBOURS", $"서:-40,-40,0,40=ws://127.0.0.1:{WEST_PORT}/ws");
			Environment.SetEnvironmentVariable("WM_ZONE_SECRET", SECRET);
			eastHost = new WorldHost(new WorldStore(eastFile));
			east = eastHost.Build(Array.Empty<string>(), $"http://127.0.0.1:{EAST_PORT}");
			await east.StartAsync();

			// 서쪽 세계: -40..0 을 맡는다.
			Environment.SetEnvironmentVariable("WM_ZONE", "서:-40,-40,0,40");
			Environment.SetEnvironmentVariable("WM_ZONE_NEIGHBOURS", $"동:0,-40,40,40=ws://127.0.0.1:{EAST_PORT}/ws");
			westHost = new WorldHost(new WorldStore(westFile));
			west = westHost.Build(Array.Empty<string>(), $"http://127.0.0.1:{WEST_PORT}");
			await west.StartAsync();
		}

		[TearDown]
		public async Task TearDown()
		{
			Environment.SetEnvironmentVariable("WM_ZONE", null);
			Environment.SetEnvironmentVariable("WM_ZONE_NEIGHBOURS", null);
			Environment.SetEnvironmentVariable("WM_ZONE_SECRET", null);

			foreach (WebApplication one in new[] { east, west })
			{
				if (one == null)
					continue;

				await one.StopAsync();
				await one.DisposeAsync();
			}

			east = null;
			west = null;

			foreach (string path in new[] { eastFile, westFile })
			{
				foreach (string one in new[] { path, path + ".bak", path + ".tmp" })
				{
					if (one != null && File.Exists(one))
						File.Delete(one);
				}
			}
		}

		[Test]
		public async Task 서쪽_끝까지_걸으면_옆_세계가_받아_준다()
		{
			using ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(new Uri($"ws://127.0.0.1:{EAST_PORT}/ws"), CancellationToken.None);
			await SendAsync(window, "{\"type\":\"hello\",\"secret\":\"\"}");

			WorldDoll[] people = eastHost.World.Snapshot();
			Assert.AreEqual(1, people.Length);
			int dollId = people[0].Id;

			// 동쪽 세계의 서쪽 끝(x=0)까지 세계의 손으로 데려다 놓고, 거기서 한 걸음 더 간다.
			eastHost.World.TryMove(dollId, new Vector3(-100f, 0f, 0f));
			await SendAsync(window, "{\"type\":\"move\",\"x\":-1.0,\"z\":0}");

			string moveOn = await ReadUntilAsync(window, text => text.Contains("\"type\":\"" + Protocol.MOVE_ON + "\""), 10);

			StringAssert.Contains("\"zone\":\"서\"", moveOn, "어느 세계로 가는지 안 알려 주면 창은 갈 데를 모른다");
			StringAssert.Contains($"127.0.0.1:{WEST_PORT}", moveOn);
			StringAssert.Contains("\"pass\":\"", moveOn, "통행증이 없으면 저쪽은 이 사람을 모른다");

			// 창이 하는 일 그대로: 통행증을 들고 옆 세계에 인사한다.
			string pass = Between(moveOn, "\"pass\":\"", "\"");
			using ClientWebSocket next = new ClientWebSocket();
			await next.ConnectAsync(new Uri($"ws://127.0.0.1:{WEST_PORT}/ws"), CancellationToken.None);
			await SendAsync(next, "{\"type\":\"hello\",\"secret\":\"\",\"pass\":\"" + pass + "\"}");

			await Task.Delay(600);

			WorldDoll[] overThere = westHost.World.Snapshot();
			Assert.AreEqual(1, overThere.Length, "옆 세계가 안 받아 주면 그 사람은 사라진다");
			Assert.LessOrEqual(overThere[0].Position.x, 0f, "받은 자리는 그 세계의 땅 안이어야 한다");

			// 그리고 원래 세계에서는 나갔다 — 둘 다 데리고 있으면 두 세계에 동시에 있게 된다.
			Assert.AreEqual(0, eastHost.World.Snapshot().Length,
				"보낸 세계가 계속 데리고 있으면 그 사람은 두 세계에 동시에 있다(가방이 복사된다)");
		}

		[Test]
		public async Task 다친_몸으로_넘어가면_다친_채로_선다()
		{
			using ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(new Uri($"ws://127.0.0.1:{EAST_PORT}/ws"), CancellationToken.None);
			await SendAsync(window, "{\"type\":\"hello\",\"secret\":\"\"}");
			await Task.Delay(300);

			int dollId = eastHost.World.Snapshot()[0].Id;

			// 세계의 손으로 몸을 깎는다(때리기 판정을 재는 자리가 아니다).
			using ClientWebSocket hitter = new ClientWebSocket();
			await hitter.ConnectAsync(new Uri($"ws://127.0.0.1:{EAST_PORT}/ws"), CancellationToken.None);
			await SendAsync(hitter, "{\"type\":\"hello\",\"secret\":\"\"}");
			await Task.Delay(300);
			eastHost.World.TryStrike(eastHost.World.Snapshot()[1].Id, dollId,
				System.Environment.TickCount64, out int left, out _);
			Assert.Less(left, WitchMendokusai.Net.StrikeRule.FULL_HEALTH, "먼저 다치게 해 놓아야 재는 뜻이 있다");

			eastHost.World.TryMove(dollId, new Vector3(-100f, 0f, 0f));
			await SendAsync(window, "{\"type\":\"move\",\"x\":-1.0,\"z\":0}");

			string moveOn = await ReadUntilAsync(window, text => text.Contains("\"type\":\"" + Protocol.MOVE_ON + "\""), 10);
			string pass = Between(moveOn, "\"pass\":\"", "\"");

			using ClientWebSocket next = new ClientWebSocket();
			await next.ConnectAsync(new Uri($"ws://127.0.0.1:{WEST_PORT}/ws"), CancellationToken.None);
			await SendAsync(next, "{\"type\":\"hello\",\"secret\":\"\",\"pass\":\"" + pass + "\"}");
			await Task.Delay(600);

			WorldDoll[] overThere = westHost.World.Snapshot();
			Assert.AreEqual(1, overThere.Length);
			Assert.AreEqual(left, overThere[0].Health,
				"국경을 넘으니 몸이 가득 찼다 — 맞기 직전에 넘어갔다 오면 회복되는 세계다");

			hitter.Dispose();
		}

		[Test]
		public async Task 지어낸_통행증으로는_안_받아_준다()
		{
			using ClientWebSocket cheat = new ClientWebSocket();
			await cheat.ConnectAsync(new Uri($"ws://127.0.0.1:{WEST_PORT}/ws"), CancellationToken.None);

			// 창이 스스로 만든 통행증 — 도장이 없다.
			await SendAsync(cheat, "{\"type\":\"hello\",\"secret\":\"\",\"pass\":\"9999;10,10;0;1|deadbeef\"}");
			await Task.Delay(500);

			WorldDoll[] here = westHost.World.Snapshot();
			Assert.AreEqual(1, here.Length);
			Assert.AreEqual(0f, here[0].Position.x, 0.01f, "지어낸 통행증이 통하면 아무 자리에나 나타날 수 있다");
			Assert.AreEqual(0f, here[0].Position.z, 0.01f);
		}

		[Test]
		public async Task 국경을_넘어도_남의_신원을_뒤집어쓰지_않는다()
		{
			// ★ 왜: 신원 번호는 <b>세계마다 따로</b> 매겨진다. 동쪽의 1번과 서쪽의 1번은 다른 사람이다.
			//   그런데 통행증에 실린 <b>남의 세계 번호</b>를 그대로 찍으면, 넘어온 사람은
			//   저 세계에 이미 사는 누군가가 된다 — 이름도 저장분도 그 사람 것이 된다.
			using ClientWebSocket resident = new ClientWebSocket();
			await resident.ConnectAsync(new Uri($"ws://127.0.0.1:{WEST_PORT}/ws"), CancellationToken.None);
			await SendAsync(resident, "{\"type\":\"hello\",\"secret\":\"\"}");
			await Task.Delay(400);

			WorldDoll[] before = westHost.World.Snapshot();
			Assert.AreEqual(1, before.Length, "서쪽에 먼저 사는 사람이 있어야 재는 뜻이 있다");
			int residentDoll = before[0].Id;
			int residentIdentity = before[0].IdentityId;
			Assert.AreNotEqual(0, residentIdentity);

			string pass = await CrossFromEastAsync();
			using ClientWebSocket traveller = new ClientWebSocket();
			await traveller.ConnectAsync(new Uri($"ws://127.0.0.1:{WEST_PORT}/ws"), CancellationToken.None);
			await SendAsync(traveller, "{\"type\":\"hello\",\"secret\":\"\",\"pass\":\"" + pass + "\"}");
			await Task.Delay(700);

			WorldDoll came = null;
			foreach (WorldDoll one in westHost.World.Snapshot())
			{
				if (one.Id != residentDoll)
					came = one;
			}

			Assert.IsNotNull(came, "넘어온 사람이 없다");
			Assert.AreNotEqual(residentIdentity, came.IdentityId,
				"넘어온 사람이 <b>이미 서쪽에 사는 사람</b>이 됐다 — 이름도 저장분도 그 사람 것이 된다");
		}

		[Test]
		public async Task 국경을_넘어도_나는_계속_나다()
		{
			// ★ 왜: 세계는 열쇠의 <b>지문</b>만 갖는다. 그 지문은 세계가 달라도 같은 값이다.
			//   그러니 넘어간 세계도 나를 「그 사람」으로 이어 알아봐야 한다 —
			//   못 알아보면 국경을 넘을 때마다 <b>남이 되는</b> 세계다(모은 게 매번 사라진다).
			using ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(new Uri($"ws://127.0.0.1:{EAST_PORT}/ws"), CancellationToken.None);
			await SendAsync(window, "{\"type\":\"hello\",\"secret\":\"\"}");

			// ⚠ 인사 <b>전</b>에도 환영이 한 번 온다(번호만 알려 주는 것) — 열쇠가 든 쪽을 기다린다.
			string welcome = await ReadUntilAsync(window, IsAnsweredHello, 10);
			string myKey = Between(welcome, "\"secret\":\"", "\"");
			Assert.IsNotEmpty(myKey, "동쪽이 열쇠를 안 줬으면 이 시험은 뜻이 없다 — " + welcome);

			int dollId = eastHost.World.Snapshot()[0].Id;
			eastHost.World.TryMove(dollId, new Vector3(-100f, 0f, 0f));
			await SendAsync(window, "{\"type\":\"move\",\"x\":-1.0,\"z\":0}");
			string moveOn = await ReadUntilAsync(window, text => text.Contains("\"type\":\"" + Protocol.MOVE_ON + "\""), 10);
			string pass = Between(moveOn, "\"pass\":\"", "\"");

			using ClientWebSocket next = new ClientWebSocket();
			await next.ConnectAsync(new Uri($"ws://127.0.0.1:{WEST_PORT}/ws"), CancellationToken.None);
			await SendAsync(next, "{\"type\":\"hello\",\"secret\":\"" + myKey + "\",\"pass\":\"" + pass + "\"}");

			string overThere = await ReadUntilAsync(next, IsAnsweredHello, 10);
			Assert.AreEqual(string.Empty, Between(overThere, "\"secret\":\"", "\""),
				"서쪽이 <b>새 열쇠</b>를 줬다 — 그러면 창의 열쇠가 바뀌어 동쪽으로 돌아가면 또 남이 된다");
			await Task.Delay(400);
			int hereIdentity = westHost.World.Snapshot()[0].IdentityId;

			// 그리고 그 열쇠로 <b>통행증 없이</b> 다시 와도 같은 사람이어야 한다.
			next.Dispose();
			await Task.Delay(300);
			using ClientWebSocket again = new ClientWebSocket();
			await again.ConnectAsync(new Uri($"ws://127.0.0.1:{WEST_PORT}/ws"), CancellationToken.None);
			await SendAsync(again, "{\"type\":\"hello\",\"secret\":\"" + myKey + "\"}");
			await Task.Delay(500);

			WorldDoll[] now = westHost.World.Snapshot();
			Assert.AreEqual(1, now.Length);
			Assert.AreEqual(hereIdentity, now[0].IdentityId,
				"같은 열쇠로 왔는데 다른 사람이 됐다 — 국경을 넘으면 모은 게 사라지는 세계다");
		}

		[Test]
		public async Task 통행증은_한_번만_쓴다()
		{
			// ★ 왜: 통행증은 <b>글자</b>다. 창은 그걸 복사할 수 있고 남에게 줄 수도 있다.
			//   한 장으로 두 번 들어올 수 있으면 그건 곧 <b>가방 복사</b>다(전형적인 복사 버그).
			string pass = await CrossFromEastAsync();

			using ClientWebSocket first = new ClientWebSocket();
			await first.ConnectAsync(new Uri($"ws://127.0.0.1:{WEST_PORT}/ws"), CancellationToken.None);
			await SendAsync(first, "{\"type\":\"hello\",\"secret\":\"\",\"pass\":\"" + pass + "\"}");
			await Task.Delay(600);

			using ClientWebSocket copy = new ClientWebSocket();
			await copy.ConnectAsync(new Uri($"ws://127.0.0.1:{WEST_PORT}/ws"), CancellationToken.None);
			await SendAsync(copy, "{\"type\":\"hello\",\"secret\":\"\",\"pass\":\"" + pass + "\"}");
			await Task.Delay(600);

			int carried = 0;
			foreach (WorldDoll one in westHost.World.Snapshot())
			{
				foreach (BagSaveEntry held in one.SaveBag())
					carried += held.amount;
			}

			Assert.AreEqual(TRAVELLING_ITEMS, carried,
				"같은 통행증 한 장으로 가방이 두 벌 들어왔다 — 복사기가 국경에 서 있는 셈이다");
		}

		/// <summary>동쪽에서 물건을 들고 서쪽 국경을 넘어, 그 통행증을 돌려준다.</summary>
		private async Task<string> CrossFromEastAsync()
		{
			ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(new Uri($"ws://127.0.0.1:{EAST_PORT}/ws"), CancellationToken.None);
			await SendAsync(window, "{\"type\":\"hello\",\"secret\":\"\"}");
			await Task.Delay(400);

			int dollId = eastHost.World.Snapshot()[0].Id;
			eastHost.World.TryGather(dollId, ServerItemCatalog.Find(WorldSeeds.WOOD), TRAVELLING_ITEMS);

			eastHost.World.TryMove(dollId, new Vector3(-100f, 0f, 0f));
			await SendAsync(window, "{\"type\":\"move\",\"x\":-1.0,\"z\":0}");
			string moveOn = await ReadUntilAsync(window, text => text.Contains("\"type\":\"" + Protocol.MOVE_ON + "\""), 10);
			window.Dispose();

			return Between(moveOn, "\"pass\":\"", "\"");
		}

		/// <summary>인사에 대한 답 환영인가 — 붙자마자 오는 첫 환영에는 신원도 열쇠도 없다.</summary>
		private static bool IsAnsweredHello(string text)
		{
			return text.Contains("\"type\":\"" + Protocol.WELCOME + "\"")
				&& text.Contains("\"identityId\":0") == false;
		}

		private static string Between(string text, string from, string to)
		{
			int at = text.IndexOf(from, StringComparison.Ordinal) + from.Length;
			int end = text.IndexOf(to, at, StringComparison.Ordinal);
			return text.Substring(at, end - at);
		}

		private static async Task SendAsync(ClientWebSocket window, string json)
		{
			byte[] payload = Encoding.UTF8.GetBytes(json);
			await window.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, CancellationToken.None);
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

			Assert.Fail("기다린 말이 안 왔다");
			return null;
		}
	}
}
