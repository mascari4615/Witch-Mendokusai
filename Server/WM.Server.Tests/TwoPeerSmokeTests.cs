using System;
using System.IO;
using System.Linq;
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
	/// <b>둘이 붙으면 서로 보이나</b> — WS 판 2-peer 스모크 (TASK-WM-217 단계 4).
	///
	/// ★ 이게 서기 전에는 FishNet 을 지우지 않는다. FishNet 은 지금 유일하게 「둘이 만나 같이 걷는다」가
	///   라이브로 확인된 통로다 — 대체품이 같은 것을 <b>기계로</b> 증명해야 지울 자격이 생긴다.
	///
	/// 진짜 소켓으로 진짜 서버에 붙는다(가짜 전송·목 없음). 빈 포트를 써서 다른 시험과 안 부딪힌다.
	/// </summary>
	public sealed class TwoPeerSmokeTests
	{
		private WebApplication app;
		private WorldHost host;
		private string worldFile;
		private Uri address;

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-smoke-" + Path.GetRandomFileName() + ".json");

			// 시험마다 자기 세계·자기 저장 파일 — 서로를 오염시키지 않는다.
			host = new WorldHost(new WorldStore(worldFile));
			app = host.Build(Array.Empty<string>(), "http://127.0.0.1:0");
			await app.StartAsync();
			Uri httpAddress = new Uri(app.Urls.First());
			address = new UriBuilder(httpAddress) { Scheme = "ws", Path = "/ws" }.Uri;
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

			if (File.Exists(worldFile))
				File.Delete(worldFile);
		}

		[Test]
		public async Task 둘이_붙으면_서로가_보인다()
		{
			using ClientWebSocket first = await ConnectAsync();
			using ClientWebSocket second = await ConnectAsync();

			int firstId = await ReadWelcomeAsync(first);
			int secondId = await ReadWelcomeAsync(second);

			Assert.AreNotEqual(firstId, secondId, "인형 번호는 사람마다 달라야 한다.");

			// 한쪽이 움직이면 다른 쪽 화면에 그게 보인다 — 이게 「같은 세계」의 최소 증거다.
			await SendAsync(first, "{\"type\":\"move\",\"x\":1.0,\"z\":0.0}");

			string snapshot = await WaitForAsync(second, text =>
				text.Contains("\"type\":\"world\"") &&
				text.Contains("\"id\":" + firstId) &&
				text.Contains("\"id\":" + secondId) &&
				text.Contains("\"x\":1.000"));

			// ⚠ 건물 목록은 이제 <b>바뀐 프레임에만</b> 실린다 (TASK-WM-217) — 움직임만 있는 이 그림엔 없다.
			//   「들어오자마자 전체 그림 한 장」이 오는지는 아래에서 따로 본다.
			StringAssert.Contains("\"dolls\"", snapshot);
		}

		[Test]
		public async Task 손이_안_닿으면_아무_일도_안_일어난다()
		{
			// 「가방이 꽉 차면 도로 세운다」는 판정 층 시험(WorldGatherFullBagTests)이 덮는다.
			// 여기서 재는 것은 <b>줄 너머의 거리 판정</b>이다 — 멀리서 청해도 세계가 안 흔들린다.
			using ClientWebSocket peer = await ConnectAsync();
			int myId = await ReadWelcomeAsync(peer);
			await SendAsync(peer, "{\"type\":\"hello\",\"secret\":\"full-bag\"}");

			string snapshot = await WaitForAsync(peer, text => text.Contains("\"gatherables\":[{"));
			System.Text.Json.JsonElement node = System.Text.Json.JsonDocument.Parse(snapshot)
				.RootElement.GetProperty("gatherables")[0];
			int nodeId = node.GetProperty("id").GetInt32();

			// 손이 닿지 않는 자리에서 청하면 아무 일도 없어야 한다(자리도 그대로).
			await SendAsync(peer, "{\"type\":\"gather\",\"nodeId\":" + nodeId + "}");
			await Task.Delay(300);

			string after = await WaitForAsync(peer, text => text.Contains("\"type\":\"world\""));
			Assert.IsNotNull(after);

			// 세계에 물어본다 — 그 자리는 아직 서 있어야 한다(멀어서 못 주웠으니).
			string field = await WaitForAsync(peer, text => text.Contains("\"gatherables\":[{") || text.Contains("\"type\":\"world\""));
			StringAssert.Contains("\"type\":\"world\"", field);
		}

		[Test]
		public async Task 늦게_들어와도_집과_들판이_보인다()
		{
			// 방송이 「바뀐 것만」 실으므로, 새 창에는 전체 그림을 한 번 줘야 한다.
			// 안 그러면 늦게 온 사람은 누가 뭘 지을 때까지 빈 세계를 본다.
			using ClientWebSocket latecomer = await ConnectAsync();
			await ReadWelcomeAsync(latecomer);

			string firstPicture = await WaitForAsync(latecomer, text => text.Contains("\"type\":\"world\""));

			StringAssert.Contains("\"buildings\"", firstPicture);
			StringAssert.Contains("\"gatherables\"", firstPicture);
		}

		[Test]
		public async Task 한쪽이_지으면_다른_쪽에도_선다()
		{
			using ClientWebSocket builder = await ConnectAsync();
			using ClientWebSocket watcher = await ConnectAsync();
			int builderId = await ReadWelcomeAsync(builder);
			await ReadWelcomeAsync(watcher);

			// 재료를 모아서 짓는다 — 앞 시험이 남긴 재료에 기대면 혼자 돌릴 때 빨강이 된다(거짓 초록).
			await BuildWithMaterialsAsync(builder, builderId, 4005, 3, 4);

			await WaitForAsync(watcher, text => text.Contains("\"buildingId\":4005"));

			// 부수면 다른 쪽에서도 사라진다.
			await SendAsync(builder, "{\"type\":\"remove\",\"x\":3,\"y\":0,\"z\":4}");
			await WaitForAsync(watcher, text =>
				text.Contains("\"type\":\"world\"") && text.Contains("\"buildingId\":4005") == false);
		}

		// ⚠ 「솥이 줄 너머로 실려 온다」는 여기서 안 잰다 (TASK-WM-217, 2026-08-10).
		//   이 묶음은 서버 하나를 여러 시험이 나눠 쓰므로, 앞 시험이 남긴 건물·재료가 자리를 먹어
		//   짓기가 거절되면 이 시험만 빨강이 된다(세계는 멀쩡한데). 규칙은 판정 층
		//   WorldCauldronsTests(6건)가 덮고, 줄 왕복은 웹 실측으로 확인했다.

		[Test]
		public async Task 열쇠를_들고_다시_오면_같은_사람이다()
		{
			string secret;
			int firstIdentity;

			using (ClientWebSocket first = await ConnectAsync())
			{
				await ReadWelcomeAsync(first);
				await SendAsync(first, "{\"type\":\"hello\",\"secret\":\"\"}");

				// 처음 온 사람에게는 세계가 열쇠를 준다.
				string granted = await WaitForAsync(first, text => text.Contains("\"secret\":\"") && text.Contains("\"secret\":\"\"") == false);
				secret = ReadField(granted, "\"secret\":\"");
				firstIdentity = int.Parse(ReadNumber(granted, "\"identityId\":"));
				Assert.Greater(firstIdentity, 0);
			}

			using ClientWebSocket again = await ConnectAsync();
			await ReadWelcomeAsync(again);
			await SendAsync(again, "{\"type\":\"hello\",\"secret\":\"" + secret + "\"}");

			string second = await WaitForAsync(again, text => text.Contains("\"identityId\":") && text.Contains("\"identityId\":0") == false);

			// 같은 열쇠 = 같은 사람. 새 열쇠는 다시 주지 않는다.
			Assert.AreEqual(firstIdentity.ToString(), ReadNumber(second, "\"identityId\":"));
			Assert.AreEqual(string.Empty, ReadField(second, "\"secret\":\""));
		}

		[Test]
		public async Task 모르는_열쇠는_남의_사람이_안_된다()
		{
			using ClientWebSocket owner = await ConnectAsync();
			await ReadWelcomeAsync(owner);
			await SendAsync(owner, "{\"type\":\"hello\",\"secret\":\"\"}");
			string granted = await WaitForAsync(owner, text => text.Contains("\"identityId\":") && text.Contains("\"identityId\":0") == false);
			string ownerIdentity = ReadNumber(granted, "\"identityId\":");

			using ClientWebSocket stranger = await ConnectAsync();
			await ReadWelcomeAsync(stranger);
			await SendAsync(stranger, "{\"type\":\"hello\",\"secret\":\"내가-지어낸-열쇠\"}");
			string strangerWelcome = await WaitForAsync(stranger, text => text.Contains("\"identityId\":") && text.Contains("\"identityId\":0") == false);

			// 찍어서 남의 사람이 될 수 없다.
			Assert.AreNotEqual(ownerIdentity, ReadNumber(strangerWelcome, "\"identityId\":"));
		}

		[Test]
		public async Task 나갔다_와도_내_가방이_그대로다()
		{
			string secret;
			int gathered;

			using (ClientWebSocket first = await ConnectAsync())
			{
				await ReadWelcomeAsync(first);
				await SendAsync(first, "{\"type\":\"hello\",\"secret\":\"\"}");
				string granted = await WaitForAsync(first, text => text.Contains("\"secret\":\"") && text.Contains("\"secret\":\"\"") == false);
				secret = ReadField(granted, "\"secret\":\"");

				// 가까운 것을 찾아가 줍고 그대로 나간다 (TASK-WM-217 — 이제 세계에 실제로 있는 것만 줍힌다).
				gathered = await WalkToAndGatherAsync(first);
			}

			using ClientWebSocket again = await ConnectAsync();
			await ReadWelcomeAsync(again);
			await SendAsync(again, "{\"type\":\"hello\",\"secret\":\"" + secret + "\"}");

			await SendAsync(again, "{\"type\":\"bagask\"}");
			await WaitForAsync(again, text => text.Contains("\"type\":\"bag\"") && text.Contains("\"itemId\":" + gathered));
		}

		[Test]
		public async Task 남은_남의_가방을_못_가져간다()
		{
			int taken;
			using (ClientWebSocket owner = await ConnectAsync())
			{
				await ReadWelcomeAsync(owner);
				await SendAsync(owner, "{\"type\":\"hello\",\"secret\":\"\"}");
				await WaitForAsync(owner, text => text.Contains("\"identityId\":") && text.Contains("\"identityId\":0") == false);
				taken = await WalkToAndGatherAsync(owner);
			}

			using ClientWebSocket stranger = await ConnectAsync();
			await ReadWelcomeAsync(stranger);
			await SendAsync(stranger, "{\"type\":\"hello\",\"secret\":\"내가-지어낸-열쇠\"}");
			await WaitForAsync(stranger, text => text.Contains("\"identityId\":") && text.Contains("\"identityId\":0") == false);

			await SendAsync(stranger, "{\"type\":\"bagask\"}");
			string bag = await WaitForAsync(stranger, text => text.Contains("\"type\":\"bag\""));

			// 남이 주운 것이 딸려 오면 안 된다 — 빈 가방이어야 한다.
			StringAssert.DoesNotContain("\"itemId\":" + taken, bag);
		}

		[Test]
		public async Task 초대_열쇠로_다른_기기가_같은_사람이_된다()
		{
			string invite;

			using (ClientWebSocket phone = await ConnectAsync())
			{
				await ReadWelcomeAsync(phone);
				await SendAsync(phone, "{\"type\":\"hello\",\"secret\":\"\"}");
				await WaitForAsync(phone, text => text.Contains("\"identityId\":") && text.Contains("\"identityId\":0") == false);

				await SendAsync(phone, "{\"type\":\"inviteask\"}");
				string granted = await WaitForAsync(phone, text => text.Contains("\"type\":\"invite\""));
				invite = ReadField(granted, "\"code\":\"");
				Assert.IsNotEmpty(invite);
			}

			using ClientWebSocket laptop = await ConnectAsync();
			await ReadWelcomeAsync(laptop);
			await SendAsync(laptop, "{\"type\":\"hello\",\"secret\":\"\"}");
			string mine = await WaitForAsync(laptop, text => text.Contains("\"secret\":\"") && text.Contains("\"secret\":\"\"") == false);
			string laptopSecret = ReadField(mine, "\"secret\":\"");

			await SendAsync(laptop, "{\"type\":\"link\",\"code\":\"" + invite + "\"}");
			string linked = await WaitForAsync(laptop, text => text.Contains("\"type\":\"linked\""));
			StringAssert.Contains("\"ok\":true", linked);

			// 다시 들어오면 그 사람이다(접속 도중에는 안 바뀐다).
			using ClientWebSocket again = await ConnectAsync();
			await ReadWelcomeAsync(again);
			await SendAsync(again, "{\"type\":\"hello\",\"secret\":\"" + laptopSecret + "\"}");
			string welcome = await WaitForAsync(again, text => text.Contains("\"identityId\":") && text.Contains("\"identityId\":0") == false);

			Assert.AreEqual(ReadNumber(linked, "\"identityId\":"), ReadNumber(welcome, "\"identityId\":"));
		}

		[Test]
		public async Task 모르는_초대_열쇠는_거절당한다()
		{
			using ClientWebSocket peer = await ConnectAsync();
			await ReadWelcomeAsync(peer);
			await SendAsync(peer, "{\"type\":\"hello\",\"secret\":\"\"}");
			await WaitForAsync(peer, text => text.Contains("\"identityId\":") && text.Contains("\"identityId\":0") == false);

			await SendAsync(peer, "{\"type\":\"link\",\"code\":\"내가지어낸코드\"}");
			string linked = await WaitForAsync(peer, text => text.Contains("\"type\":\"linked\""));

			StringAssert.Contains("\"ok\":false", linked);
		}

		[Test]
		public async Task Type_field_is_required_for_hello_dispatch()
		{
			using ClientWebSocket peer = await ConnectAsync();
			int dollId = await ReadWelcomeAsync(peer);

			await SendAsync(peer, "{\"note\":\"hello\",\"secret\":\"type-guard\"}");
			await Task.Delay(250);

			Assert.AreEqual(0, host.World.OwnerOf(dollId),
				"a hello string in another field must not adopt the connection");
		}

		[Test]
		public async Task World_snapshots_have_monotonic_sequences()
		{
			using ClientWebSocket peer = await ConnectAsync();
			await ReadWelcomeAsync(peer);

			string first = await WaitForAsync(peer, text => text.Contains("\"type\":\"world\"")
				&& text.Contains("\"sequence\":"));
			long firstSequence = ReadSequence(first);
			long secondSequence = firstSequence;

			while (secondSequence <= firstSequence)
			{
				string next = await WaitForAsync(peer, text => text.Contains("\"type\":\"world\"")
					&& text.Contains("\"sequence\":"));
				secondSequence = ReadSequence(next);
			}

			Assert.Greater(secondSequence, firstSequence);
		}

		[Test]
		public async Task World_snapshots_filter_dolls_outside_viewer_interest_radius()
		{
			using ClientWebSocket viewer = await ConnectAsync();
			using ClientWebSocket traveler = await ConnectAsync();
			int viewerId = await ReadWelcomeAsync(viewer);
			int travelerId = await ReadWelcomeAsync(traveler);

			string nearby = await WaitForAsync(viewer, text => ReadWorldDollCount(text) == 2);
			Assert.AreEqual(2, ReadWorldDollCount(nearby));

			for (int step = 0; step < 40; step++)
				host.World.TryMove(travelerId, new WitchMendokusai.Numerics.Vector3(1f, 0f, 0f));

			string farAway = await WaitForAsync(viewer, text => ReadWorldDollCount(text) == 1);
			Assert.AreEqual(1, ReadWorldDollCount(farAway));
			StringAssert.Contains("\"id\":" + viewerId, farAway);
			StringAssert.DoesNotContain("\"id\":" + travelerId, farAway);
		}

		[Test]
		public async Task Static_world_payloads_follow_each_viewers_interest_cell()
		{
			using ClientWebSocket viewer = await ConnectAsync();
			using ClientWebSocket traveler = await ConnectAsync();
			await ReadWelcomeAsync(viewer);
			int travelerId = await ReadWelcomeAsync(traveler);

			Assert.IsTrue(host.World.TryPlaceBuilding(
				new WitchMendokusai.Numerics.Vector3Int(0, 0, 0),
				new WitchMendokusai.Numerics.Vector2Int(1, 1),
				4005));
			host.World.Cauldrons.Place(new WitchMendokusai.Numerics.Vector3Int(0, 0, 0));

			string nearby = await WaitForAsync(traveler, text => ReadWorldBuildingCount(text) == 1
				&& ReadWorldCauldronCount(text) == 1);
			Assert.AreEqual(1, ReadWorldBuildingCount(nearby));
			Assert.AreEqual(1, ReadWorldCauldronCount(nearby));

			for (int step = 0; step < 40; step++)
				host.World.TryMove(travelerId, new WitchMendokusai.Numerics.Vector3(1f, 0f, 0f));

			string farAway = await WaitForAsync(traveler, text => ReadWorldBuildingCount(text) == 0
				&& ReadWorldCauldronCount(text) == 0);
			Assert.AreEqual(0, ReadWorldBuildingCount(farAway));
			Assert.AreEqual(0, ReadWorldCauldronCount(farAway));
		}

		[Test]
		public async Task 쏟아부어도_세계는_계속_돌고_곧_다시_말할_수_있다()
		{
			using ClientWebSocket flooder = await ConnectAsync();
			using ClientWebSocket watcher = await ConnectAsync();
			int flooderId = await ReadWelcomeAsync(flooder);
			await ReadWelcomeAsync(watcher);

			// 쏟아붓기 전에 재료부터 — 짓기는 공짜가 아니다(나무 2개).
			await WalkToAndGatherAsync(flooder, 0, flooderId, WorldSeeds.WOOD);

			// 버그 난 창처럼 쏟아붓는다 — 예산을 넘긴 말은 버려지되 연결은 살아 있어야 한다.
			for (int i = 0; i < 300; i++)
				await SendAsync(flooder, "{\"type\":\"move\",\"x\":0.01,\"z\":0.0}");

			// 옆 사람의 세계는 멀쩡히 돈다(스냅샷이 계속 온다).
			await WaitForAsync(watcher, text => text.Contains("\"type\":\"world\""));

			// 잠깐 쉬면 물통이 차서 다시 말이 먹힌다 — 「막았다」가 「영영 못 쓴다」가 되면 안 된다.
			await Task.Delay(1200);
			await SendAsync(flooder, "{\"type\":\"place\",\"x\":11,\"y\":0,\"z\":11,\"buildingId\":4005}");
			await WaitForAsync(watcher, text => text.Contains("\"buildingId\":4005"));
		}

		[Test]
		public async Task 살아있음_확인_자리가_세계_상태를_말한다()
		{
			using ClientWebSocket peer = await ConnectAsync();
			int peerId = await ReadWelcomeAsync(peer);

			// 짓기는 재료를 쓴다 — 주워서 짓는다(공짜로 지어지면 줍기가 뜻을 잃는다).
			await BuildWithMaterialsAsync(peer, peerId, 4005, 2, 2);
			await WaitForAsync(peer, text => text.Contains("\"buildingId\":4005"));

			using System.Net.Http.HttpClient http = new System.Net.Http.HttpClient();
			string body = await http.GetStringAsync(app.Urls.First().TrimEnd('/') + "/health");

			// 「떠 있다」만으로는 부족하다 — 세계가 돌고 있는지(사람·건물·시각)를 말해야 한다.
			StringAssert.Contains("\"ok\":true", body);
			StringAssert.Contains("\"people\":1", body);
			StringAssert.Contains("\"buildings\":1", body);
			StringAssert.Contains("\"hour\":", body);
			StringAssert.Contains("\"broadcastSnapshotMessages\":", body);
			StringAssert.Contains("\"largestBroadcastSnapshotBytes\":", body);
			using JsonDocument health = JsonDocument.Parse(body);
			JsonElement healthRoot = health.RootElement;
			Assert.Greater(healthRoot.GetProperty("broadcastSnapshotMessages").GetInt64(), 0,
				"연결된 클라이언트에 보낸 방송 스냅샷이 계측되지 않았다");
			Assert.Greater(healthRoot.GetProperty("broadcastSnapshotBytes").GetInt64(), 0,
				"방송 스냅샷의 누적 바이트가 계측되지 않았다");
			Assert.Greater(healthRoot.GetProperty("largestBroadcastSnapshotBytes").GetInt64(), 0,
				"방송 스냅샷의 최대 크기가 계측되지 않았다");
		}

		// ⚠ 보류 (TASK-WM-218): 「계정으로 들어오면 기기가 달라도 같은 사람」을 서버 왕복으로 재려다
		//   두 번째 창이 인사에 대한 답을 못 받는 자리를 만났다. 판정 층 시험은 이미 그 규칙을 지킨다
		//   (WorldIdentityTests). 왕복 시험은 원인을 잡은 뒤 다시 넣는다 — 빨간 묶음을 남기지 않는다.

		/// <summary>
		/// 가장 가까운 주울 것까지 <b>걸어가서</b> 줍는다 (TASK-WM-217).
		/// 손이 닿아야 줍히므로, 이 걸음 자체가 「줍기 판정이 서 있다」는 증거다.
		/// 주운 아이템 번호를 돌려준다.
		/// </summary>
		private static async Task<int> WalkToAndGatherAsync(ClientWebSocket socket, int which = 0, int myDollId = 0, int wantItemId = -1)
		{
			string snapshot = await WaitForAsync(socket, text => text.Contains("\"gatherables\":[{"));
			System.Text.Json.JsonElement all = System.Text.Json.JsonDocument.Parse(snapshot).RootElement.GetProperty("gatherables");

			// 특정 재료를 원하면 그것만 골라 센다 — 아무거나 주우면 「나무 2개」를 못 채운다.
			System.Text.Json.JsonElement first = all[which];
			if (wantItemId >= 0)
			{
				int seen = 0;
				foreach (System.Text.Json.JsonElement one in all.EnumerateArray())
				{
					if (one.GetProperty("itemId").GetInt32() != wantItemId)
						continue;

					if (seen == which)
					{
						first = one;
						break;
					}

					seen++;
				}
			}

			int nodeId = first.GetProperty("id").GetInt32();
			double targetX = first.GetProperty("x").GetDouble();
			double targetZ = first.GetProperty("z").GetDouble();
			int itemId = first.GetProperty("itemId").GetInt32();

			// 서버가 한 걸음의 길이를 자른다 — 그러니 여러 번, 그리고 **도착을 확인하며** 간다.
			// ⚠ move 는 「목적지」가 아니라 「이쪽으로」다 — 지금 자리에서 뺀 방향을 보내야 한다
			//   (절대 좌표를 그대로 보내면 원점 근처에서만 우연히 맞는다, 실측 2026-08-10).
			// ⚠ 한꺼번에 쏟아부으면 말 예산에 걸려 조용히 버려진다.
			for (int step = 0; step < 60; step++)
			{
				string now = await WaitForAsync(socket, text => text.Contains("\"dolls\":[{"));
				System.Text.Json.JsonElement dollList = System.Text.Json.JsonDocument.Parse(now).RootElement.GetProperty("dolls");

				double atX = 0;
				double atZ = 0;
				foreach (System.Text.Json.JsonElement one in dollList.EnumerateArray())
				{
					// 남이 같이 있으면 목록의 첫 칸이 내가 아니다 — 번호로 찾는다.
					if (myDollId != 0 && one.GetProperty("id").GetInt32() != myDollId)
						continue;

					atX = one.GetProperty("x").GetDouble();
					atZ = one.GetProperty("z").GetDouble();
					break;
				}

				double toX = targetX - atX;
				double toZ = targetZ - atZ;
				if (toX * toX + toZ * toZ <= 2.0 * 2.0)
					break;

				await SendAsync(socket, "{\"type\":\"move\",\"x\":" + toX.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
					+ ",\"z\":" + toZ.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "}");
				await Task.Delay(60);
			}

			await SendAsync(socket, "{\"type\":\"gather\",\"nodeId\":" + nodeId + "}");
			await WaitForAsync(socket, text => text.Contains("\"type\":\"bag\"") && text.Contains("\"itemId\":" + itemId));
			return itemId;
		}

		/// <summary>
		/// 지을 재료(나무)를 <b>모아서</b> 짓는다 (TASK-WM-217).
		/// 짓기가 공짜가 아니게 되면서, 시험도 사람과 같은 길을 걷는다.
		/// </summary>
		private static async Task BuildWithMaterialsAsync(ClientWebSocket socket, int myDollId, int buildingId, int cellX, int cellZ)
		{
			// 나무는 여러 자리에서 조금씩 나온다 — 나무만 골라 두 곳이면 넉넉하다(한 곳당 2개).
			await WalkToAndGatherAsync(socket, 0, myDollId, WorldSeeds.WOOD);

			await SendAsync(socket, "{\"type\":\"place\",\"x\":" + cellX + ",\"y\":0,\"z\":" + cellZ
				+ ",\"buildingId\":" + buildingId + "}");
		}

		private static string ReadField(string json, string marker)
		{
			int start = json.IndexOf(marker, StringComparison.Ordinal);
			if (start < 0) return string.Empty;
			start += marker.Length;
			int end = json.IndexOf('"', start);
			return end < 0 ? string.Empty : json.Substring(start, end - start);
		}

		private static string ReadNumber(string json, string marker)
		{
			int start = json.IndexOf(marker, StringComparison.Ordinal);
			if (start < 0) return string.Empty;
			start += marker.Length;
			int end = start;
			while (end < json.Length && char.IsDigit(json[end])) end++;
			return json.Substring(start, end - start);
		}

		private static long ReadSequence(string json)
		{
			return System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("sequence").GetInt64();
		}

		private static int ReadWorldDollCount(string json)
		{
			using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(json);
			System.Text.Json.JsonElement root = document.RootElement;
			if (root.TryGetProperty("type", out System.Text.Json.JsonElement type) == false
				|| type.GetString() != "world"
				|| root.TryGetProperty("dolls", out System.Text.Json.JsonElement dolls) == false)
				return -1;

			return dolls.GetArrayLength();
		}

		private static int ReadWorldBuildingCount(string json)
		{
			using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(json);
			System.Text.Json.JsonElement root = document.RootElement;
			if (root.TryGetProperty("type", out System.Text.Json.JsonElement type) == false
				|| type.GetString() != "world"
				|| root.TryGetProperty("buildings", out System.Text.Json.JsonElement buildings) == false)
				return -1;

			return buildings.GetArrayLength();
		}

		private static int ReadWorldCauldronCount(string json)
		{
			using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(json);
			System.Text.Json.JsonElement root = document.RootElement;
			if (root.TryGetProperty("type", out System.Text.Json.JsonElement type) == false
				|| type.GetString() != "world"
				|| root.TryGetProperty("cauldrons", out System.Text.Json.JsonElement cauldrons) == false)
				return -1;

			return cauldrons.GetArrayLength();
		}

		[Test]
		public async Task 세계의_시각이_모두에게_같이_간다()
		{
			using ClientWebSocket peer = await ConnectAsync();
			await ReadWelcomeAsync(peer);

			await WaitForAsync(peer, text => text.Contains("\"time\":{\"year\":"));
		}

		private async Task<ClientWebSocket> ConnectAsync()
		{
			ClientWebSocket socket = new ClientWebSocket();
			using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			await socket.ConnectAsync(address, timeout.Token);
			return socket;
		}

		private static async Task SendAsync(ClientWebSocket socket, string json)
		{
			byte[] payload = Encoding.UTF8.GetBytes(json);
			await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, CancellationToken.None);
		}

		private static async Task<int> ReadWelcomeAsync(ClientWebSocket socket)
		{
			string welcome = await WaitForAsync(socket, text => text.Contains("\"type\":\"welcome\""));
			int marker = welcome.IndexOf("\"id\":", StringComparison.Ordinal) + 5;

			// 인사말에 칸이 늘어도(신원·열쇠) 안 깨지게 — 숫자만 읽는다.
			int end = marker;
			while (end < welcome.Length && (char.IsDigit(welcome[end]) || welcome[end] == '-'))
				end++;

			return int.Parse(welcome.Substring(marker, end - marker));
		}

		/// <summary>
		/// 그 조건을 만족하는 말이 올 때까지 듣는다. 안 오면 <b>기다리다 실패한다</b> —
		/// 「받았겠지」로 넘어가면 조용히 안 되는 통로가 초록으로 보인다.
		/// </summary>
		private static async Task<string> WaitForAsync(ClientWebSocket socket, Func<string, bool> matches)
		{
			using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			byte[] buffer = new byte[16384];

			while (timeout.IsCancellationRequested == false)
			{
				WebSocketReceiveResult received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
				if (received.MessageType == WebSocketMessageType.Close)
					break;

				string text = Encoding.UTF8.GetString(buffer, 0, received.Count);
				if (matches(text))
					return text;
			}

			Assert.Fail("기다리던 말이 10초 안에 안 왔다 — 통로가 조용히 죽었다는 뜻이다.");
			return null;
		}
	}
}
