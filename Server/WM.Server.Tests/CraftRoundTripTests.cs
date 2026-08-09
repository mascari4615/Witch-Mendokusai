using System;
using System.IO;
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
	/// 제작을 <b>세계가 판정한다</b> — 진짜 소켓으로 잰다 (TASK-WM-217).
	///
	/// ★ 왜: 제작은 창 안에서 끝났다 — 재료 확인도, 성공 주사위도, 지급도 창이 했다.
	///   창을 고친 사람은 언제나 성공하고 무엇이든 만든다. 이 시험은 「빈손으로 요청하면 거절되고,
	///   재료가 있으면 가방이 실제로 바뀐다」를 서버 왕복으로 확인한다.
	/// </summary>
	public sealed class CraftRoundTripTests
	{
		private const int PORT = 5401;

		private static readonly Uri address = new Uri($"ws://127.0.0.1:{PORT}/ws");

		private WebApplication app;
		private WorldHost host;
		private string worldFile;

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-craft-" + Path.GetRandomFileName() + ".json");
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
		public async Task 들어오면_세계의_제작표를_받는다()
		{
			using ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);

			string book = await Read(window, "\"type\":\"" + Protocol.CRAFT_BOOK + "\"");
			using JsonDocument said = JsonDocument.Parse(book);

			Assert.Greater(said.RootElement.GetProperty("recipes").GetArrayLength(), 0,
				"제작표가 안 오면 창은 무엇을 만들 수 있는지 모른다 — 버튼만 남는다");
		}

		[Test]
		public async Task 빈손으로_만들겠다면_거절한다()
		{
			using ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);
			await Read(window, "\"welcome\"");

			// ★ 시험이 씨앗 줄을 알고 있으면 안 된다 (실측 2026-08-10): 진짜 제작표를 뽑아 꽂자
			//   씨앗에만 있던 줄이 사라져 시험이 「세계가 모르는 제작」으로 죽었다.
			//   그 세계에 실제로 있는 첫 줄을 쓴다 — 자산이 바뀌어도 이 시험은 산다.
			CraftRecipeEntry first = ServerCraftBook.Book.Recipes[0];
			await Send(window, "{\"type\":\"" + Protocol.CRAFT + "\",\"recipeId\":" + first.id + "}");
			string made = await Read(window, "\"type\":\"" + Protocol.CRAFTED + "\"");

			using JsonDocument result = JsonDocument.Parse(made);
			Assert.IsFalse(result.RootElement.GetProperty("attempted").GetBoolean(),
				"재료도 없이 주사위를 굴리면 창을 고친 사람이 공짜로 만든다");
			Assert.AreEqual("재료가 모자란다", result.RootElement.GetProperty("denied").GetString(),
				"왜 안 되는지 안 알려주면 사람은 「고장」으로 읽는다");
		}

		[Test]
		public async Task 재료가_있으면_만들어지고_가방이_바뀐다()
		{
			using ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);

			string welcome = await Read(window, "\"welcome\"");
			int dollId = JsonDocument.Parse(welcome).RootElement.GetProperty("id").GetInt32();

			// 그 세계에 실제로 있는 줄로 잰다 — 반드시 되는 줄(성공률 100)을 고른다.
			CraftRecipeEntry recipe = null;
			for (int i = 0; i < ServerCraftBook.Book.Recipes.Count; i++)
			{
				if (ServerCraftBook.Book.Recipes[i].percentage < 100f)
					continue;

				recipe = ServerCraftBook.Book.Recipes[i];
				break;
			}

			Assert.IsNotNull(recipe, "반드시 되는 제작 줄이 하나도 없으면 이 시험은 주사위를 재는 게 된다");

			// 재료는 세계가 쥐여 준다 — 여기서 재는 것은 「줍기」가 아니라 「제작」이다.
			for (int i = 0; i < recipe.items.Length; i++)
				host.World.TryGather(dollId, ServerItemCatalog.Find(recipe.items[i].itemId), recipe.items[i].amount);

			await Send(window, "{\"type\":\"" + Protocol.CRAFT + "\",\"recipeId\":" + recipe.id + "}");
			string made = await Read(window, "\"type\":\"" + Protocol.CRAFTED + "\"");

			using JsonDocument result = JsonDocument.Parse(made);
			Assert.IsTrue(result.RootElement.GetProperty("succeeded").GetBoolean(), made);
			Assert.AreEqual(recipe.resultItemId, result.RootElement.GetProperty("itemId").GetInt32());

			Assert.AreEqual(recipe.resultAmount, host.World.BagCount(dollId, recipe.resultItemId),
				"만든 것이 가방에 안 들어가면 만든 게 아니다");

			for (int i = 0; i < recipe.items.Length; i++)
			{
				// 결과가 재료이기도 한 줄(나무 → 나무 판자 등)은 남은 수가 0 이 아닐 수 있다 — 그것만 뺀다.
				if (recipe.items[i].itemId == recipe.resultItemId)
					continue;

				Assert.AreEqual(0, host.World.BagCount(dollId, recipe.items[i].itemId),
					"재료가 안 빠지면 무한히 만들 수 있다");
			}
		}


		[Test]
		public async Task 정한_이름이_세계의_그림에_실린다()
		{
			using ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);
			await Read(window, "\"welcome\"");
			await Send(window, "{\"type\":\"hello\",\"secret\":\"기기-이름\"}");
			await Read(window, "\"identityId\"");

			await Send(window, "{\"type\":\"" + Protocol.RENAME + "\",\"name\":\"욘\"}");

			// 이름은 매 그림에 실린다 — 안 실리면 남의 화면에서 나는 영영 「손님」이다.
			string world = await Read(window, "\"name\":\"욘\"");
			StringAssert.Contains("\"name\":\"욘\"", world);
		}

		[Test]
		public async Task 인사도_안_하고_이름부터_정하면_거절한다()
		{
			using ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);
			await Read(window, "\"welcome\"");

			await Send(window, "{\"type\":\"" + Protocol.RENAME + "\",\"name\":\"누구\"}");
			string denied = await Read(window, "\"type\":\"denied\"");

			StringAssert.Contains("먼저 인사를", denied, "조용히 무시하면 사람은 「고장」으로 읽는다");
		}

		private static async Task Send(ClientWebSocket socket, string json)
		{
			byte[] payload = Encoding.UTF8.GetBytes(json);
			await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, CancellationToken.None);
		}

		/// <summary>그 말이 올 때까지 읽는다 — 조각난 알림도 이어 붙인다.</summary>
		private static async Task<string> Read(ClientWebSocket socket, string needle)
		{
			using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
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
