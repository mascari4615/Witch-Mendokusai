using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Server
{
	/// <summary>
	/// 판정을 굴리는 서버의 최소 사이클 (TASK-WM-216 증분 1).
	///
	/// 하는 일: 웹소켓을 받고 → 접속마다 인형 하나 주고 → 움직임 요청을 <b>서버가 판정</b>하고
	/// → 일정 간격으로 모두에게 현재 모습을 보낸다.
	///
	/// 안 하는 일: 그리기·소리·물리 충돌. 그건 창(Unity · 웹)의 몫이다.
	/// </summary>
	public static class Program
	{
		/// <summary>1초에 몇 번 모두에게 알릴 것인가.</summary>
		private const int SNAPSHOT_HZ = 20;

		private static readonly WorldSim world = new WorldSim();
		private static readonly ConcurrentDictionary<int, WebSocket> sockets = new ConcurrentDictionary<int, WebSocket>();

		public static async Task Main(string[] args)
		{
			// 계약 뽑기 — 서버가 소유한 정의에서 웹이 쓸 타입 선언을 만든다.
			// 시험이 「뽑은 것 == 저장된 것」을 보므로, 계약을 고치면 이 명령을 다시 돌려야 한다.
			if (args.Length > 0 && args[0] == "--emit-protocol")
			{
				string outputPath = System.IO.Path.Combine(AppContext.BaseDirectory, "wwwroot", "protocol.d.ts");
				if (args.Length > 1)
					outputPath = args[1];

				System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outputPath));
				System.IO.File.WriteAllText(outputPath, Protocol.ToTypeScript());
				Console.WriteLine("계약을 뽑았다: " + outputPath);
				return;
			}

			WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
			WebApplication app = builder.Build();

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

			// 알림 루프는 서버가 실제로 뜬 뒤에 시작한다 — 뜨기 전에 시작하면 조용히 죽어도 아무도 모른다.
			app.Lifetime.ApplicationStarted.Register(() => _ = RunBroadcastLoopAsync(app.Lifetime.ApplicationStopping));

			await app.RunAsync();
		}

		private static async Task ServeAsync(WebSocket socket)
		{
			Doll doll = world.Join();
			sockets[doll.Id] = socket;

			await SendAsync(socket, Protocol.Welcome(doll.Id));

			byte[] buffer = new byte[1024];
			try
			{
				while (socket.State == WebSocketState.Open)
				{
					WebSocketReceiveResult received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
					if (received.MessageType == WebSocketMessageType.Close)
						break;

					string text = Encoding.UTF8.GetString(buffer, 0, received.Count);
					HandleMessage(doll.Id, text);
				}
			}
			catch (WebSocketException)
			{
				// 창이 그냥 닫히는 건 사고가 아니다 — 조용히 정리한다.
			}
			finally
			{
				sockets.TryRemove(doll.Id, out WebSocket _);
				world.Leave(doll.Id);
			}
		}

		/// <summary>창이 보낸 말을 계약(<see cref="Protocol"/>)대로 읽는다.</summary>
		private static void HandleMessage(int dollId, string text)
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
					world.TryMove(dollId, new Vector3(x, 0f, z));
					return;
				}

				if (kind == Protocol.GATHER)
				{
					int itemId = ReadInt(root, "itemId");
					int amount = System.Math.Max(1, ReadInt(root, "amount"));

					// 가방이 꽉 차면 서버가 덜 넣는다 — 창이 우겨도 소용없다.
					world.TryGather(dollId, ServerItemCatalog.Find(itemId), amount);
					_ = SendBagAsync(dollId);
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
					world.TryPlaceBuilding(new Vector3Int(cellX, cellY, cellZ), new Vector2Int(width, length), buildingId);
				}
			}
			catch (JsonException)
			{
				// 못 알아들을 말은 그냥 버린다 — 창이 이상한 걸 보냈다고 서버가 죽지 않는다.
			}
		}

		/// <summary>그 창에게만 자기 가방을 알린다.</summary>
		private static async Task SendBagAsync(int dollId)
		{
			if (sockets.TryGetValue(dollId, out WebSocket socket) == false)
				return;

			System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int, int>> counts =
				new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int, int>>
				{
					new System.Collections.Generic.KeyValuePair<int, int>(ServerItemCatalog.STONE, world.BagCount(dollId, ServerItemCatalog.STONE)),
					new System.Collections.Generic.KeyValuePair<int, int>(ServerItemCatalog.HERB, world.BagCount(dollId, ServerItemCatalog.HERB)),
				};

			await SendAsync(socket, Protocol.Bag(counts));
		}

		private static int ReadInt(JsonElement root, string name)
		{
			return root.TryGetProperty(name, out JsonElement element) ? (int)element.GetDouble() : 0;
		}

		private static async Task RunBroadcastLoopAsync(CancellationToken stopping)
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

		private static async Task BroadcastLoopAsync(CancellationToken stopping)
		{
			int delayMilliseconds = 1000 / SNAPSHOT_HZ;

			while (stopping.IsCancellationRequested == false)
			{
				string snapshot = Protocol.WorldSnapshot(world.Snapshot(), world.Buildings());
				foreach (System.Collections.Generic.KeyValuePair<int, WebSocket> entry in sockets)
				{
					if (entry.Value.State != WebSocketState.Open)
						continue;

					await SendAsync(entry.Value, snapshot);
				}

				await Task.Delay(delayMilliseconds, CancellationToken.None);
			}
		}

		private static async Task SendAsync(WebSocket socket, string text)
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
