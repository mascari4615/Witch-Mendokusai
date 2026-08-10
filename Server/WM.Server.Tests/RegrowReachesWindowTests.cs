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
	/// <b>다시 자란 것이 창까지 돌아오나</b> (TASK-WM-217).
	///
	/// ★ 왜 또 재나: 판정 층에는 이미 시험이 있다(때가 되면 다시 선다). 그런데 창이 보는 것은
	///   <b>방송</b>이고, 방송은 「바뀐 것만」 보낸다 — 그 사이에 자물쇠가 하나 있었다:
	///   재생이 「들판을 훑을 때」만 일어나고, 훑는 쪽은 「버전이 올랐을 때만」 훑었다.
	///   그래서 아무도 안 훑고 버전도 안 올라, 다시 자란 자리가 <b>창에 영영 안 나타났다</b>.
	///   판정 층이 초록이어도 사람 화면에는 안 보일 수 있다 — 그 사이를 여기서 잰다.
	/// </summary>
	public sealed class RegrowReachesWindowTests
	{
		private const int PORT = 5425;
		private static readonly Uri address = new Uri($"ws://127.0.0.1:{PORT}/ws");

		private WebApplication app;
		private WorldHost host;
		private string worldFile;

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-regrow-" + Path.GetRandomFileName() + ".json");
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
		public async Task 뽑아_간_자리가_때가_되면_창에_다시_뜬다()
		{
			using ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);
			await Read(window, "\"welcome\"");

			// 세계가 처음 보내 준 들판에서 하나 고른다.
			string first = await Read(window, "\"gatherables\"");
			int nodeId = FirstNodeId(first);

			// 그 자리를 뽑는다 — 손이 닿는지는 세계가 보므로, 세계 쪽에서 바로 뽑는다.
			GatherableNode node = FindNode(nodeId);
			Assert.IsTrue(host.World.Gatherables.TryTake(node.Id, node.X, node.Z,
				host.World.Calendar.TotalMinutes(), out int _, out int _), "뽑지도 못했다");

			// 사라진 들판이 한 번 방송돼야 한다(그래야 창이 그 자리를 지운다).
			string afterTake = await Read(window, "\"gatherables\"");
			Assert.IsFalse(Contains(afterTake, nodeId), "뽑았는데 창에는 그대로 서 있다");

			// 하루를 넘겨 흘린다 — 씨앗의 가장 긴 재생도 여섯 시간이다.
			host.World.AdvanceMinutes(60 * 24);

			// ★ 여기가 자물쇠였다: 아무도 훑지 않으면 버전이 안 올라 방송이 안 나간다.
			string back = await Read(window, "\"gatherables\"");
			Assert.IsTrue(Contains(back, nodeId),
				"때가 지났는데도 창에 안 돌아오면, 사람에게 그 들판은 한 번 쓰고 끝이다");
		}

		private GatherableNode FindNode(int nodeId)
		{
			foreach (GatherableNode node in host.World.Gatherables.Alive(host.World.Calendar.TotalMinutes()))
			{
				if (node.Id == nodeId)
					return node;
			}

			Assert.Fail($"세계에 {nodeId} 자리가 없다.");
			return default;
		}

		private static int FirstNodeId(string snapshot)
		{
			using JsonDocument world = JsonDocument.Parse(snapshot);
			JsonElement nodes = world.RootElement.GetProperty("gatherables");
			Assert.Greater(nodes.GetArrayLength(), 0, "세계에 주울 것이 하나도 없다");
			return nodes[0].GetProperty("id").GetInt32();
		}

		private static bool Contains(string snapshot, int nodeId)
		{
			using JsonDocument world = JsonDocument.Parse(snapshot);
			if (world.RootElement.TryGetProperty("gatherables", out JsonElement nodes) == false)
				return false;

			for (int i = 0; i < nodes.GetArrayLength(); i++)
			{
				if (nodes[i].GetProperty("id").GetInt32() == nodeId)
					return true;
			}

			return false;
		}

		private static async Task<string> Read(ClientWebSocket socket, string needle)
		{
			using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			byte[] buffer = new byte[65536];
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
