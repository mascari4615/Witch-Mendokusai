using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 예측이 <b>실제 대결 경로</b>에서도 맞나 (TASK-WM-411).
	///
	/// 앞선 시험은 예측기만 따로 재고, 여기서는 심판·손님이 진짜 말을 주고받는 상태에서 잰다 —
	/// 라운드 재료(스탯·시작 자리)가 오고, 손님이 미리 굴리고, 스냅샷이 오면 되감는다.
	/// 마지막에 <b>두 판의 지문이 같아야</b> 「내 화면에선 맞혔는데」가 없다.
	/// </summary>
	public sealed class VersusPredictedPlayTests
	{
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

		private static VersusInputFrame MineAt(int tick)
		{
			return new VersusInputFrame
			{
				Move = new Numerics.Vector2(Numerics.Mathf.Cos(tick * 0.09f), Numerics.Mathf.Sin(tick * 0.13f)),
				Aim = new Numerics.Vector2(1f, Numerics.Mathf.Sin(tick * 0.21f)),
				Fire = tick % 5 != 0,
				Dash = tick % 47 == 0,
			};
		}

		[Test]
		public void 손님이_미리_굴려도_심판_판과_같아진다()
		{
			TestCodec codec = new TestCodec();
			(VersusLoopbackTransport authoritySide, VersusLoopbackTransport guestSide) = VersusLoopbackTransport.Pair();

			VersusAuthority authority = new VersusAuthority(VersusRules.Default(), VersusTuning.Default(),
				VersusBotTuning.Default(), codec, 5150,
				VersusDuelSim.ARENA_HALF_WIDTH, VersusDuelSim.ARENA_HALF_DEPTH);

			authority.Attach(0, authoritySide);
			authority.FillWithBot(1, 99);

			VersusGuest guest = new VersusGuest(guestSide, codec, 0);

			// 라운드 재료가 오기 전 한 번 굴려도 터지지 않아야 한다(붙자마자의 몇 프레임).
			guest.StepAndSend(MineAt(0));
			guest.Pump();

			// ★ 지연을 실제로 준다: 손님은 매 틱 미리 굴리고, 심판의 말은 5틱에 한 번 몰아서 도착한다.
			//   지연 0 이면 되감을 것이 없어 예측 경로를 안 지난다(2026-08-17 실측: 롤백 0).
			for (int step = 1; step <= 240; step++)
			{
				guest.StepAndSend(MineAt(step));
				authority.Tick(VersusRoundState.TICK);

				if (step % 5 == 0)
					guest.Pump();
			}

			Assert.IsNotNull(guest.Predicted, "라운드 재료가 안 와서 손님이 판을 못 지었다");
			Assert.Greater(guest.RollbackCount, 0, "한 번도 안 되감았다 — 예측 경로를 안 지났다");

			// 마지막 정정까지 반영되도록, 손님은 더 안 굴리고 심판 말만 받는다.
			for (int step = 0; step < 20; step++)
			{
				authority.Tick(VersusRoundState.TICK);
				guest.Pump();
			}

			Assert.AreEqual(authority.Round.Fingerprint(), guest.Predicted.Fingerprint(),
				"손님이 미리 굴린 판이 심판 판과 다르다 — 「내 화면에선 맞혔는데」가 나는 상태");
		}

		[Test]
		public void 내_조작은_기다리지_않고_바로_반영된다()
		{
			// 예측의 존재 이유 — 심판을 한 번도 안 굴려도 내 인형이 움직여야 한다.
			TestCodec codec = new TestCodec();
			(VersusLoopbackTransport authoritySide, VersusLoopbackTransport guestSide) = VersusLoopbackTransport.Pair();

			VersusAuthority authority = new VersusAuthority(VersusRules.Default(), VersusTuning.Default(),
				VersusBotTuning.Default(), codec, 777,
				VersusDuelSim.ARENA_HALF_WIDTH, VersusDuelSim.ARENA_HALF_DEPTH);

			authority.Attach(0, authoritySide);
			authority.FillWithBot(1, 5);

			VersusGuest guest2 = new VersusGuest(guestSide, codec, 0);
			guest2.Pump(); // 라운드 재료 수신

			Assert.IsNotNull(guest2.Predicted, "라운드 재료가 안 왔다");

			Numerics.Vector2 before = guest2.Predicted.PositionOf(0);

			// 심판을 <b>한 번도</b> 안 굴린다.
			for (int step = 0; step < 20; step++)
			{
				guest2.StepAndSend(new VersusInputFrame
				{
					Move = new Numerics.Vector2(1f, 0f),
					Aim = new Numerics.Vector2(1f, 0f),
					Fire = false,
					Dash = false,
				});
			}

			Numerics.Vector2 after = guest2.Predicted.PositionOf(0);

			Assert.Greater(after.x - before.x, 0.5f, "심판을 안 굴렸다고 내 인형이 안 움직였다 — 예측이 죽어 있다");
		}
	}
}
