using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using NUnit.Framework;
using WitchMendokusai.Server;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// 계정 왕복을 <b>가장 작은 조각</b>부터 잰다 (TASK-WM-218).
	/// 두 창으로 재다 막혔을 때 「무엇이 안 되는지」를 못 좁혔다 — 그래서 한 창부터 다시 센다.
	/// </summary>
	public sealed class IdentityRoundTripTests
	{
		private const int PORT = 5397;
		private static readonly Uri address = new Uri($"ws://127.0.0.1:{PORT}/ws");

		private WebApplication app;
		private WorldHost host;
		private string worldFile;

		private sealed class AlwaysSamePerson : KarmoLabAccounts
		{
			public AlwaysSamePerson() : base("http://kl.test")
			{
			}

			public override Task<string> TryResolveCodeAsync(string code) => Task.FromResult("karmolab:mascari");

			public override Task<string> TryResolveAsync(string sessionCookie) => Task.FromResult<string>(null);
		}

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-id-" + Path.GetRandomFileName() + ".json");
			host = new WorldHost(new WorldStore(worldFile)) { Accounts = new AlwaysSamePerson() };
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
		public async Task 한_창이_계정_코드로_들어오면_신원이_붙는다()
		{
			using ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);

			await Read(window, "welcome");
			await Send(window, "{\"type\":\"hello\",\"secret\":\"기기\",\"klCode\":\"AAA-111\"}");

			string welcome = await Read(window, "\"identityId\":1");
			StringAssert.Contains("\"identityId\":1", welcome);
		}

		[Test]
		public async Task 두_번째_창도_같은_신원을_받는다()
		{
			using ClientWebSocket first = new ClientWebSocket();
			await first.ConnectAsync(address, CancellationToken.None);
			await Read(first, "welcome");
			await Send(first, "{\"type\":\"hello\",\"secret\":\"기기A\",\"klCode\":\"AAA-111\"}");
			await Read(first, "\"identityId\":1");

			using ClientWebSocket second = new ClientWebSocket();
			await second.ConnectAsync(address, CancellationToken.None);
			await Read(second, "welcome");
			await Send(second, "{\"type\":\"hello\",\"secret\":\"기기B\",\"klCode\":\"BBB-222\"}");

			// 같은 계정이므로 같은 신원이어야 한다(그리고 첫 창은 밀려난다).
			string welcome = await Read(second, "\"identityId\":1");
			StringAssert.Contains("\"identityId\":1", welcome);
		}

		private static async Task Send(ClientWebSocket socket, string json)
		{
			byte[] payload = Encoding.UTF8.GetBytes(json);
			await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, CancellationToken.None);
		}

		private static async Task<string> Read(ClientWebSocket socket, string needle)
		{
			using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
			byte[] buffer = new byte[16384];
			while (timeout.IsCancellationRequested == false)
			{
				WebSocketReceiveResult received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
				if (received.MessageType == WebSocketMessageType.Close)
					break;

				string text = Encoding.UTF8.GetString(buffer, 0, received.Count);
				if (text.Contains(needle))
					return text;
			}

			Assert.Fail($"「{needle}」 가 안 왔다.");
			return null;
		}
	}
}
