using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.Net;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 네트워크 대결이 <b>진짜로 한 판 끝나나</b> (TASK-WM-411) — 줄 없이, 같은 프로세스 안에서.
	///
	/// ★ 왜 이렇게 재나: 두 컴퓨터를 실제로 붙여야만 알 수 있다고 두면 회귀를 영영 못 잡는다.
	///   심판(<see cref="VersusAuthority"/>)과 손님(<see cref="VersusGuest"/>)은 나르는 방법을 모르므로,
	///   그 자리에 <b>같은 프로세스 구멍</b>을 꽂으면 네트워크 코드 전부가 시험대에 오른다.
	///   여기서 초록이면 남은 위험은 「줄이 진짜 붙나」 하나로 줄어든다.
	/// </summary>
	public sealed class VersusNetPlayTests
	{
		/// <summary> 시험용 글자 변환기 — 서버는 System.Text.Json, 유니티는 JsonUtility 를 꽂는다. </summary>
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
		public void 심판과_손님이_붙어_한_매치가_끝난다()
		{
			TestCodec codec = new TestCodec();
			(VersusLoopbackTransport authoritySide, VersusLoopbackTransport guestSide) = VersusLoopbackTransport.Pair();

			VersusAuthority authority = new VersusAuthority(VersusRules.Default(), VersusTuning.Default(),
				VersusBotTuning.Default(), codec, 4615,
				VersusDuelSim.ARENA_HALF_WIDTH, VersusDuelSim.ARENA_HALF_DEPTH);

			// 0번 자리 = 줄 너머의 사람(손님), 1번 자리 = 봇.
			authority.Attach(0, authoritySide);
			authority.FillWithBot(1, 777);

			VersusGuest guest = new VersusGuest(guestSide, codec, 0);
			VersusBotPolicy guestBrain = new VersusBotPolicy(VersusBotTuning.Default(),
				VersusDuelSim.ARENA_HALF_WIDTH, VersusDuelSim.ARENA_HALF_DEPTH, 1f, 0f);

			int statesSeen = 0;
			int offersSeen = 0;

			// 60Hz 로 90초 = 5선승 한 판에 넉넉하다.
			for (int step = 0; step < 60 * 90 && guest.MatchWinner == VersusMatchCore.NO_WINNER; step++)
			{
				// 손님은 심판이 내려 준 그림만 보고 판단한다 — 자기 규칙을 돌리지 않는다.
				guest.SendInput(guestBrain.Decide(authority.Round, 0, VersusRoundState.TICK, 0f), step);

				if (guest.Offer != null)
				{
					offersSeen++;
					guest.SendPick(0);
				}

				authority.Tick(VersusRoundState.TICK);
				guest.Pump();

				if (guest.Fighters.Length > 0)
					statesSeen++;
			}

			Assert.AreNotEqual(VersusMatchCore.NO_WINNER, guest.MatchWinner, "매치가 안 끝났다");
			Assert.Greater(statesSeen, 100, "손님이 그림을 거의 못 받았다");
			Assert.Greater(offersSeen, 0, "진 라운드가 있었는데 카드 후보가 한 번도 안 왔다");
			Assert.AreEqual(VersusRules.Default().RoundsToWin,
				Numerics.Mathf.Max(guest.ScoreMine, guest.ScoreTheirs), "점수판이 심판과 안 맞는다");
		}

		[Test]
		public void 손님은_위치를_안_보낸다()
		{
			// 「의도만 보낸다」가 이 설계의 뼈대다. 위치를 보내기 시작하면 치팅과 어긋남이 같이 온다.
			TestCodec codec = new TestCodec();
			(VersusLoopbackTransport left, VersusLoopbackTransport right) = VersusLoopbackTransport.Pair();
			VersusGuest guest = new VersusGuest(left, codec, 0);

			guest.SendInput(new VersusInputFrame
			{
				Move = new Numerics.Vector2(1f, 0f),
				Aim = new Numerics.Vector2(0f, 1f),
				Fire = true,
				Dash = false,
			}, 12);

			List<string> sent = new List<string>();
			right.Drain(sent);

			Assert.AreEqual(1, sent.Count);
			Assert.AreEqual(VersusMessageType.INPUT, codec.TypeOf(sent[0]));
			StringAssert.DoesNotContain("\"x\"", sent[0], "입력에 위치가 섞여 있다");
			StringAssert.DoesNotContain("\"y\"", sent[0], "입력에 위치가 섞여 있다");
		}

		[Test]
		public void 심판은_봇_둘로도_혼자_돈다()
		{
			// 서버가 빈 방을 굴려 보거나, 사람이 혼자 연습할 때의 길 — 구멍이 하나도 없어도 판이 선다.
			TestCodec codec = new TestCodec();
			VersusAuthority authority = new VersusAuthority(VersusRules.Default(), VersusTuning.Default(),
				VersusBotTuning.Default(), codec, 20260816,
				VersusDuelSim.ARENA_HALF_WIDTH, VersusDuelSim.ARENA_HALF_DEPTH);

			authority.FillWithBot(0, 11);
			authority.FillWithBot(1, 22);

			// 5선승이라 라운드가 여러 번 돈다 — 시간 예산을 넉넉히(가상 10분).
			for (int step = 0; step < 60 * 600 && authority.Match.IsConcluded == false; step++)
				authority.Tick(VersusRoundState.TICK);

			Assert.IsTrue(authority.Match.IsConcluded, "봇끼리 붙였는데 판이 안 끝났다");
		}
	}
}
