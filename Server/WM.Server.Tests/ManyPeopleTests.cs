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
	/// <b>여럿이 붙어도 세계가 돈다</b> (TASK-WM-217).
	///
	/// ★ 왜: 지금까지 잰 것은 하나·둘이었다. 그런데 「같이 노는 세계」의 값은 사람이 늘 때 드러난다 —
	///   한 사람이 늦게 읽으면 모두가 멈추던 자리(방송이 차례로 기다리던 것)를 이미 한 번 밟았다.
	///   그때 증상은 「접속은 되는데 아무도 못 움직인다」였고, 사람 눈에는 서버가 죽은 것으로 보인다.
	///
	/// 진짜 소켓 여럿으로 잰다. 재는 것은 속도가 아니라 <b>버티나</b>다 —
	/// 모두가 세계를 받고, 각자 한 짓이 서로에게 보이고, 안 읽는 창 하나가 남을 멈추지 않는다.
	/// </summary>
	public sealed class ManyPeopleTests
	{
		private const int PORT = 5405;
		private const int PEOPLE = 12;

		private static readonly Uri address = new Uri($"ws://127.0.0.1:{PORT}/ws");

		private WebApplication app;
		private WorldHost host;
		private string worldFile;

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-many-" + Path.GetRandomFileName() + ".json");
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
		public async Task 열두_명이_한꺼번에_들어와도_모두_세계를_받는다()
		{
			ClientWebSocket[] windows = new ClientWebSocket[PEOPLE];

			try
			{
				Task<ClientWebSocket>[] joining = new Task<ClientWebSocket>[PEOPLE];
				for (int i = 0; i < PEOPLE; i++)
					joining[i] = JoinAsync("기기-" + i);

				windows = await Task.WhenAll(joining);

				// 모두가 세계 그림을 한 장씩은 받아야 한다 — 못 받은 사람은 빈 화면을 본다.
				for (int i = 0; i < PEOPLE; i++)
				{
					string world = await Read(windows[i], "\"type\":\"world\"");
					Assert.IsNotNull(world, "들어왔는데 세계를 못 받은 사람이 있다");
				}

				Assert.AreEqual(PEOPLE, host.World.Snapshot().Length, "세계가 사람 수를 잘못 세고 있다");
			}
			finally
			{
				Close(windows);
			}
		}

		// ⚠ 「안 읽는 창 하나가 모두를 멈추나」는 여기서 <b>안 잰다</b> (2026-08-10).
		//   그 시험을 써 봤지만, 방송의 밀린-창 건너뛰기(Connection.Sending)를 <b>일부러 빼도
		//   초록이었다</b> — 즉 잡으려던 것을 못 보는 시험이었다. 창 몇 개를 안 읽게 두는 것만으로는
		//   OS 버퍼가 넉넉해 압박이 안 생긴다. 「초록인데 안 보는 시험」은 없는 것보다 나쁘다
		//   (사람이 그걸 보고 「그 자리는 지켜진다」고 믿기 때문에). 진짜로 재려면 버퍼를 채울 만큼
		//   큰 그림 · 많은 창이 필요하고, 그건 이 자리(단위 시험)가 아니라 부하 관문의 몫이다.

		[Test]
		public async Task 여럿이_동시에_지어도_세계가_한_벌로_남는다()
		{
			ClientWebSocket[] windows = new ClientWebSocket[PEOPLE];

			try
			{
				for (int i = 0; i < PEOPLE; i++)
					windows[i] = await JoinAsync("기기-짓는사람-" + i);

				WorldDoll[] dolls = host.World.Snapshot();
				for (int i = 0; i < dolls.Length; i++)
				{
					ServerBuildingCatalog.Catalog.TryCost(WorldSim.CAULDRON_BUILDING_ID, out int itemId, out int amount);
					host.World.TryGather(dolls[i].Id, ServerItemCatalog.Find(itemId), amount);
				}

				// 각자 다른 칸에 짓는다 — 겹치지 않으니 전부 서야 한다.
				for (int i = 0; i < PEOPLE; i++)
				{
					await Send(windows[i], "{\"type\":\"" + Protocol.PLACE + "\",\"x\":" + (100 + i)
						+ ",\"y\":0,\"z\":100,\"buildingId\":" + WorldSim.CAULDRON_BUILDING_ID + "}");
				}

				await Task.Delay(1500);

				Assert.AreEqual(PEOPLE, host.World.Buildings().Length,
					"동시에 지으면 몇 채가 조용히 사라진다 — 그건 손이 미끄러진 게 아니라 세계가 잃은 것이다");
			}
			finally
			{
				Close(windows);
			}
		}

		[Test]
		public async Task 광장에_상한보다_많이_모여도_각자_자기_인형을_본다()
		{
			// 한 칸(16m)에 상한(48명)보다 많이 모이면 세계는 소식 한 벌을 여럿이 같이 쓴다.
			// 그 한 벌에는 가까운 몇 명만 들어가므로 잘린 사람은 <b>자기 인형</b>이 빠진다 —
			// 자기가 안 보이면 화면이 통째로 멎는다. 세계는 그 사람에게 자기 자리를 따로 알려 준다.
			//
			// ⚠ 들어올 때 받는 한 장에는 자기가 늘 들어 있다 — 그것만 보면 이 시험은 <b>거짓 초록</b>이다.
			//   그래서 들어올 때 온 말을 먼저 다 흘려보내고, 그 뒤 <b>방송</b>에서 자기를 보는지 본다.
			int crowd = InterestCrowd.MAX_VISIBLE_DOLLS + 7;
			ClientWebSocket[] windows = new ClientWebSocket[crowd];

			try
			{
				for (int i = 0; i < crowd; i++)
					windows[i] = await JoinAsync("광장-" + i);

				WorldDoll[] dolls = host.World.Snapshot();
				Assert.AreEqual(crowd, dolls.Length, "다 안 들어왔다 — 아래 판정이 의미가 없어진다");

				// 마지막에 들어온 창들이 잘릴 쪽이다(같은 자리면 번호가 큰 쪽부터 잘린다).
				for (int i = crowd - 5; i < crowd; i++)
				{
					int mine = dolls[i].Id;
					string sawMyself = await ReadAfterFirstWorld(windows[i], "\"id\":" + mine + ",");
					StringAssert.Contains("\"id\":" + mine + ",", sawMyself);
				}
			}
			finally
			{
				Close(windows);
			}
		}

		[Test]
		public async Task 판을_건너뛴_창은_다음에_전부를_받는다()
		{
			// 「바뀐 것만」 보내기(TASK-WM-220)의 위험한 자리 — 밀려서 건너뛴 창이 그 판을 영영
			// 못 받으면, 그때 움직이고 멈춘 사람이 그 창에선 엉뚱한 자리에 영원히 서 있게 된다.
			using ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);
			await Read(window, "\"welcome\"");

			// ⚠ 들어올 때 받는 한 장은 원래 「전부」다 — 그걸로 판정하면 거짓 초록이다.
			//   <b>「바뀐 것만」 판이 오기 시작한 뒤</b>에 재야 한다.
			await Read(window, "\"changed\":true");

			// 이 창이 「밀린 것」으로 표시되게 한다 — 그 다음 판은 반드시 전부여야 한다.
			host.MarkMissedForTest();
			string plate = await Read(window, "\"type\":\"world\"");

			StringAssert.DoesNotContain("\"changed\":true", plate,
				"건너뛴 창에 「바뀐 것만」을 주면 그 창의 세계는 영영 어긋난다");
		}

		/// <summary>
		/// 들어올 때 받는 <b>첫 전체 그림</b>을 지나친 뒤에 그 말을 찾는다.
		///
		/// ⚠ 첫 그림에는 자기가 늘 들어 있다 — 그것으로 판정하면 이 시험은 거짓 초록이다.
		/// ⚠ 받는 중에 취소하면 소켓이 <b>끊긴다</b>(Aborted). 그래서 「흘려보내기」는 안 쓴다.
		/// </summary>
		private static async Task<string> ReadAfterFirstWorld(ClientWebSocket socket, string needle)
		{
			using CancellationTokenSource timeout = TestTimeout.After(15);
			byte[] buffer = new byte[32768];
			StringBuilder pending = new StringBuilder();
			bool passedFirstWorld = false;

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

				if (passedFirstWorld == false)
				{
					if (text.Contains("\"type\":\"world\""))
						passedFirstWorld = true;

					continue;
				}

				if (text.Contains(needle))
					return text;
			}

			Assert.Fail($"들어온 뒤로는 「{needle}」 를 한 번도 못 봤다 — 그 창은 자기 인형을 모른다.");
			return null;
		}

		private static void Close(ClientWebSocket[] windows)
		{
			for (int i = 0; i < windows.Length; i++)
			{
				if (windows[i] == null)
					continue;

				try
				{
					windows[i].Dispose();
				}
				catch (ObjectDisposedException)
				{
					// 이미 닫힌 창 — 정리 중이라 문제될 게 없다.
				}
			}
		}

		private static async Task<ClientWebSocket> JoinAsync(string secret)
		{
			ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);
			await Read(window, "\"welcome\"");
			await Send(window, "{\"type\":\"hello\",\"secret\":\"" + secret + "\"}");
			await Read(window, "\"identityId\"");
			return window;
		}

		private static async Task Send(ClientWebSocket socket, string json)
		{
			byte[] payload = Encoding.UTF8.GetBytes(json);
			await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, CancellationToken.None);
		}

		private static async Task<string> Read(ClientWebSocket socket, string needle)
		{
			using CancellationTokenSource timeout = TestTimeout.After(15);
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
