using System.Diagnostics;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 사거리 전투 시뮬 (combat.md 7, T1~T10).
	///
	/// ★ 지키는 것: 결정성, 사거리가 자리 결정, 사거리 밖이면 이동, 맨 앞이 피격,
	///   원거리 적은 원거리 사격, 구역 진행, 전멸, 성능, 오프라인 실측 근사
	/// </summary>
	public sealed class IdleBattleSimTests
	{
		private const int MELEE_DOLL = 0;   // 세모, Damage 축, 사거리 2
		private const int MID_DOLL = 3;     // 여섯모, Speed 축, 사거리 5
		private const int RANGED_DOLL = 1;  // 네모, Base 축, 사거리 8

		private static IdleState Fresh(IdleTuning tuning, params int[] party)
		{
			IdleState state = new IdleState();
			IdleDolls.EnsureStarter(state);

			for (int seat = 0; seat < party.Length; seat++)
			{
				if (state.IndexOfDoll(party[seat]) < 0)
				{
					state.Dolls.Add(new IdleDollOwned(party[seat]));
				}

				state.Party[seat] = party[seat];
			}

			for (int seat = party.Length; seat < IdleSquad.SEAT_COUNT; seat++)
			{
				state.Party[seat] = -1;
			}

			state.EnsureSeatRoom(tuning);
			IdleBattleSim.Reset(state, tuning);
			return state;
		}

		/// <summary>적 하나만 남기고 원하는 거리에</summary>
		private static IdleFoe OneFoe(IdleState state, double x, IdleFoeKind kind = IdleFoeKind.Melee)
		{
			IdleBattle battle = state.Battle;
			while (battle.Foes.Count > 1)
			{
				battle.Foes.RemoveAt(battle.Foes.Count - 1);
			}

			IdleFoe foe = battle.Foes[0];
			foe.X = x;
			foe.Y = 0d;
			foe.Kind = kind;
			foe.Range = kind == IdleFoeKind.Ranged ? 6d : 1.5d;
			foe.MaxHealth = 1e12d;
			foe.Health = foe.MaxHealth;
			return foe;
		}

		/// <summary>T1. 같은 판, 같은 시간이면 위치와 처치 동일</summary>
		[Test]
		public void SameSeed_SameResult()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState one = Fresh(tuning, MELEE_DOLL, RANGED_DOLL);
			IdleState two = Fresh(tuning, MELEE_DOLL, RANGED_DOLL);

			IdleModel.StepLive(one, tuning, 60d);
			IdleModel.StepLive(two, tuning, 60d);

			Assert.AreEqual(one.Kills, two.Kills, "처치가 갈렸다");
			Assert.AreEqual(one.Stage, two.Stage, "구역이 갈렸다");
			for (int seat = 0; seat < IdleSquad.SEAT_COUNT; seat++)
			{
				Assert.AreEqual(one.Battle.X[seat], two.Battle.X[seat], 0d, "자리 " + seat + " 위치가 갈렸다");
			}
		}

		/// <summary>T1'. 60s 한 번과 0.1s 600번 동일 (틱 고정 + 이월)</summary>
		[Test]
		public void FrameLength_DoesNotChangeTheBattle()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState once = Fresh(tuning, MELEE_DOLL, RANGED_DOLL);
			IdleState split = Fresh(tuning, MELEE_DOLL, RANGED_DOLL);

			IdleModel.StepLive(once, tuning, 60d);
			for (int beat = 0; beat < 600; beat++)
			{
				IdleModel.StepLive(split, tuning, 0.1d);
			}

			Assert.AreEqual(once.Kills, split.Kills, "쪼개 밟았더니 처치가 달라졌다");
			Assert.AreEqual(once.Stage, split.Stage, "쪼개 밟았더니 구역이 달라졌다");
		}

		/// <summary>T2. 근접이 앞, 원거리가 뒤. 편성 순서와 무관 (원거리를 0번 자리에)</summary>
		[Test]
		public void Range_DecidesTheFrontLine()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Fresh(tuning, RANGED_DOLL, MELEE_DOLL);
			OneFoe(state, 12d);

			IdleBattleSim.Advance(state, tuning, 10d);

			Assert.Greater(state.Battle.X[1], state.Battle.X[0], "근접(자리 1)이 원거리(자리 0) 앞에 서지 않았다");
			Assert.AreEqual(1, IdleBattleSim.FrontSeat(state), "적이 노리는 맨 앞이 근접이 아니다");
		}

		/// <summary>T3. 사거리 밖이면 걷기, 안에 들면 정지</summary>
		[Test]
		public void OutOfRange_Walks_InRange_Stops()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Fresh(tuning, RANGED_DOLL);
			IdleFoe foe = OneFoe(state, 12d);
			foe.Speed = 0d;
			double start = state.Battle.X[0];

			IdleBattleSim.Advance(state, tuning, 0.1d);
			Assert.Greater(state.Battle.X[0], start, "사거리 밖인데 안 걸었다");

			IdleBattleSim.Advance(state, tuning, 5d);
			double settled = state.Battle.X[0];
			Assert.AreEqual(12d - 8d, settled, 0.3d, "사거리 8 에서 안 멈췄다");

			IdleBattleSim.Advance(state, tuning, 1d);
			Assert.AreEqual(settled, state.Battle.X[0], 1e-9d, "사거리 안인데 계속 걸었다");
		}

		/// <summary>T4. 적의 목표는 x 최대 인형만</summary>
		[Test]
		public void Foes_HitTheFrontmostOnly()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Fresh(tuning, RANGED_DOLL, MELEE_DOLL, MID_DOLL);
			state.Stage = 30;
			state.Battle.StageSeen = 30;
			OneFoe(state, 4d);

			double[] before = (double[])state.SeatHealth.Clone();
			IdleBattleSim.Advance(state, tuning, 3d);

			int front = 1;
			Assert.Less(state.SeatHealth[front], before[front], "맨 앞(근접)이 안 맞았다");
			Assert.AreEqual(before[0], state.SeatHealth[0], before[0] * 0.5d, "뒤(원거리)가 맞았다");
		}

		/// <summary>T5. 원거리 적은 거리 6 에서 정지 후 사격</summary>
		[Test]
		public void RangedFoe_StopsAtItsRange_AndShoots()
		{
			IdleTuning tuning = new IdleTuning();
			// 인형이 걸으면 간격이 인형 사거리로 축소. 적의 정지 거리만 보려고 인형 정지
			tuning.DollMoveSpeed = 0d;
			IdleState state = Fresh(tuning, MELEE_DOLL);
			state.Stage = 30;
			state.Battle.StageSeen = 30;
			IdleFoe foe = OneFoe(state, 14d, IdleFoeKind.Ranged);
			double before = state.SeatHealth[0];

			IdleBattleSim.Advance(state, tuning, 6d);

			double gap = foe.X - state.Battle.X[0];
			Assert.AreEqual(6d, gap, 0.6d, "원거리 적이 6 에서 안 멈췄다 (간격 " + gap + ")");
			Assert.Less(state.SeatHealth[0], before, "원거리 적이 사거리 안인데 안 쐈다");
		}

		/// <summary>T6. 10 처치면 구역이 오르고 전원 만렙</summary>
		[Test]
		public void TenKills_AdvanceTheStage()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Fresh(tuning, MELEE_DOLL);

			for (int guard = 0; guard < 600 && state.Stage == 1; guard++)
			{
				IdleModel.StepLive(state, tuning, 1d);
			}

			Assert.AreEqual(2, state.Stage, "10 처치 뒤 구역이 안 올랐다 (처치 " + state.Kills + ")");
			Assert.AreEqual(1, state.ClearedStage);
			Assert.AreEqual(IdleSquad.MaxHealthOf(state, tuning, 0), state.SeatHealth[0], 1e-6d, "구역을 깼는데 만렙이 아니다");
		}

		/// <summary>
		/// 웨이브 사이는 이음새. 원점을 되감지 않고 (OriginX 0), 인형은 걸어서만 (x 가 줄지 않음), 재배치 번호 그대로
		/// (사용자 2026-09-05, 09-08 두 번 지적. 구역 클리어와 전멸만 전환 뒤 Reset)
		/// </summary>
		[Test]
		public void Waves_JoinSeamlessly_NoRewind_NoJump()
		{
			IdleTuning tuning = new IdleTuning();
            // 원거리의 빠른 처치로 구역이 바뀌지 않도록 클리어 조건 분리
			tuning.KillsPerStage = 100000;
			IdleState state = Fresh(tuning, MELEE_DOLL, RANGED_DOLL, MID_DOLL);
			long epoch = state.Battle.Epoch;
			double[] last = (double[])state.Battle.X.Clone();
			int wavesSeen = state.Battle.Wave;

			for (int tick = 0; tick < 1200; tick++)
			{
				IdleBattleSim.Advance(state, tuning, 0.1d);
				for (int seat = 0; seat < IdleSquad.SEAT_COUNT; seat++)
				{
					if (IdleSquad.Standing(state, seat))
					{
						Assert.GreaterOrEqual(state.Battle.X[seat] + 1e-9d, last[seat], "자리 " + seat + " 이 뒤로 갔다 (틱 " + tick + ", 구역 " + state.Stage + ", 재배치 " + state.Battle.Epoch + ", 원점 " + state.Battle.OriginX + ", 웨이브 " + state.Battle.Wave + ", 반복 " + state.Repeating + ", 체력 " + state.SeatHealth[seat] + ")");
					}
					last[seat] = state.Battle.X[seat];
				}
			}

			Assert.Greater(state.Battle.Wave, wavesSeen + 1, "웨이브가 두 번 이상 바뀌어야 시험이 뜻이 있다");
			Assert.AreEqual(0d, state.Battle.OriginX, 1e-9d, "웨이브 사이에 원점을 되감았다");
			Assert.AreEqual(epoch, state.Battle.Epoch, "웨이브 사이에 재배치가 났다");
		}

		/// <summary>아주 멀리 가면 (BattleRebaseDistance) 그때만 되감음. 재배치 번호는 그대로 (무대가 같은 프레임에 옮김)</summary>
		[Test]
		public void Rewind_OnlyBeyondTheRebaseDistance()
		{
			IdleTuning tuning = new IdleTuning();
			tuning.KillsPerStage = 100000;
			tuning.BattleRebaseDistance = 30d;
			IdleState state = Fresh(tuning, MELEE_DOLL);
			long epoch = state.Battle.Epoch;

			for (int tick = 0; tick < 3000 && state.Battle.OriginX <= 0d; tick++)
			{
				IdleBattleSim.Advance(state, tuning, 0.1d);
			}

			Assert.Greater(state.Battle.OriginX, 0d, "30m 를 넘게 갔는데 안 되감았다 (x " + state.Battle.X[0] + ", 웨이브 " + state.Battle.Wave + ", 재배치 " + state.Battle.Epoch + ", 구역 " + state.Stage + ")");
			Assert.AreEqual(epoch, state.Battle.Epoch, "되감기가 재배치로 셌다");
			Assert.Less(state.Battle.X[0], 30d, "되감은 뒤 좌표가 작아지지 않았다");
		}

		/// <summary>구역 클리어는 Reset. 재배치 번호가 오름 (무대가 전환 막 뒤에 자리를 바꿈)</summary>
		[Test]
		public void StageClear_Resets_WithANewEpoch()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Fresh(tuning, MELEE_DOLL, RANGED_DOLL, MID_DOLL);
			long epoch = state.Battle.Epoch;
			int stage = state.Stage;

			for (int tick = 0; tick < 3000 && state.Stage == stage; tick++)
			{
				IdleBattleSim.Advance(state, tuning, 0.1d);
			}

			Assert.Greater(state.Stage, stage, "300초 안에 구역을 못 넘겼다");
			Assert.AreEqual(epoch + 1L, state.Battle.Epoch, "구역 클리어가 재배치 한 번이 아니다");
		}

		/// <summary>T7. 전멸이면 물러나 반복</summary>
		[Test]
		public void Wipe_FallsBack()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Fresh(tuning, MELEE_DOLL);
			state.Stage = 40;
			state.BestStage = 40;
			state.ClearedStage = 39;
			state.Battle.StageSeen = 40;

			IdleModel.StepLive(state, tuning, 30d);

			Assert.IsTrue(state.Repeating, "전멸했는데 반복이 아니다");
			Assert.AreEqual(39, state.Stage, "클리어한 구역으로 안 물러났다");
		}

		/// <summary>T8. 한 시간을 0.1s 틱으로. 200ms 미만</summary>
		[Test]
		public void OneHour_UnderTwoHundredMilliseconds()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Fresh(tuning, MELEE_DOLL, RANGED_DOLL, MID_DOLL);

			Stopwatch clock = Stopwatch.StartNew();
			for (int minute = 0; minute < 60; minute++)
			{
				IdleModel.StepLive(state, tuning, 60d);
			}
			clock.Stop();

			Assert.Less(clock.ElapsedMilliseconds, 200L, "한 시간 시뮬이 " + clock.ElapsedMilliseconds + "ms");
		}

		/// <summary>T9. 실측이 있으면 오프라인 처치는 초당 처치 x 시간 x 오프라인 몫</summary>
		[Test]
		public void Away_WithMeasurement_IsLinear()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Fresh(tuning, MELEE_DOLL);
			state.MeasuredStage = 1;
			state.MeasuredKillsPerSecond = 0.5d;
			long before = state.Kills;

			IdleModel.StepAway(state, tuning, 3600d);

			Assert.AreEqual(before + (long)(0.5d * 3600d * tuning.OfflineKillShare), state.Kills, "오프라인 처치가 선형이 아니다");
			Assert.AreEqual(1, state.Stage, "자는 동안 구역이 움직였다");
		}

		/// <summary>T10. 실측이 없으면 옛 수식 그대로</summary>
		[Test]
		public void Away_WithoutMeasurement_UsesTheOldFormula()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState away = Fresh(tuning, MELEE_DOLL);
			IdleState old = Fresh(tuning, MELEE_DOLL);

			IdleModel.StepAway(away, tuning, 600d);
			IdleModel.Step(old, tuning, 600d);

			Assert.AreEqual(old.Kills, away.Kills, "실측 없는 정산이 옛 수식과 다르다");
			Assert.AreEqual(old.Stage, away.Stage);
		}

		/// <summary>라이브 60s 뒤 실측 생성</summary>
		[Test]
		public void Live_ProducesAMeasurement()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Fresh(tuning, MELEE_DOLL);
			state.HoldingStage = true;

			IdleModel.StepLive(state, tuning, 61d);

			Assert.AreEqual(1, state.MeasuredStage, "실측 구역이 안 잡혔다");
			Assert.Greater(state.MeasuredKillsPerSecond, 0d, "초당 처치가 0 이다");
		}

		/// <summary>사진에 위치와 타격 포함</summary>
		[Test]
		public void Snapshot_CarriesPositionsAndHits()
		{
			IdleTuning tuning = new IdleTuning();
			IdleSession session = new IdleSession(tuning);

			session.AdvanceLive(5d);
			IdleSnapshot snapshot = session.Capture();

			Assert.AreEqual(IdleSquad.SEAT_COUNT, snapshot.Fighters.Length);
			Assert.Greater(snapshot.Foes.Length, 0, "적이 사진에 없다");
			Assert.Greater(snapshot.Fighters[0].Range, 0d, "시작 인형 사거리가 0");
		}
	}
}
