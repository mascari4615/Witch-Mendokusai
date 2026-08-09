using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
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

		private static readonly World world = new World();
		private static readonly ConcurrentDictionary<int, WebSocket> sockets = new ConcurrentDictionary<int, WebSocket>();

		public static async Task Main(string[] args)
		{
			WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
			WebApplication app = builder.Build();

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

			await SendAsync(socket, "{\"type\":\"welcome\",\"id\":" + doll.Id + "}");

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

		/// <summary>
		/// 아주 단순한 약속: <c>move x z</c>.
		/// 제대로 된 계약(스키마 한 자리에서 생성)은 증분 2 — 지금은 <b>도는 것</b>이 먼저다.
		/// </summary>
		private static void HandleMessage(int dollId, string text)
		{
			string[] parts = text.Split(' ');
			if (parts.Length != 3 || parts[0] != "move")
				return;

			if (float.TryParse(parts[1], out float x) == false)
				return;

			if (float.TryParse(parts[2], out float z) == false)
				return;

			world.TryMove(dollId, new Vector3(x, 0f, z));
		}

		/// <summary>루프가 조용히 죽는 걸 막는다 — 터지면 적어도 콘솔에 남긴다.</summary>
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
				string snapshot = BuildSnapshot();
				foreach (System.Collections.Generic.KeyValuePair<int, WebSocket> entry in sockets)
				{
					if (entry.Value.State != WebSocketState.Open)
						continue;

					await SendAsync(entry.Value, snapshot);
				}

				await Task.Delay(delayMilliseconds, CancellationToken.None);
			}
		}

		private static string BuildSnapshot()
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("{\"type\":\"world\",\"dolls\":[");

			bool first = true;
			foreach (Doll doll in world.Dolls)
			{
				if (first == false)
					builder.Append(',');

				first = false;
				builder.Append("{\"id\":").Append(doll.Id)
					.Append(",\"x\":").Append(doll.Position.x.ToString("F3"))
					.Append(",\"z\":").Append(doll.Position.z.ToString("F3"))
					.Append('}');
			}

			builder.Append("]}");
			return builder.ToString();
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
