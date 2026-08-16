using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// <b>서버 없이</b> 둘이 붙나 (TASK-WM-411, P2P 호스트). 한쪽이 문을 열고 다른 쪽이 그 주소로 붙는다.
	///
	/// ★ 왜 재나: 같은 프로세스 「구멍」 시험은 심판·손님 코드를 덮지만 <b>진짜 줄</b>은 안 덮는다.
	///   여기서는 실제 웹소켓이 오간다 — 문이 열리나, 손님이 붙나, 의도가 건너가나, 그림이 돌아오나.
	/// </summary>
	public sealed class VersusP2PTests
	{
		// 시험용 포트. 쓰이고 있으면 그 사실을 알리고 넘어간다(0 을 초록으로 적지 않는다).
		private const int PORT = 57411;

		private sealed class TestCodec : IVersusCodec
		{
			private static readonly System.Text.Json.JsonSerializerOptions Options =
				new System.Text.Json.JsonSerializerOptions { IncludeFields = true };

			public string Encode(object message) =>
				System.Text.Json.JsonSerializer.Serialize(message, message.GetType(), Options);

			public string TypeOf(string message)
			{
				using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(message);
				return document.RootElement.TryGetProperty("type", out System.Text.Json.JsonElement type)
					? type.GetString()
					: string.Empty;
			}

			public T Decode<T>(string message) where T : class =>
				System.Text.Json.JsonSerializer.Deserialize<T>(message, Options);
		}

		[Test]
		public void 서버_없이_문을_열고_친구가_붙어_그림을_받는다()
		{
			TestCodec codec = new TestCodec();

			using VersusHostListener host = new VersusHostListener(PORT);

			if (host.Start() == false)
			{
				// 포트가 막힌 기계에서 「검사 못 함」을 초록으로 적지 않는다.
				Assert.Ignore("문을 못 열었다 — " + host.LastError);
				return;
			}

			ClientWebSocket client = new ClientWebSocket();
			client.ConnectAsync(new Uri($"ws://localhost:{PORT}/vs/"), CancellationToken.None).GetAwaiter().GetResult();

			IVersusTransport guestSide = WaitForGuest(host);
			Assert.IsNotNull(guestSide, "손님이 붙었는데 호스트가 못 받았다");

			VersusAuthority authority = new VersusAuthority(VersusRules.Default(), VersusTuning.Default(),
				VersusBotTuning.Default(), codec, 411,
				VersusDuelSim.ARENA_HALF_WIDTH, VersusDuelSim.ARENA_HALF_DEPTH);

			// 0번 = 이 창(호스트 자신), 1번 = 줄 너머 친구.
			authority.Attach(1, guestSide);

			VersusSocketTransport clientTransport = new VersusSocketTransport(client, CancellationToken.None);
			VersusGuest guest = new VersusGuest(clientTransport, codec, 1);
			VersusBotPolicy hostBrain = new VersusBotPolicy(VersusBotTuning.Default(),
				VersusDuelSim.ARENA_HALF_WIDTH, VersusDuelSim.ARENA_HALF_DEPTH, 1f, 0f);

			int statesSeen = 0;

			for (int step = 0; step < 600 && statesSeen < 5; step++)
			{
				authority.SubmitLocalInput(0, hostBrain.Decide(authority.Round, 0, VersusRoundState.TICK, 0f));
				guest.SendInput(hostBrain.Decide(authority.Round, 1, VersusRoundState.TICK, 0.05f), step);

				authority.Tick(VersusRoundState.TICK);

				// 줄은 스레드로 움직인다 — 조금 기다려 줘야 도착한 것이 보인다.
				Task.Delay(5).GetAwaiter().GetResult();
				guest.Pump();

				if (guest.Fighters.Length > 0)
					statesSeen++;
			}

			Assert.GreaterOrEqual(statesSeen, 5, "친구가 그림을 못 받았다 — 줄은 붙었는데 말이 안 건너간다");
			Assert.AreEqual(1, guest.Seat, "자리 배정이 틀렸다");

			clientTransport.Dispose();
		}

		private static IVersusTransport WaitForGuest(VersusHostListener host)
		{
			for (int attempt = 0; attempt < 100; attempt++)
			{
				IVersusTransport transport = host.TryAccept();

				if (transport != null)
					return transport;

				Task.Delay(20).GetAwaiter().GetResult();
			}

			return null;
		}
	}
}
