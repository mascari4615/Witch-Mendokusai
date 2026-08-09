using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Server
{
	/// <summary>
	/// 세계 하나를 호스팅하는 서버 (TASK-WM-217).
	///
	/// ★ 왜 클래스인가 (전에는 static Program 이었다): <b>시험이 서버를 띄울 수 있어야</b>
	///   「둘이 붙어서 서로 보이나」를 기계가 확인한다. FishNet 을 지우려면 그 확인이 먼저다 —
	///   지금 유일하게 검증된 멀티가 FishNet 이기 때문이다.
	///   전역 상태였을 때는 시험마다 같은 세계·같은 저장 파일을 물어 서로를 오염시켰다.
	/// </summary>
	public sealed class WorldHost
	{
		/// <summary>1초에 몇 번 모두에게 알릴 것인가.</summary>
		private const int SNAPSHOT_HZ = 20;

		/// <summary>세계를 디스크로 내리는 간격 — 바뀐 게 있을 때만 쓴다.</summary>
		private const int SAVE_INTERVAL_MILLISECONDS = 5000;

		/// <summary>실제 1초에 세계의 몇 분이 흐르나 — 게임의 WorldClockSO 와 맞춰야 할 값.</summary>
		private const float MINUTES_PER_REAL_SECOND = 1f;

		/// <summary>
		/// 아무도 없어도 <b>세계의 시간이 이만큼 흐르면</b> 한 번 적는다 (TASK-WM-218).
		///
		/// ★ 왜: 시각은 사람이 없어도 흐르는데 저장은 사람이 있을 때만 했다 — 그래서 서버를 껐다 켜면
		///   <b>시계가 뒤로 감겼다</b>(실측: 7:34 → 6:45). 지은 건 남는데 시간만 되돌아가는 세계는 이상하다.
		///   그렇다고 매번 쓰면 빈 밤에도 디스크가 돈다 — 그 사이를 이 값이 정한다.
		/// </summary>
		private const int IDLE_SAVE_WORLD_MINUTES = 60;

		private int savedAtWorldMinute;

		/// <summary>이만큼(세계의 날) 안 오고 아무것도 안 남긴 사람은 장부에서 지운다.</summary>
		private const int GUEST_FORGET_DAYS = 90;

		private readonly WorldStore store;
		/// <summary>
		/// 창 하나 — 소켓과 <b>차례 서는 자리</b> (TASK-WM-218).
		///
		/// ★ 왜 차례가 필요한가: 소켓 하나에 두 곳에서 동시에 쓰면 터진다(알림 루프는 20Hz 로 쓰고,
		///   답장은 사람이 말할 때 쓴다). 그 예외는 WebSocketException 이 아니라서 조용히 새 나가
		///   <b>인사에 대한 답이 통째로 사라졌다</b> — 창은 접속은 됐는데 자기가 누군지 모르게 됐다.
		///   시험이 그 자리를 재현해 잡았다.
		/// </summary>
		private sealed class Connection
		{
			public Connection(WebSocket socket)
			{
				Socket = socket;
			}

			public WebSocket Socket { get; }

			public SemaphoreSlim SendGate { get; } = new SemaphoreSlim(1, 1);
		}

		private readonly ConcurrentDictionary<int, Connection> sockets = new ConcurrentDictionary<int, Connection>();
		private int worldDirty;

		public WorldHost(WorldStore worldStore)
		{
			store = worldStore;
		}

		/// <summary>이 서버가 굴리는 세계 — 시험이 들여다본다.</summary>
		public WorldSim World { get; } = new WorldSim { Gatherables = ServerGatherables.Field };

		/// <summary>KarmoLab 계정에 「이 사람 누구냐」고 묻는 자리 — 못 물어보면 손님으로 받는다.</summary>
		public KarmoLabAccounts Accounts { get; set; } = new KarmoLabAccounts();

		/// <summary>세계가 아는 사람들 (TASK-WM-218) — 열쇠로 알아본다.</summary>
		public WitchMendokusai.Identity.WorldIdentityRegistry Identities { get; } = new WitchMendokusai.Identity.WorldIdentityRegistry();

		/// <summary>가방을 되살릴 때 쓰는 아이템 목록 — 게임에서 뽑아 온 그것.</summary>
		private WorldItemCatalog ItemsCatalog => ServerItemCatalog.Catalog;

		/// <summary>세계를 띄운다. <paramref name="url"/> 를 주면 그 자리에(시험은 빈 포트를 쓴다).</summary>
		public WebApplication Build(string[] args, string url = null)
		{
			WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
			WebApplication app = builder.Build();
			if (string.IsNullOrEmpty(url) == false)
				app.Urls.Add(url);

			// 골격 창(wwwroot/index.html) — 서버가 자기 확인용 화면을 같이 준다.
			app.UseDefaultFiles();
			app.UseStaticFiles();
			app.UseWebSockets();

			// 사람이 눈으로 살아있음을 확인하는 자리 — 게이트도 여기를 찌른다.
			// ★ 「살아 있다」만으로는 부족하다: 세계가 <b>돌고 있는지</b>(시각이 흐르는지, 사람이 있는지,
			//   장부가 남아 있는지)를 같이 말한다. 안 그러면 「떠 있는데 시간이 멈춘 세계」를 못 알아본다.
			app.MapGet("/health", () => Results.Json(new
			{
				ok = true,
				people = World.Snapshot().Length,
				identities = Identities.Count,
				buildings = World.Buildings().Length,
				day = World.Calendar.TotalDays(),
				hour = World.Calendar.Hour,
				minute = World.Calendar.Minute,
				worldFile = store.Path,
			}));

			app.Map("/ws", async (HttpContext context) =>
			{
				if (context.WebSockets.IsWebSocketRequest == false)
				{
					context.Response.StatusCode = 400;
					return;
				}

				WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
				await ServeAsync(socket);
			});

			// 세계는 서버보다 오래 산다 (단계 5) — 뜨자마자 지난 기억을 되살린다.
			WorldSaveData loaded = store.TryLoad();
			Identities.Load(loaded?.identities);
			int restored = World.Load(loaded);
			savedAtWorldMinute = World.Calendar.TotalMinutes();
			Console.WriteLine($"[world] 되살린 건물 {restored}개 ({store.Path})");

			// 알림 루프는 서버가 실제로 뜬 뒤에 시작한다 — 뜨기 전에 시작하면 조용히 죽어도 아무도 모른다.
			app.Lifetime.ApplicationStarted.Register(() =>
			{
				_ = RunBroadcastLoopAsync(app.Lifetime.ApplicationStopping);
				_ = RunSaveLoopAsync(app.Lifetime.ApplicationStopping);
			});

			// 꺼질 때 한 번 더 — 마지막 몇 초 사이에 지은 것도 남는다.
			app.Lifetime.ApplicationStopping.Register(() => store.TrySave(SaveWorld()));

			return app;
		}

		private async Task ServeAsync(WebSocket socket)
		{
			// ★ 먼저 받아 주고, 열쇠는 오면 그때 붙인다 (TASK-WM-218).
			//   「인사를 받고 나서 인형을 준다」로 했더니 인사 안 하는 옛 창이 영영 환영을 못 받고
			//   멈춰 섰다(스모크 4개가 그 자리에서 죽었다). 접속은 인사를 기다리지 않는다.
			WorldDoll doll = World.Join();
			Connection connection = new Connection(socket);
			sockets[doll.Id] = connection;
			await SendAsync(connection, Protocol.Welcome(doll.Id));

			// 낱말표는 들어올 때 한 번 — 이게 있어야 창이 「돌 3개」라고 말할 수 있다(없으면 「17450 3개」).
			await SendAsync(connection, Protocol.Catalog(ItemsCatalog.Names()));

			// 이 연결의 말 예산 — 창 하나가 모두의 세계를 느리게 만들지 못하게 (TASK-WM-218).
			WitchMendokusai.Net.MessageBudget budget = new WitchMendokusai.Net.MessageBudget();
			DateTime lastSpoke = DateTime.UtcNow;

			byte[] buffer = new byte[4096];
			try
			{
				while (socket.State == WebSocketState.Open)
				{
					string text = await ReceiveTextAsync(socket, buffer);
					if (text == null)
						break;

					DateTime now = DateTime.UtcNow;
					budget.Refill((float)(now - lastSpoke).TotalSeconds);
					lastSpoke = now;

					// 예산을 넘긴 말은 버린다(끊지는 않는다 — 잠깐 몰릴 수도 있다).
					if (budget.TrySpend() == false)
						continue;

					await HandleMessageAsync(doll.Id, connection, text);
				}
			}
			catch (WebSocketException)
			{
				// 창이 그냥 닫히는 건 사고가 아니다 — 조용히 정리한다.
			}
			finally
			{
				sockets.TryRemove(doll.Id, out Connection _);
				World.Leave(doll.Id);
				Interlocked.Exchange(ref worldDirty, 1); // 나간 사람의 자리·가방을 디스크로 내린다.
			}
		}

		/// <summary>인사를 받으면 그 연결의 인형에 주인을 붙이고, 새 사람이면 열쇠를 준다.</summary>
		private async Task HandleMessageAsync(int dollId, Connection socket, string text)
		{
			if (text.Contains("\"" + Protocol.INVITE_ASK + "\""))
			{
				// 지금 이 연결의 주인에게만 초대 열쇠를 낸다 — 손님(주인 없음)은 낼 수 없다.
				int owner = World.OwnerOf(dollId);
				string code = owner == 0 ? null : Identities.IssueInvite(owner, World.Calendar.TotalDays());
				await SendAsync(socket, Protocol.Invite(code));
				Interlocked.Exchange(ref worldDirty, 1);
				return;
			}

			if (text.Contains("\"" + Protocol.LINK + "\""))
			{
				string code = ReadStringField(text, "code");
				string deviceSecret = CurrentSecretOf(dollId);
				WitchMendokusai.Identity.WorldIdentityRecord linked = Identities.RedeemInvite(
					code, deviceSecret, World.Calendar.TotalDays(), out int previousIdentity);

				// 이 기기가 전에 쓰던 사람이 갖고 있던 것을 옮겨 준다 — 안 옮기면 사람 눈엔 사라진 것이다.
				if (linked != null && previousIdentity != 0 && previousIdentity != linked.id)
					World.MergePerson(previousIdentity, linked.id, ItemsCatalog);

				// 이었어도 지금 인형은 안 바꾼다(접속 도중 주인 갈아타기는 막혀 있다) —
				// 다시 들어오면 그때부터 그 사람이다.
				await SendAsync(socket, Protocol.Linked(linked != null, linked?.id ?? 0));
				Interlocked.Exchange(ref worldDirty, 1);
				return;
			}

			if (text.Contains("\"" + Protocol.HELLO + "\""))
			{
				string secret = ReadHelloSecret(text);

				// 계정을 댔으면 그걸 먼저 본다 — 기기 열쇠는 기기만 알아보기 때문이다.
				string klSession = ReadStringField(text, "klSession");
				string externalId = await Accounts.TryResolveAsync(klSession);

				// 쿠키를 못 읽는 창(게임)은 코드로 온다 — 둘 중 되는 쪽을 쓴다.
				if (string.IsNullOrEmpty(externalId))
					externalId = await Accounts.TryResolveCodeAsync(ReadStringField(text, "klCode"));

				WitchMendokusai.Identity.WorldIdentityRecord person;
				bool created;
				if (string.IsNullOrEmpty(externalId) == false)
					person = Identities.RecognizeExternal(externalId, secret, World.Calendar.TotalDays(), out created);
				else
					person = Identities.Recognize(secret, out created, World.Calendar.TotalDays());
				World.Adopt(dollId, person.id, ItemsCatalog, out int evictedDollId);

				// 중복 로그인 — 일반 MMORPG 처럼 나중에 온 쪽이 이긴다. 밀려난 창에는 이유를 말하고 닫는다
				// (조용히 끊으면 사람은 「버그」로 읽는다).
				if (evictedDollId != 0 && sockets.TryRemove(evictedDollId, out Connection evicted))
				{
					// ★ 밀려난 창 정리를 <b>기다리지 않는다</b> (TASK-WM-218).
					//   기다렸더니 새 창의 인사 답장이 통째로 막혔다 — 닫기(CloseAsync)는 상대의 답을
					//   기다리는데, 그 상대는 이미 우리 말을 안 듣는 중일 수 있다(시험이 잡았다).
					//   그래서 「보내고 닫기」는 옆으로 보내고, 새로 온 사람의 길을 먼저 연다.
					_ = EvictAsync(evicted);
				}

				await SendAsync(socket, Protocol.Welcome(dollId, created ? person.secret : string.Empty, person.id));
				Interlocked.Exchange(ref worldDirty, 1);
				return;
			}

			HandleMessage(dollId, text);
		}

		/// <summary>밀려난 창에 이유를 말하고 닫는다 — 오래 걸려도 다른 사람의 길을 막지 않는다.</summary>
		private async Task EvictAsync(Connection evicted)
		{
			try
			{
				await SendAsync(evicted, Protocol.Kicked());

				// 답을 안 하는 상대도 있다 — 출력만 닫고 손을 뗀다(CloseAsync 는 상대의 답을 기다린다).
				await evicted.Socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "same person elsewhere", CancellationToken.None);
			}
			catch (Exception error)
			{
				Console.WriteLine("[identity] 밀려난 창을 닫다 문제 — 무시하고 계속: " + error.Message);
			}
		}

		/// <summary>세계 + 신원 장부를 함께 뜬다 — 둘이 따로 저장되면 「누구 가방인지」가 갈라진다.</summary>
		private WorldSaveData SaveWorld()
		{
			WorldSaveData data = World.Save();
			data.identities = Identities.Save();
			return data;
		}

		/// <summary>한 마디 받는다. 닫히면 null.</summary>
		private static async Task<string> ReceiveTextAsync(WebSocket socket, byte[] buffer)
		{
			try
			{
				WebSocketReceiveResult received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
				if (received.MessageType == WebSocketMessageType.Close)
					return null;

				return Encoding.UTF8.GetString(buffer, 0, received.Count);
			}
			catch (WebSocketException)
			{
				return null;
			}
		}

		/// <summary>그 연결이 지금 쓰고 있는 기기 열쇠 — 이을 때 이 열쇠를 그 사람에 붙인다.</summary>
		private string CurrentSecretOf(int dollId)
		{
			int owner = World.OwnerOf(dollId);
			return owner == 0 ? null : Identities.Find(owner)?.secret;
		}

		private static string ReadStringField(string text, string name)
		{
			try
			{
				using JsonDocument document = JsonDocument.Parse(text);
				return document.RootElement.TryGetProperty(name, out JsonElement value) ? value.GetString() : null;
			}
			catch (JsonException)
			{
				return null;
			}
		}

		private static string ReadHelloSecret(string text)
		{
			try
			{
				using JsonDocument document = JsonDocument.Parse(text);
				return document.RootElement.TryGetProperty("secret", out JsonElement secret) ? secret.GetString() : null;
			}
			catch (JsonException)
			{
				return null;
			}
		}

		/// <summary>창이 보낸 말을 계약(<see cref="Protocol"/>)대로 읽는다.</summary>
		private void HandleMessage(int dollId, string text)
		{
			try
			{
				using JsonDocument document = JsonDocument.Parse(text);
				JsonElement root = document.RootElement;

				if (root.TryGetProperty("type", out JsonElement type) == false)
					return;

				string kind = type.GetString();

				if (kind == Protocol.MOVE)
				{
					float x = root.TryGetProperty("x", out JsonElement xElement) ? (float)xElement.GetDouble() : 0f;
					float z = root.TryGetProperty("z", out JsonElement zElement) ? (float)zElement.GetDouble() : 0f;
					World.TryMove(dollId, new Vector3(x, 0f, z));
					return;
				}

				if (kind == Protocol.CONSUME)
				{
					// 없는 걸 썼다고 우겨도 소용없다 — 있는 만큼만 빠진다.
					World.TryConsume(dollId, ReadInt(root, "itemId"), System.Math.Max(1, ReadInt(root, "amount")));
					_ = SendBagAsync(dollId);
					Interlocked.Exchange(ref worldDirty, 1);
					return;
				}

				if (kind == Protocol.BAG_ASK)
				{
					// 다시 들어온 창이 자기 가방을 그리려면 물어볼 수 있어야 한다.
					_ = SendBagAsync(dollId);
					return;
				}

				if (kind == Protocol.GATHER)
				{
					// ★ 창은 「저기 있는 저것을 줍겠다」만 말한다 (TASK-WM-217).
					//   전에는 「아이템 X 를 N개 주웠다」고 말하면 세계가 그냥 넣어 줬다 — 그건
					//   판정이 아니라 신고였고, 창을 고친 사람은 무엇이든 무한히 가질 수 있었다.
					int nodeId = ReadInt(root, "nodeId");
					Vector3 standing = World.PositionOf(dollId);
					if (World.Gatherables.TryTake(nodeId, standing.x, standing.z, World.Calendar.TotalMinutes(),
						out int itemId, out int amount) == false)
					{
						return; // 없는 자리거나, 손이 안 닿거나, 방금 남이 가져갔다
					}

					// 가방이 꽉 차면 서버가 덜 넣는다.
					World.TryGather(dollId, ServerItemCatalog.Find(itemId), amount);
					Interlocked.Exchange(ref worldDirty, 1);
					_ = SendBagAsync(dollId);
					return;
				}

				if (kind == Protocol.BREW)
				{
					float dx = root.TryGetProperty("dx", out JsonElement dxElement) ? (float)dxElement.GetDouble() : 0f;
					float dy = root.TryGetProperty("dy", out JsonElement dyElement) ? (float)dyElement.GetDouble() : 0f;
					float grind = root.TryGetProperty("grind", out JsonElement grindElement) ? (float)grindElement.GetDouble() : 1f;

					// 누가 젓든 같은 솥에 쌓인다 — 솥은 세계의 물건이다.
					World.Cauldron.AddStep(new WitchMendokusai.DomainSDK.Alchemy.BrewStep
					{
						Direction = new WitchMendokusai.DomainSDK.Alchemy.BrewVector(dx, dy),
						Grind = grind,
					});

					return;
				}

				if (kind == Protocol.BREW_COMPLETE)
				{
					// 완성은 세계가 한 사람에게만 내준다 — 둘이 같은 순간에 눌러도 뒤엣사람은 빈 솥.
					// 무엇이 나왔는지도 세계가 정한다(마도서) — 그리고 **그 자리에서 가방에 넣는다**.
					if (World.Cauldron.TryComplete(ServerRecipeBook.Book, out BrewCompletion taken))
					{
						if (taken.Empty == false)
						{
							IItemData reward = ServerItemCatalog.Find(taken.ResultItemId);
							int leftover = World.TryGather(dollId, reward, taken.Amount);
							if (leftover > 0)
								Console.WriteLine($"[brew] 가방이 좁아 {leftover}개는 못 넣었다 (인형 {dollId})");

							Interlocked.Exchange(ref worldDirty, 1);
							_ = SendBagAsync(dollId);
						}

						if (sockets.TryGetValue(dollId, out Connection claimer))
							_ = SendAsync(claimer, Protocol.BrewTaken(taken));
					}

					return;
				}

				if (kind == Protocol.BREW_RESET)
				{
					World.Cauldron.ResetBrew();
					return;
				}

				if (kind == Protocol.REMOVE)
				{
					// 부수기도 서버가 판정한다 — 빈 칸을 찍으면 아무 일도 안 일어난다.
					if (World.TryRemoveBuilding(new Vector3Int(ReadInt(root, "x"), ReadInt(root, "y"), ReadInt(root, "z"))))
						Interlocked.Exchange(ref worldDirty, 1);

					return;
				}

				if (kind == Protocol.PLACE)
				{
					int cellX = ReadInt(root, "x");
					int cellY = ReadInt(root, "y");
					int cellZ = ReadInt(root, "z");
					int width = System.Math.Max(1, ReadInt(root, "w"));
					int length = System.Math.Max(1, ReadInt(root, "l"));
					int buildingId = ReadInt(root, "buildingId");

					// 겹치면 서버가 거절한다 — 거절도 판정이다(창이 우기지 못한다).
					if (World.TryPlaceBuilding(new Vector3Int(cellX, cellY, cellZ), new Vector2Int(width, length), buildingId))
						Interlocked.Exchange(ref worldDirty, 1);
				}
			}
			catch (JsonException)
			{
				// 못 알아들을 말은 그냥 버린다 — 창이 이상한 걸 보냈다고 서버가 죽지 않는다.
			}
		}

		/// <summary>그 창에게만 자기 가방을 알린다.</summary>
		private async Task SendBagAsync(int dollId)
		{
			if (sockets.TryGetValue(dollId, out Connection socket) == false)
				return;

			// 가방에 든 것 **전부**. 전에는 서버가 아는 두 종류만 물어 봐서, 나머지는 갖고 있어도 창에 안 보였다.
			System.Collections.Generic.List<BagSaveEntry> bag = World.BagOf(dollId);
			System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int, int>> counts =
				new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int, int>>(bag.Count);

			for (int i = 0; i < bag.Count; i++)
				counts.Add(new System.Collections.Generic.KeyValuePair<int, int>(bag[i].itemId, bag[i].amount));

			await SendAsync(socket, Protocol.Bag(counts));
		}

		private int ReadInt(JsonElement root, string name)
		{
			return root.TryGetProperty(name, out JsonElement element) ? (int)element.GetDouble() : 0;
		}

		/// <summary>
		/// 바뀐 게 있을 때만 디스크로 내려간다 (TASK-WM-217 단계 5).
		/// 매번 쓰면 아무도 안 짓는 밤에도 디스크가 초당 20번 돈다 — 그건 세계가 아니라 소음이다.
		/// </summary>
		private async Task RunSaveLoopAsync(CancellationToken stopping)
		{
			try
			{
				while (stopping.IsCancellationRequested == false)
				{
					await Task.Delay(SAVE_INTERVAL_MILLISECONDS, CancellationToken.None);

					// ⚠ 움직임은 dirty 를 안 찍는다(초당 20번 찍으면 뜻이 없다). 그래서 사람이 있으면
					//   그 자체로 「바뀌는 중」으로 본다 — 안 그러면 걷기만 하다 서버가 죽었을 때
					//   그동안 걸어온 자리가 통째로 사라진다(가방은 남고 자리만 옛것이 되는 이상한 상태).
					bool someoneIsHere = World.Snapshot().Length > 0;

					// 아무도 없어도 시간이 꽤 흘렀으면 적는다 — 안 그러면 시계가 뒤로 감긴다.
					int now = World.Calendar.TotalMinutes();
					bool clockDrifted = now - savedAtWorldMinute >= IDLE_SAVE_WORLD_MINUTES;

					if (Interlocked.Exchange(ref worldDirty, 0) == 0 && someoneIsHere == false && clockDrifted == false)
						continue;

					savedAtWorldMinute = now;

					// 빈손이고 오래 안 온 손님은 장부에서 지운다 — 안 그러면 장부가 영원히 커진다.
					// 뭔가 남긴 사람은 절대 안 지운다(세계를 지우는 짓이다).
					int forgotten = Identities.PruneGuests(World.Calendar.TotalDays(), GUEST_FORGET_DAYS, World.OwnsSomething);
					if (forgotten > 0)
						Console.WriteLine($"[identity] 빈손 손님 {forgotten}명을 장부에서 지웠다.");

					store.TrySave(SaveWorld());
				}
			}
			catch (Exception exception)
			{
				Console.WriteLine("[wm-server] 저장 루프가 죽었다: " + exception);
			}
		}

		private async Task RunBroadcastLoopAsync(CancellationToken stopping)
		{
			try
			{
				await BroadcastLoopAsync(stopping);
			}
			catch (Exception exception)
			{
				Console.WriteLine("[wm-server] 알림 루프가 죽었다: " + exception);
			}
		}

		private async Task BroadcastLoopAsync(CancellationToken stopping)
		{
			int delayMilliseconds = 1000 / SNAPSHOT_HZ;
			float minutesPerTick = MINUTES_PER_REAL_SECOND * (delayMilliseconds / 1000f);

			while (stopping.IsCancellationRequested == false)
			{
				// 세계의 시간은 <b>사람이 있든 없든</b> 흐른다 — 서버가 굴리는 이유가 그것이다.
				if (World.AdvanceMinutes(minutesPerTick))
					Interlocked.Exchange(ref worldDirty, 1);

				string snapshot = Protocol.WorldSnapshot(World.Snapshot(), World.Buildings(), World.Calendar, World.Cauldron,
					World.Gatherables.Alive(World.Calendar.TotalMinutes()));
				foreach (System.Collections.Generic.KeyValuePair<int, Connection> entry in sockets)
				{
					if (entry.Value.Socket.State != WebSocketState.Open)
						continue;

					await SendAsync(entry.Value, snapshot);
				}

				await Task.Delay(delayMilliseconds, CancellationToken.None);
			}
		}

		/// <summary>
		/// 한 창에 한 마디. <b>차례를 서서</b> 보낸다 — 두 곳에서 동시에 쓰면 소켓이 터진다.
		/// </summary>
		private async Task SendAsync(Connection connection, string text)
		{
			byte[] payload = Encoding.UTF8.GetBytes(text);
			await connection.SendGate.WaitAsync();
			try
			{
				await connection.Socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, CancellationToken.None);
			}
			catch (WebSocketException)
			{
				// 끊긴 창에 보내다 나는 오류 — 다음 정리 때 빠진다.
			}
			finally
			{
				connection.SendGate.Release();
			}
		}
	}
}
