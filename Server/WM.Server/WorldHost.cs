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

		private readonly WorldStore store;
		private readonly ConcurrentDictionary<int, WebSocket> sockets = new ConcurrentDictionary<int, WebSocket>();
		private int worldDirty;

		public WorldHost(WorldStore worldStore)
		{
			store = worldStore;
		}

		/// <summary>이 서버가 굴리는 세계 — 시험이 들여다본다.</summary>
		public WorldSim World { get; } = new WorldSim();

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
			app.MapGet("/health", () => "wm-server ok");

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
			sockets[doll.Id] = socket;
			await SendAsync(socket, Protocol.Welcome(doll.Id));

			byte[] buffer = new byte[4096];
			try
			{
				while (socket.State == WebSocketState.Open)
				{
					string text = await ReceiveTextAsync(socket, buffer);
					if (text == null)
						break;

					await HandleMessageAsync(doll.Id, socket, text);
				}
			}
			catch (WebSocketException)
			{
				// 창이 그냥 닫히는 건 사고가 아니다 — 조용히 정리한다.
			}
			finally
			{
				sockets.TryRemove(doll.Id, out WebSocket _);
				World.Leave(doll.Id);
				Interlocked.Exchange(ref worldDirty, 1); // 나간 사람의 자리·가방을 디스크로 내린다.
			}
		}

		/// <summary>인사를 받으면 그 연결의 인형에 주인을 붙이고, 새 사람이면 열쇠를 준다.</summary>
		private async Task HandleMessageAsync(int dollId, WebSocket socket, string text)
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
				WitchMendokusai.Identity.WorldIdentityRecord linked = Identities.RedeemInvite(code, deviceSecret, World.Calendar.TotalDays());

				// 이었어도 지금 인형은 안 바꾼다(접속 도중 주인 갈아타기는 막혀 있다) —
				// 다시 들어오면 그때부터 그 사람이다.
				await SendAsync(socket, Protocol.Linked(linked != null, linked?.id ?? 0));
				Interlocked.Exchange(ref worldDirty, 1);
				return;
			}

			if (text.Contains("\"" + Protocol.HELLO + "\""))
			{
				string secret = ReadHelloSecret(text);
				WitchMendokusai.Identity.WorldIdentityRecord person = Identities.Recognize(secret, out bool created);
				World.Adopt(dollId, person.id, ItemsCatalog);

				// 새 열쇠는 새로 만들었을 때만 — 기기에 적어 둬야 다음에 「나」다.
				await SendAsync(socket, Protocol.Welcome(dollId, created ? person.secret : string.Empty, person.id));
				Interlocked.Exchange(ref worldDirty, 1);
				return;
			}

			HandleMessage(dollId, text);
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
					int itemId = ReadInt(root, "itemId");
					int amount = System.Math.Max(1, ReadInt(root, "amount"));

					// 가방이 꽉 차면 서버가 덜 넣는다 — 창이 우겨도 소용없다.
					World.TryGather(dollId, ServerItemCatalog.Find(itemId), amount);
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
					if (World.Cauldron.TryComplete(out WitchMendokusai.DomainSDK.Alchemy.BrewState taken)
						&& sockets.TryGetValue(dollId, out WebSocket claimer))
					{
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
			if (sockets.TryGetValue(dollId, out WebSocket socket) == false)
				return;

			System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int, int>> counts =
				new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int, int>>
				{
					new System.Collections.Generic.KeyValuePair<int, int>(ServerItemCatalog.STONE, World.BagCount(dollId, ServerItemCatalog.STONE)),
					new System.Collections.Generic.KeyValuePair<int, int>(ServerItemCatalog.HERB, World.BagCount(dollId, ServerItemCatalog.HERB)),
				};

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

					if (Interlocked.Exchange(ref worldDirty, 0) == 0)
						continue;

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

				string snapshot = Protocol.WorldSnapshot(World.Snapshot(), World.Buildings(), World.Calendar, World.Cauldron);
				foreach (System.Collections.Generic.KeyValuePair<int, WebSocket> entry in sockets)
				{
					if (entry.Value.State != WebSocketState.Open)
						continue;

					await SendAsync(entry.Value, snapshot);
				}

				await Task.Delay(delayMilliseconds, CancellationToken.None);
			}
		}

		private async Task SendAsync(WebSocket socket, string text)
		{
			byte[] payload = Encoding.UTF8.GetBytes(text);
			try
			{
				await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, CancellationToken.None);
			}
			catch (WebSocketException)
			{
				// 끊긴 창에 보내다 나는 오류 — 다음 정리 때 빠진다.
			}
		}
	}
}
