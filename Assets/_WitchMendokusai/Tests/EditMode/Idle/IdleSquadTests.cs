using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;
using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 부대 — 맞고 쓰러지고 일어난다 (V2, 사용자 방향 2026-08-23).
	///
	/// ★ 여기서 지키는 것:
	///   ① 적이 실제로 때린다 (안 때리면 깊이의 벽이 시간뿐이다)
	///   ② 하나라도 서 있으면 쓰러진 자리가 <b>부활</b>한다 (방향 7)
	///   ③ 전멸하면 <b>실패</b> — 클리어한 구역으로 물러나 <b>반복</b>에 들어간다 (방향 5·6)
	///   ④ 반복 중에는 자동으로 안 내려간다 — 사람이 「다음 구역」을 눌러야 (방향 6)
	///   ⑤ 이 층을 얹어도 <b>스텝 불변</b>이 산다 (오프라인 정산의 전제)
	/// </summary>
	public sealed class IdleSquadTests
	{
		/// <summary>
		/// ★ 적이 때린다 — <b>못 잡는 깊이</b>에서는 체력이 준다.
		///
		/// ★ 얕은 데서는 안 준다 — 처치 회복이 피해를 앞지르기 때문이다(그게 설계다).
		///   그래서 이 시험은 <b>벽 너머</b>에서 잰다: 잘 잡으면 안 죽고, 못 잡으면 죽는다.
		/// </summary>
		[Test]
		public void EnemiesActuallyHit_WhereTheSquadCannotKeepUp()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Fresh(tuning);
			state.Stage = 40;
			state.BestStage = 40;
			state.ClearedStage = 39;

			IdleModel.StepLive(state, tuning, 30d);

			Assert.IsTrue(state.Repeating, "못 잡는 깊이인데 아무도 안 쓰러졌다");
		}

		/// <summary>★ 잘 잡는 얕은 데서는 <b>안 죽는다</b> — 벽은 시계가 아니라 처치 속도가 만든다.</summary>
		[Test]
		public void WhereKillsAreFast_TheSquadHolds()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Fresh(tuning);

			IdleModel.StepLive(state, tuning, 600d);

			Assert.IsFalse(state.Repeating, "1구역에서 10분 만에 전멸했다 — 초반은 배우는 자리다");
		}

		/// <summary>
		/// ★ <b>자는 동안은 안 죽는다</b> — 자리 비운 몫엔 위험이 없다 (방치형의 기본 계약).
		///
		/// 8시간을 비웠는데 첫 20분에 전멸해 나머지가 헛돌면 그건 도전이 아니라 벌이다.
		/// </summary>
		[Test]
		public void WhileAway_NothingCanWipe()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Fresh(tuning);
			state.Stage = 60;
			state.BestStage = 60;

			IdleModel.Step(state, tuning, 8d * 3600d);

			Assert.IsFalse(state.Repeating, "자리를 비운 사이에 전멸했다");
			Assert.AreEqual(60, state.Stage, "자리를 비운 사이에 구역이 물러났다");
		}

		/// <summary>★ 혼자 서 있다 쓰러지면 그건 전멸 — 실패로 간다 (부활 못 함).</summary>
		[Test]
		public void AloneAndDown_IsAWipe_AndFallsBack()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Fresh(tuning);
			state.Stage = 12;
			state.ClearedStage = 11;
			state.BestStage = 12;

			// 혼자(나) 있는 판이라 쓰러지는 순간 전멸이다.
			IdleModel.StepLive(state, tuning, 600d);

			Assert.IsTrue(state.Repeating, "전멸했는데 반복 모드가 안 켜졌다");
			Assert.AreEqual(11, state.Stage, "실패했는데 클리어한 구역으로 안 물러났다");
			Assert.Greater(state.SeatHealth[0], 0d, "물러났는데 부대가 안 일어났다");
		}

		/// <summary>
		/// ★ 하나라도 서 있으면 쓰러진 자리가 <b>일어난다</b> (방향 7).
		///
		/// 영웅 하나를 앉히고, 앞자리(나)를 일부러 눕힌 뒤 게이지만큼 흘린다.
		/// </summary>
		[Test]
		public void OneStanding_RevivesTheFallen()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Fresh(tuning);
			Seat(state, tuning, 0, 4);

			// 앞자리를 눕힌다 — 뒷자리(영웅)는 서 있다.
			state.SeatHealth[0] = 0d;
			state.SeatReviveSeconds[0] = 0d;

			Assert.AreEqual(1, IdleSquad.StandingCount(state), "판을 못 세웠다 — 하나만 서 있어야 한다");

			IdleModel.StepLive(state, tuning, tuning.ReviveSeconds + 0.5d);

			Assert.Greater(state.SeatHealth[0], 0d, "하나가 서 있는데 쓰러진 자리가 안 일어났다");
			Assert.IsFalse(state.Repeating, "전멸이 아닌데 실패로 처리됐다");
		}

		/// <summary>★ 반복 중에는 클리어해도 안 내려간다 — 「다음 구역」을 눌러야 간다.</summary>
		[Test]
		public void WhileRepeating_TheStageDoesNotAdvanceByItself()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Fresh(tuning);
			state.Repeating = true;
			state.Stage = 3;
			state.BestStage = 5;
			// 적이 안 때리게 — 이 시험이 보려는 것은 «안 내려간다» 하나다.
			tuning.EnemyDamageByStage = new GeometricScale(0d, 1d);

			IdleModel.StepLive(state, tuning, 300d);

			Assert.AreEqual(3, state.Stage, "반복 중인데 저절로 내려갔다");
			Assert.Greater(state.Kills, 0L, "반복 중이라고 싸움까지 멈췄다");
		}

		/// <summary>★ 「다음 구역」 — 반복을 끝내고 한 칸 내려간다. 아닐 때는 아무 일도 없다.</summary>
		[Test]
		public void NextStage_OnlyWorksWhileRepeating()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Fresh(tuning);
			state.Stage = 3;

			Assert.IsFalse(IdleSquad.TryAdvanceStage(state, tuning), "반복 중이 아닌데 다음 구역이 먹혔다");
			Assert.AreEqual(3, state.Stage);

			state.Repeating = true;
			Assert.IsTrue(IdleSquad.TryAdvanceStage(state, tuning), "반복 중인데 다음 구역이 안 먹혔다");
			Assert.AreEqual(4, state.Stage, "다음 구역인데 안 내려갔다");
			Assert.IsFalse(state.Repeating, "다음 구역으로 갔는데 반복이 안 풀렸다");
		}

		/// <summary>★ 쓰러진 자리는 <b>안 때린다</b> — 싸움의 몫이 준다.</summary>
		[Test]
		public void TheFallenDoNotFight()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Fresh(tuning);
			Seat(state, tuning, 0, 4);

			double whole = IdleModel.AttackSpeedOf(state, tuning);

			state.SeatHealth[0] = 0d;
			double halved = IdleModel.AttackSpeedOf(state, tuning);

			Assert.Less(halved, whole, "하나가 쓰러졌는데 판이 그대로 센다");
			Assert.AreEqual(whole * 0.5d, halved, whole * 1e-9d, "둘 중 하나면 몫도 절반이어야 한다");
		}

		/// <summary>
		/// ★ <b>스텝 불변</b> — 부대층을 얹어도 60초 한 번 == 0.1초 600번.
		///
		/// 쓰러짐·부활이 스텝 중간에 나도 같아야 한다. 이게 깨지면 오프라인 정산이 거짓말을 한다.
		/// </summary>
		[Test]
		public void SquadEvents_AreStepInvariant()
		{
			IdleTuning tuning = new IdleTuning();

			IdleState once = Fresh(tuning);
			IdleState split = Fresh(tuning);
			Seat(once, tuning, 0, 4);
			Seat(split, tuning, 0, 4);
			once.Stage = 8;
			split.Stage = 8;

			IdleModel.StepLive(once, tuning, 120d);
			for (int beat = 0; beat < 1200; beat++)
			{
				IdleModel.StepLive(split, tuning, 0.1d);
			}

			Assert.AreEqual(once.Kills, split.Kills, "쪼개 밟았더니 처치 수가 달라졌다");
			Assert.AreEqual(once.Stage, split.Stage, "쪼개 밟았더니 구역이 달라졌다");
			Assert.AreEqual(once.Repeating, split.Repeating, "쪼개 밟았더니 실패 판정이 달라졌다");

			// ★ 체력은 <b>같은 결</b>이면 된다 — 프레임 길이가 곧 위험이 되지 않는다는 계약이다.
			//   회복(처치당)과 피해(초당)가 다른 리듬으로 들어오므로 소수점 아래는 갈릴 수 있다.
			//   갈리면 안 되는 것은 <b>판정</b>(처치·구역·실패)이고, 그건 위에서 엄격히 본다.
			double most = IdleSquad.MaxHealthOf(once, tuning, 0);
			Assert.AreEqual(once.SeatHealth[0], split.SeatHealth[0], most * 0.05d,
				"쪼개 밟았더니 체력이 눈에 띄게 달라졌다");
		}

		/// <summary>★ 물러나면 부대가 회복한다 — 재정비가 아니면 물러날 이유가 없다.</summary>
		[Test]
		public void RetreatingHealsTheSquad()
		{
			IdleTuning tuning = new IdleTuning();
			IdleSession session = new IdleSession(tuning);
			session.State.EnsureSeatRoom(tuning);
			session.State.BestStage = 5;
			session.State.Stage = 5;
			session.State.SeatHealth[0] = 1d;

			Assert.IsTrue(session.Send(new IdleGoToStageIntent(2)), "물러나기가 거절됐다");
			Assert.Greater(session.State.SeatHealth[0], 1d, "물러났는데 회복이 없다");
		}

		/// <summary>★ 저장을 건넌다 — 체력·부활·반복·클리어 구역.</summary>
		[Test]
		public void SquadSurvivesTheSave()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Fresh(tuning);
			state.SeatHealth[0] = 17d;
			state.SeatReviveSeconds[1] = 3d;
			state.Repeating = true;
			state.ClearedStage = 9;

			IdleState back = new IdleState();
			back.Load(state.Save());

			Assert.AreEqual(17d, back.SeatHealth[0], 1e-12d, "체력이 저장을 못 건넜다");
			Assert.AreEqual(3d, back.SeatReviveSeconds[1], 1e-12d, "부활 게이지가 저장을 못 건넜다");
			Assert.IsTrue(back.Repeating, "반복 모드가 저장을 못 건넜다");
			Assert.AreEqual(9, back.ClearedStage, "클리어 구역이 저장을 못 건넜다");
		}

		/// <summary>★ 사진에 실린다 — 화면이 체력을 자기 눈으로 세지 않게.</summary>
		[Test]
		public void TheSnapshotCarriesTheSeats()
		{
			IdleSession session = new IdleSession(new IdleTuning());
			IdleSnapshot snapshot = session.Capture();

			Assert.AreEqual(IdleSquad.SEAT_COUNT, snapshot.Seats.Length, "자리가 사진에 안 실렸다");
			Assert.IsTrue(snapshot.Seats[0].Taken, "나(0번)가 사진에서 빠졌다");
			Assert.IsTrue(snapshot.Seats[0].Standing, "새 판인데 내가 쓰러져 있다");
			Assert.IsFalse(snapshot.Seats[1].Taken, "안 앉힌 자리가 «있다»로 실렸다");
			Assert.Greater(snapshot.EnemyDamagePerSecond, 0d, "적 피해가 사진에 안 실렸다");
		}

		/// <summary>새 판 — 자리를 세운 상태.</summary>
		private static IdleState Fresh(IdleTuning tuning)
		{
			IdleState state = new IdleState();
			state.EnsureSeatRoom(tuning);
			return state;
		}

		/// <summary>파티 자리에 영웅을 앉히고 체력을 세운다.</summary>
		private static void Seat(IdleState state, IdleTuning tuning, int partySlot, int heroId)
		{
			state.Heroes.Add(new IdleHeroOwned(heroId));
			state.Party[partySlot] = heroId;
			state.EnsureSeatRoom(tuning);
		}
	}
}
