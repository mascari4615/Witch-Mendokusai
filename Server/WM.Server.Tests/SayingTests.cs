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
	/// <b>말이 오간다</b> — 그리고 아무 데나 가지 않는다 (TASK-WM-250).
	///
	/// ★ 왜 코어인가: 「같이 노는 세계」인데 말을 못 걸면 그건 같은 화면을 보는 두 사람일 뿐이다.
	///   그리고 말은 사람이 <b>직접 짓는</b> 유일한 것이라, 세계가 안 보면 한 줄로 남의 화면을 부순다.
	///
	/// ★ 왜 「보이는 사람에게만」인가: 세계 반대편까지 가면 그건 대화가 아니라 확성기다.
	///   누가 보이나는 이미 세계가 아는 것(관심 반경)이라 새로 정하지 않는다.
	/// </summary>
	public sealed class SayingTests
	{
		private const int PORT = 5418;

		private static readonly Uri address = new Uri($"ws://127.0.0.1:{PORT}/ws");

		private WebApplication app;
		private WorldHost host;
		private string worldFile;

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-say-" + Path.GetRandomFileName() + ".json");
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
		public async Task 옆_사람의_말이_들린다()
		{
			using ClientWebSocket speaker = await JoinAsync();
			using ClientWebSocket listener = await JoinAsync();

			await SendAsync(speaker, "{\"type\":\"" + Protocol.SAY + "\",\"text\":\"같이 갈래?\"}");

			string heard = await ReadUntilAsync(listener, text => text.Contains("\"type\":\"" + Protocol.SAID + "\""), 10);
			StringAssert.Contains("같이 갈래?", heard, "옆에 있는데 말이 안 들리면 같이 노는 세계가 아니다");
		}

		[Test]
		public async Task 멀리_있는_사람에게는_안_들린다()
		{
			using ClientWebSocket speaker = await JoinAsync();
			using ClientWebSocket faraway = await JoinAsync();

			WorldDoll[] people = host.World.Snapshot();
			Assert.AreEqual(2, people.Length);
			int farId = people[1].Id;

			// 관심 반경 밖으로 걸어 보낸다(세계의 손으로 — 걸음 심판은 여기 주제가 아니다).
			for (int step = 0; step < 80; step++)
				host.World.TryMove(farId, new Vector3(1f, 0f, 0f));

			await Task.Delay(300);
			await SendAsync(speaker, "{\"type\":\"" + Protocol.SAY + "\",\"text\":\"여기까지 들리나\"}");

			bool heard = await SawWithinAsync(faraway, Protocol.SAID, 1500);
			Assert.IsFalse(heard, "세계 반대편까지 말이 가면 그건 대화가 아니라 확성기다");
		}

		[Test]
		public async Task 빈_말은_아무에게도_안_간다()
		{
			using ClientWebSocket speaker = await JoinAsync();
			using ClientWebSocket listener = await JoinAsync();

			await SendAsync(speaker, "{\"type\":\"" + Protocol.SAY + "\",\"text\":\"   \"}");

			bool heard = await SawWithinAsync(listener, Protocol.SAID, 1500);
			Assert.IsFalse(heard, "빈 줄이 남에게 가면 그건 소음 장치가 된다");
		}

		[Test]
		public async Task 내가_한_말은_나에게도_들린다()
		{
			using ClientWebSocket speaker = await JoinAsync();

			await SendAsync(speaker, "{\"type\":\"" + Protocol.SAY + "\",\"text\":\"혼잣말\"}");

			string heard = await ReadUntilAsync(speaker, text => text.Contains("\"type\":\"" + Protocol.SAID + "\""), 10);
			StringAssert.Contains("혼잣말", heard, "내 말이 내 화면에 안 뜨면 갔는지 안 갔는지 모른다");
		}

		private static async Task<ClientWebSocket> JoinAsync()
		{
			ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);
			byte[] hello = Encoding.UTF8.GetBytes("{\"type\":\"hello\",\"secret\":\"\"}");
			await window.SendAsync(new ArraySegment<byte>(hello), WebSocketMessageType.Text, true, CancellationToken.None);
			return window;
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

		/// <summary>그 시간 안에 그 말이 왔나 — 안 오는 것을 확인할 때 쓴다.</summary>
		private static async Task<bool> SawWithinAsync(ClientWebSocket window, string kind, int milliseconds)
		{
			using CancellationTokenSource stopping = new CancellationTokenSource(milliseconds);
			byte[] bin = new byte[65536];

			try
			{
				while (window.State == WebSocketState.Open)
				{
					WebSocketReceiveResult came = await window.ReceiveAsync(new ArraySegment<byte>(bin), stopping.Token);
					if (came.MessageType == WebSocketMessageType.Close)
						break;

					if (Encoding.UTF8.GetString(bin, 0, came.Count).Contains("\"type\":\"" + kind + "\""))
						return true;
				}
			}
			catch (OperationCanceledException)
			{
				// 안 왔다 — 그게 답이다.
			}

			return false;
		}
	}
}
