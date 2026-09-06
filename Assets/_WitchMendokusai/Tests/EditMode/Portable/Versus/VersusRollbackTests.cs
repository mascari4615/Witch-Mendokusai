using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 되감기가 <b>진짜로</b> 되나 (TASK-WM-411, 롤백 넷코드의 전제).
	///
	/// 온라인 대결에서 내 조작이 서버 왕복을 기다렸다 반응하면 그건 다른 게임이다. 그래서 창이 자기 판을
	/// 미리 굴리고(예측), 서버가 「그 틱은 사실 이랬다」를 보내면 되감아 다시 굴린다(롤백).
	/// 그 전제가 셋이다 — ① 같은 입력에 같은 답 ② 통째로 찍힘 ③ 되돌리면 같은 미래.
	/// <b>여기서 초록이 아니면 예측은 거짓말이 된다.</b>
	/// </summary>
	public sealed class VersusRollbackTests
	{
		private static VersusRoundState NewRound()
		{
			return new VersusRoundState(
				VersusCards.Apply(VersusFighterStats.Default(), VersusCardKind.Bounce),
				VersusCards.Apply(VersusFighterStats.Default(), VersusCardKind.Split),
				VersusTuning.Default(),
				VersusDuelSim.ARENA_HALF_WIDTH, VersusDuelSim.ARENA_HALF_DEPTH,
				new Numerics.Vector2(-9f, 0f), new Numerics.Vector2(9f, 0f));
		}

		// 사람이 실제로 하는 것과 비슷하게 흔들리는 입력 — 고정 입력이면 어떤 버그는 안 드러난다.
		private static VersusInputFrame[] FramesAt(int tick)
		{
			float angle = tick * 0.07f;

			return new[]
			{
				new VersusInputFrame
				{
					Move = new Numerics.Vector2(Numerics.Mathf.Cos(angle), Numerics.Mathf.Sin(angle * 0.7f)),
					Aim = new Numerics.Vector2(1f, Numerics.Mathf.Sin(angle * 0.3f)),
					Fire = tick % 7 != 0,
					Dash = tick % 53 == 0,
				},
				new VersusInputFrame
				{
					Move = new Numerics.Vector2(-Numerics.Mathf.Cos(angle * 1.3f), Numerics.Mathf.Sin(angle)),
					Aim = new Numerics.Vector2(-1f, Numerics.Mathf.Cos(angle * 0.4f)),
					Fire = tick % 5 != 0,
					Dash = tick % 71 == 0,
				},
			};
		}

		[Test]
		public void 같은_입력이면_같은_판이_된다()
		{
			VersusRoundState first = NewRound();
			VersusRoundState second = NewRound();

			for (int tick = 0; tick < 400; tick++)
			{
				first.Step(FramesAt(tick), 0f);
				second.Step(FramesAt(tick), 0f);
			}

			Assert.AreEqual(first.Fingerprint(), second.Fingerprint(), "같은 입력인데 판이 갈렸다 — 예측이 불가능하다");
		}

		[Test]
		public void 되감았다_다시_굴리면_원래와_같아진다()
		{
			VersusRoundState round = NewRound();

			for (int tick = 0; tick < 120; tick++)
				round.Step(FramesAt(tick), 0f);

			VersusRoundSnapshot saved = round.Capture(120);

			// 계속 굴려서 「미래」를 만든다.
			for (int tick = 120; tick < 200; tick++)
				round.Step(FramesAt(tick), 0f);

			int futureFingerprint = round.Fingerprint();

			// 되감고 같은 입력을 다시 굴리면 같은 미래여야 한다.
			round.Restore(saved);

			for (int tick = 120; tick < 200; tick++)
				round.Step(FramesAt(tick), 0f);

			Assert.AreEqual(futureFingerprint, round.Fingerprint(), "되감아 다시 굴렸더니 다른 판이 됐다");
		}

		[Test]
		public void 남의_스냅샷으로_되감아도_따라잡는다()
		{
			// 서버 판과 창 판. 창이 <b>늦게</b> 시작하고, 서버 스냅샷으로 되감아 따라잡는 상황 그대로.
			VersusRoundState server = NewRound();
			VersusRoundState client = NewRound();

			for (int tick = 0; tick < 150; tick++)
				server.Step(FramesAt(tick), 0f);

			VersusRoundSnapshot fromServer = server.Capture(150);

			// 창은 엉뚱한 자리에 있다(예측이 빗나간 상태).
			for (int tick = 0; tick < 40; tick++)
				client.Step(FramesAt(tick * 3), 0f);

			Assert.AreNotEqual(server.Fingerprint(), client.Fingerprint(), "일부러 어긋냈는데 같다 — 시험이 아무것도 안 재고 있다");

			client.Restore(fromServer);

			Assert.AreEqual(server.Fingerprint(), client.Fingerprint(), "서버 스냅샷으로 되감았는데 판이 다르다");

			// 되감은 뒤 같은 입력을 굴리면 계속 같이 간다.
			for (int tick = 150; tick < 220; tick++)
			{
				server.Step(FramesAt(tick), 0f);
				client.Step(FramesAt(tick), 0f);
			}

			Assert.AreEqual(server.Fingerprint(), client.Fingerprint(), "되감은 뒤 다시 갈렸다");
		}

		[Test]
		public void 찍은_것에_굴리는_값이_다_들어_있다()
		{
			// 「그리는 값만」 찍으면 되감기가 조용히 거짓말을 한다 — 탄 속도·쿨다운이 빠지면 다음 틱이 달라진다.
			VersusRoundState round = NewRound();

			for (int tick = 0; tick < 30; tick++)
				round.Step(FramesAt(tick), 0f);

			VersusRoundSnapshot snapshot = round.Capture(30);

			Assert.Greater(snapshot.shots.Length, 0, "탄이 없는 자리에서 재고 있다");
			Assert.IsTrue(System.Array.Exists(snapshot.shots, shot => shot.velocityX != 0f || shot.velocityY != 0f),
				"탄 속도가 안 찍혔다 — 되감으면 탄이 멈춘다");
			Assert.IsTrue(System.Array.Exists(snapshot.fighters, fighter => fighter.fireCooldown != 0f),
				"발사 쿨다운이 안 찍혔다 — 되감으면 연사 속도가 달라진다");
		}
	}
}
