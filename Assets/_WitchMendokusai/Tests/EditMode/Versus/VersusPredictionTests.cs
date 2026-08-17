using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 예측이 <b>거짓말이 아닌가</b> (TASK-WM-411, 롤백 넷코드).
	///
	/// 창이 미리 굴린 판은 결국 서버 판과 같아져야 한다. 안 그러면 「내 화면에선 맞혔는데」가 생기고,
	/// 그건 대결 게임에서 가장 나쁜 종류의 버그다. 여기서 재는 것 셋 —
	/// ① 늦게 오는 정정을 얹으면 서버와 같아지나 ② 상대 추측이 틀려도 바로잡히나 ③ 되감기 뒤에도 계속 같이 가나.
	/// </summary>
	public sealed class VersusPredictionTests
	{
		private const float NO_LIMIT = 0f;

		private static VersusRoundState NewRound()
		{
			return new VersusRoundState(
				VersusFighterStats.Default(), VersusFighterStats.Default(), VersusTuning.Default(),
				VersusDuelSim.ARENA_HALF_WIDTH, VersusDuelSim.ARENA_HALF_DEPTH,
				new Numerics.Vector2(-9f, 0f), new Numerics.Vector2(9f, 0f));
		}

		private static VersusInputFrame MineAt(int tick)
		{
			return new VersusInputFrame
			{
				Move = new Numerics.Vector2(Numerics.Mathf.Cos(tick * 0.11f), Numerics.Mathf.Sin(tick * 0.05f)),
				Aim = new Numerics.Vector2(1f, Numerics.Mathf.Sin(tick * 0.2f)),
				Fire = tick % 6 != 0,
				Dash = tick % 40 == 0,
			};
		}

		// 상대는 창이 모르는 방식으로 움직인다 — 그래서 「마지막 것이 계속된다」 추측은 반드시 틀린다.
		private static VersusInputFrame TheirsAt(int tick)
		{
			return new VersusInputFrame
			{
				Move = new Numerics.Vector2(-Numerics.Mathf.Sin(tick * 0.23f), Numerics.Mathf.Cos(tick * 0.17f)),
				Aim = new Numerics.Vector2(-1f, Numerics.Mathf.Cos(tick * 0.31f)),
				Fire = tick % 4 != 0,
				Dash = tick % 33 == 0,
			};
		}

		[Test]
		public void 늦게_오는_정정을_얹으면_서버_판과_같아진다()
		{
			VersusRoundState server = NewRound();
			VersusRoundState client = NewRound();
			VersusPredictor predictor = new VersusPredictor(client, 0);

			VersusInputFrame[] serverFrames = new VersusInputFrame[MatchConstants.VERSUS_PLAYER_COUNT];

			// 회선이 8틱(약 130ms) 늦다고 본다 — 서버가 8틱 전 상태를 지금 보내 준다.
			const int lag = 8;
			VersusRoundSnapshot pending = null;
			int pendingDueAt = -1;

			for (int tick = 1; tick <= 240; tick++)
			{
				// 서버는 두 사람의 진짜 의도로 굴린다(정본).
				serverFrames[0] = MineAt(tick);
				serverFrames[1] = TheirsAt(tick);
				server.Step(serverFrames, NO_LIMIT);

				// 창은 내 의도만 알고 미리 굴린다.
				predictor.Step(MineAt(tick), NO_LIMIT);

				// 늦게 도착하는 정정 + 그때의 상대 의도.
				if (pending != null && tick >= pendingDueAt)
				{
					for (int past = pending.tick + 1; past <= tick; past++)
						predictor.ObserveOpponent(past, TheirsAt(past));

					predictor.ApplyAuthoritative(pending, NO_LIMIT);
					pending = null;
				}

				if (pending == null && tick % 3 == 0)
				{
					pending = server.Capture(tick);
					pendingDueAt = tick + lag;
				}
			}

			// 마지막 정정을 확실히 얹고 나면 두 판이 같아야 한다.
			for (int past = pending != null ? pending.tick + 1 : 0; pending != null && past <= 240; past++)
				predictor.ObserveOpponent(past, TheirsAt(past));

			if (pending != null)
				predictor.ApplyAuthoritative(pending, NO_LIMIT);

			VersusRoundSnapshot last = server.Capture(240);

			for (int past = last.tick + 1; past <= predictor.CurrentTick; past++)
				predictor.ObserveOpponent(past, TheirsAt(past));

			predictor.ApplyAuthoritative(last, NO_LIMIT);

			Assert.AreEqual(server.Fingerprint(), client.Fingerprint(), "정정을 다 얹었는데도 판이 다르다");
			Assert.Greater(predictor.RollbackCount, 0, "한 번도 안 되감았다 — 시험이 예측 경로를 안 지났다");
		}

		[Test]
		public void 되감은_뒤에도_계속_같이_간다()
		{
			VersusRoundState server = NewRound();
			VersusRoundState client = NewRound();
			VersusPredictor predictor = new VersusPredictor(client, 0);
			VersusInputFrame[] serverFrames = new VersusInputFrame[MatchConstants.VERSUS_PLAYER_COUNT];

			for (int tick = 1; tick <= 60; tick++)
			{
				serverFrames[0] = MineAt(tick);
				serverFrames[1] = TheirsAt(tick);
				server.Step(serverFrames, NO_LIMIT);
				predictor.Step(MineAt(tick), NO_LIMIT);
			}

			for (int past = 1; past <= 60; past++)
				predictor.ObserveOpponent(past, TheirsAt(past));

			predictor.ApplyAuthoritative(server.Capture(60), NO_LIMIT);
			Assert.AreEqual(server.Fingerprint(), client.Fingerprint(), "되감기 직후 판이 다르다");

			// 이후로는 상대 의도를 제때 알려 주면 정정 없이도 같이 가야 한다.
			for (int tick = 61; tick <= 120; tick++)
			{
				serverFrames[0] = MineAt(tick);
				serverFrames[1] = TheirsAt(tick);
				server.Step(serverFrames, NO_LIMIT);

				predictor.ObserveOpponent(tick, TheirsAt(tick));
				predictor.Step(MineAt(tick), NO_LIMIT);
			}

			Assert.AreEqual(server.Fingerprint(), client.Fingerprint(), "상대 의도를 다 알고도 갈렸다");
		}

		[Test]
		public void 아주_늦은_정정은_그_자리에_앉힌다()
		{
			// 되감을 기억보다 오래된 정정 — 부드럽진 않아도 <b>판이 갈린 채로 두지는 않는다</b>.
			VersusRoundState server = NewRound();
			VersusRoundState client = NewRound();
			VersusPredictor predictor = new VersusPredictor(client, 0);
			VersusInputFrame[] serverFrames = new VersusInputFrame[MatchConstants.VERSUS_PLAYER_COUNT];

			for (int tick = 1; tick <= 20; tick++)
			{
				serverFrames[0] = MineAt(tick);
				serverFrames[1] = TheirsAt(tick);
				server.Step(serverFrames, NO_LIMIT);
			}

			VersusRoundSnapshot old = server.Capture(20);

			for (int tick = 1; tick <= 200; tick++)
				predictor.Step(MineAt(tick), NO_LIMIT);

			predictor.ApplyAuthoritative(old, NO_LIMIT);

			Assert.AreEqual(server.Fingerprint(), client.Fingerprint(), "아주 늦은 정정 뒤에도 판이 다르다");
			Assert.AreEqual(20, predictor.CurrentTick, "정정을 얹었으면 현재 틱도 그 자리로 와야 한다");
		}
	}
}
