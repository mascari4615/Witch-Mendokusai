using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 던전 안 판 v2 (changes/idle-dungeon-run, economy.md 4''')
	///
	/// ★ 지키는 것: 입장권 한 장에 판 하나, 던전은 본판 위 두 번째 전장 (본판은 계속 돌고 체력은 따로),
	///   칸은 난이도 x 스테이지 (앞 칸을 깨야 다음), 시간과 보스와 웨이브가 끝을 정함, 전멸과 나가기는 얻은 것만,
	///   소탕은 깬 칸만, 규칙 값은 튜닝이 줌.
	///   끝난 판은 던전 안에서 멈춰 결과를 보이고 (Finished), 나가기를 눌러야 본판 (사용자 2026-09-20)
	/// </summary>
	public sealed class IdleDungeonRunTests
	{
		private static IdleState Ready(IdleTuning tuning)
		{
			IdleState state = new IdleState();
			IdleHeroes.EnsureStarter(state);
			state.EnsureSeatRoom(tuning);
			IdleDungeons.Refill(state, tuning, 0L);
			return state;
		}

		/// <summary>칸을 깬 것으로 표시. 해금 시험의 발판</summary>
		private static void MarkCleared(IdleState state, IdleDungeonKind kind, int difficulty, int stage)
		{
			state.DungeonCleared |= 1L << IdleDungeons.CellIndexOf(kind, difficulty, stage);
		}

		[Test]
		public void Enter_SpendsOneTicket_AndStartsTheRun_OnItsOwnArena()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Ready(tuning);
			long before = IdleDungeons.TicketsOf(state, IdleDungeonKind.Gold);
			IdleDungeonStageSpec cell = IdleDungeons.CellOf(tuning, IdleDungeonKind.Gold, 0, 0);
			bool mainReady = state.Battle.Ready;

			Assert.IsTrue(IdleDungeons.TryStart(state, tuning, IdleDungeonKind.Gold, 0, 0));

			Assert.IsTrue(state.Dungeon.Active);
			Assert.AreEqual(IdleDungeonKind.Gold, state.Dungeon.Kind);
			Assert.AreEqual(cell.TimeLimitSeconds, state.Dungeon.SecondsLeft, 1e-9d);
			Assert.AreEqual(before - 1L, IdleDungeons.TicketsOf(state, IdleDungeonKind.Gold));
			Assert.AreEqual(mainReady, state.Battle.Ready, "던전 입장이 본판 전장을 건드렸다");
			Assert.Greater(state.Dungeon.SeatHealth[0], 0d, "던전 체력이 안 채워졌다");
			Assert.IsTrue(state.ActiveArena.Dungeon);
		}

		/// <summary>★ 던전 안에서 던전을 못 감. 입장권도 안 씀</summary>
		[Test]
		public void Enter_WhileRunning_IsRefused()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Ready(tuning);
			Assert.IsTrue(IdleDungeons.TryStart(state, tuning, IdleDungeonKind.Gold, 0, 0));
			long before = IdleDungeons.TicketsOf(state, IdleDungeonKind.Boss);

			Assert.IsFalse(IdleDungeons.TryStart(state, tuning, IdleDungeonKind.Boss, 0, 0));
			Assert.AreEqual(before, IdleDungeons.TicketsOf(state, IdleDungeonKind.Boss));
			Assert.AreEqual(IdleDungeonKind.Gold, state.Dungeon.Kind);
		}

		/// <summary>★ 앞 스테이지를 깨야 다음, 앞 난이도의 마지막을 깨야 다음 난이도 (refs/dungeons.md 2)</summary>
		[Test]
		public void Cells_UnlockInOrder()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Ready(tuning);

			Assert.IsTrue(IdleDungeons.IsUnlocked(state, tuning, IdleDungeonKind.Gold, 0, 0));
			Assert.IsFalse(IdleDungeons.IsUnlocked(state, tuning, IdleDungeonKind.Gold, 0, 1));
			Assert.IsFalse(IdleDungeons.IsUnlocked(state, tuning, IdleDungeonKind.Gold, 1, 0));
			Assert.IsFalse(IdleDungeons.TryStart(state, tuning, IdleDungeonKind.Gold, 0, 1), "안 열린 칸에 들어갔다");

			MarkCleared(state, IdleDungeonKind.Gold, 0, 0);
			Assert.IsTrue(IdleDungeons.IsUnlocked(state, tuning, IdleDungeonKind.Gold, 0, 1));
			Assert.IsFalse(IdleDungeons.IsUnlocked(state, tuning, IdleDungeonKind.Gold, 1, 0));

			for (int stage = 1; stage < IdleDungeons.STAGE_COUNT; stage++)
			{
				MarkCleared(state, IdleDungeonKind.Gold, 0, stage);
			}

			Assert.IsTrue(IdleDungeons.IsUnlocked(state, tuning, IdleDungeonKind.Gold, 1, 0), "앞 난이도를 다 깼는데 다음 난이도가 안 열렸다");
			Assert.IsFalse(IdleDungeons.IsUnlocked(state, tuning, IdleDungeonKind.Gold, 1, 1));
			Assert.IsFalse(IdleDungeons.IsUnlocked(state, tuning, IdleDungeonKind.Boss, 0, 1), "다른 던전이 같이 열렸다");
		}

		[Test]
		public void GoldDungeon_PaysPerKill_AndClearsWhenTimeRunsOut()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Ready(tuning);
			IdleDungeonStageSpec cell = IdleDungeons.CellOf(tuning, IdleDungeonKind.Gold, 0, 0);
			Assert.IsTrue(IdleDungeons.TryStart(state, tuning, IdleDungeonKind.Gold, 0, 0));
			double perKill = IdleModel.IncomePerSecond(state, tuning) * cell.GoldSecondsPerKill;

			Assert.IsFalse(IdleDungeons.OnKill(state, tuning, false), "처치 하나로 재화 던전이 끝났다");
			Assert.IsFalse(IdleDungeons.OnKill(state, tuning, false));
			Assert.AreEqual(2d * perKill, state.Resource, 1e-6d);
			Assert.AreEqual(2d * perKill, state.Dungeon.Gold, 1e-6d);

			Assert.IsFalse(IdleDungeons.TickRun(state, tuning, cell.TimeLimitSeconds * 0.5d));
			Assert.IsTrue(IdleDungeons.TickRun(state, tuning, cell.TimeLimitSeconds * 0.5d), "시간이 다 됐는데 안 끝났다");

			Assert.IsTrue(state.Dungeon.Active, "끝난 판이 결과도 안 보이고 사라졌다");
			Assert.IsTrue(state.Dungeon.Finished);
			Assert.IsTrue(state.Dungeon.Cleared);
			Assert.IsTrue(state.LastDungeonResult.Cleared, "재화 던전은 시간을 버티면 클리어");
			Assert.AreEqual(2L, state.LastDungeonResult.Kills);
			Assert.AreEqual(2d * perKill, state.LastDungeonResult.Gold, 1e-6d);
			Assert.AreEqual(cell.TimeLimitSeconds, state.LastDungeonResult.SecondsSpent, 1e-6d);
			Assert.AreEqual(1L, state.DungeonResultSequence);
			Assert.IsTrue(IdleDungeons.IsCleared(state, IdleDungeonKind.Gold, 0, 0));
			Assert.IsTrue(IdleDungeons.IsUnlocked(state, tuning, IdleDungeonKind.Gold, 0, 1), "깼는데 다음 칸이 안 열렸다");
		}

		[Test]
		public void BossDungeon_KillingTheBoss_EndsWithShardsAndGear()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Ready(tuning);
			IdleDungeonStageSpec cell = IdleDungeons.CellOf(tuning, IdleDungeonKind.Boss, 0, 0);
			Assert.IsTrue(IdleDungeons.TryStart(state, tuning, IdleDungeonKind.Boss, 0, 0));

			Assert.IsTrue(IdleDungeons.OnKill(state, tuning, true), "보스를 잡았는데 안 끝났다");

			Assert.AreEqual(cell.Shards, state.PrestigeShards);
			Assert.AreEqual(cell.GearCount, (long)state.Bag.Count);
			Assert.IsTrue(state.LastDungeonResult.Cleared);
			Assert.AreEqual(cell.Shards, state.LastDungeonResult.Shards);
			Assert.AreEqual((int)cell.GearCount, state.LastDungeonResult.Gear);
			Assert.AreEqual(0d, state.LastDungeonResult.Gold, 1e-9d, "보스 던전이 골드를 줬다");
			Assert.IsTrue(IdleDungeons.IsCleared(state, IdleDungeonKind.Boss, 0, 0));
		}

		/// <summary>
		/// ★ 끝난 판은 던전 안에 멈춰 있다 (사용자 2026-09-20 "던전 안에서 결과 보여주고 클릭하면 그때 나온다").
		///   시계는 결과까지의 초만 세고, 결과는 RESULT_DELAY 뒤에, 나가기가 판을 지운다
		/// </summary>
		[Test]
		public void FinishedRun_StaysInTheDungeon_UntilLeave()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Ready(tuning);
			Assert.IsTrue(IdleDungeons.TryStart(state, tuning, IdleDungeonKind.Boss, 0, 0));
			Assert.IsTrue(IdleDungeons.OnKill(state, tuning, true));

			Assert.IsTrue(state.Dungeon.Active, "보스를 잡자마자 본판으로 튕겼다");
			Assert.IsTrue(state.Dungeon.Finished);
			Assert.IsTrue(state.Dungeon.Cleared);
			Assert.IsTrue(state.ActiveArena.Dungeon, "결과 보는 동안 보는 전장이 본판이다");
			Assert.AreEqual(0, state.Dungeon.Battle.Foes.Count);
			Assert.IsFalse(IdleDungeons.Dismiss(new IdleState()), "판이 없는데 나가졌다");

			Assert.IsTrue(IdleDungeons.TickRun(state, tuning, IdleDungeons.RESULT_DELAY_SECONDS * 0.5d), "끝난 판의 시계가 돈다");
			Assert.AreEqual(IdleDungeons.RESULT_DELAY_SECONDS * 0.5d, state.Dungeon.SecondsSinceFinish, 1e-9d);
			Assert.IsTrue(IdleDungeons.OnKill(state, tuning, true) == false && state.DungeonResultSequence == 1L, "끝난 판에서 처치가 결과를 또 냈다");
			IdleDungeons.EndRun(state, tuning, false);
			Assert.IsTrue(state.Dungeon.Cleared, "끝난 판을 다시 끝내니 결과가 뒤집혔다");

			IdleBattleSim.Advance(state, tuning, 1d);
			Assert.AreEqual(0, state.Dungeon.Battle.Foes.Count, "멈춘 전장에 적이 또 섰다");
			Assert.GreaterOrEqual(state.Dungeon.SecondsSinceFinish, IdleDungeons.RESULT_DELAY_SECONDS);

			Assert.IsTrue(IdleDungeons.Leave(state, tuning));
			Assert.IsFalse(state.Dungeon.Active);
			Assert.IsFalse(state.Dungeon.Finished);
			Assert.IsTrue(IdleDungeons.IsCleared(state, IdleDungeonKind.Boss, 0, 0));
		}

		/// <summary>★ 보스를 못 잡고 시간이 끝나면 실패. 조각 없음, 소탕 안 열림</summary>
		[Test]
		public void BossDungeon_TimeOut_IsNotAClear()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Ready(tuning);
			IdleDungeonStageSpec cell = IdleDungeons.CellOf(tuning, IdleDungeonKind.Boss, 0, 0);
			Assert.IsTrue(IdleDungeons.TryStart(state, tuning, IdleDungeonKind.Boss, 0, 0));

			Assert.IsTrue(IdleDungeons.TickRun(state, tuning, cell.TimeLimitSeconds + 1d));

			Assert.IsFalse(state.LastDungeonResult.Cleared);
			Assert.AreEqual(0L, state.PrestigeShards);
			Assert.IsFalse(IdleDungeons.IsCleared(state, IdleDungeonKind.Boss, 0, 0));
		}

		[Test]
		public void GearDungeon_GivesGearPerWave_AndClearsAtTheLastWave()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Ready(tuning);
			IdleDungeonStageSpec cell = IdleDungeons.CellOf(tuning, IdleDungeonKind.Gear, 0, 0);
			Assert.Greater(cell.Waves, 1, "장비 던전 웨이브가 하나뿐이면 시험이 뜻이 없다");
			Assert.IsTrue(IdleDungeons.TryStart(state, tuning, IdleDungeonKind.Gear, 0, 0));

			for (int wave = 1; wave < cell.Waves; wave++)
			{
				Assert.IsFalse(IdleDungeons.OnWaveCleared(state, tuning), "마지막 웨이브 전에 끝났다 (" + wave + ")");
				Assert.AreEqual(cell.GearCount * wave, (long)state.Bag.Count);
			}

			Assert.IsTrue(IdleDungeons.OnWaveCleared(state, tuning), "마지막 웨이브를 밀었는데 안 끝났다");
			Assert.AreEqual(cell.GearCount * cell.Waves, (long)state.Bag.Count);
			Assert.IsTrue(state.LastDungeonResult.Cleared);
			Assert.AreEqual(cell.Waves, state.LastDungeonResult.WavesCleared);
		}

		[Test]
		public void GearDungeon_FillsTheBag_ButNotPastIt()
		{
			IdleTuning tuning = new IdleTuning();
			tuning.Dungeons = IdleDungeonSpec.Defaults();
			tuning.Dungeons[(int)IdleDungeonKind.Gear].CellOf(0, 0).GearCount = 500L;
			IdleState state = Ready(tuning);
			int room = IdleShop.BagCapacityOf(state, tuning);
			Assert.IsTrue(IdleDungeons.TryStart(state, tuning, IdleDungeonKind.Gear, 0, 0));

			IdleDungeons.OnWaveCleared(state, tuning);

			Assert.AreEqual(room, state.Bag.Count, "가방보다 많이 들어갔다");
			Assert.AreEqual(room, state.Dungeon.Gear, "결과의 장비 수는 실제로 들어간 수");
		}

		/// <summary>★ 스킬 재료가 아직 없어 스킬 던전은 닫혀 있다. 입장권도 안 쓴다</summary>
		[Test]
		public void SkillDungeon_IsClosed()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Ready(tuning);
			long before = IdleDungeons.TicketsOf(state, IdleDungeonKind.Skill);

			Assert.IsFalse(IdleDungeons.IsOpen(tuning, IdleDungeonKind.Skill));
			Assert.IsFalse(IdleDungeons.IsUnlocked(state, tuning, IdleDungeonKind.Skill, 0, 0));
			Assert.IsFalse(IdleDungeons.TryStart(state, tuning, IdleDungeonKind.Skill, 0, 0));
			Assert.AreEqual(before, IdleDungeons.TicketsOf(state, IdleDungeonKind.Skill));
		}

		/// <summary>★ 규칙 값은 튜닝 (SO) 몫. 비어 있으면 코드 기본값. 칸이 짧으면 그만큼만</summary>
		[Test]
		public void Specs_ComeFromTuning_OrFallBackToDefaults()
		{
			IdleTuning tuning = new IdleTuning();
			tuning.Dungeons = new[]
			{
				new IdleDungeonSpec
				{
					Kind = IdleDungeonKind.Gold,
					Tiers = new[] { new IdleDungeonTier { Stages = new[] { new IdleDungeonStageSpec { TimeLimitSeconds = 7d } } } },
				},
			};
			IdleState state = Ready(tuning);

			Assert.IsTrue(IdleDungeons.TryStart(state, tuning, IdleDungeonKind.Gold, 0, 0));
			Assert.AreEqual(7d, state.Dungeon.SecondsLeft, 1e-9d);
			Assert.IsNull(IdleDungeons.CellOf(tuning, IdleDungeonKind.Gold, 0, 1), "표에 없는 칸이 생겼다");

			IdleDungeonStageSpec fallback = IdleDungeons.CellOf(tuning, IdleDungeonKind.Boss, 0, 0);
			Assert.AreEqual(IdleDungeonSpec.Defaults()[(int)IdleDungeonKind.Boss].CellOf(0, 0).Shards, fallback.Shards);

			tuning.Dungeons = null;
			Assert.AreEqual(IdleDungeonSpec.Defaults()[(int)IdleDungeonKind.Gold].CellOf(0, 0).TimeLimitSeconds,
				IdleDungeons.CellOf(tuning, IdleDungeonKind.Gold, 0, 0).TimeLimitSeconds, 1e-9d);
		}

		/// <summary>★ 칸의 적 세기는 본판 구역과 무관한 절대값 (칸의 Level). 난이도가 오르면 세진다</summary>
		[Test]
		public void Sim_FoeStrength_ComesFromTheCell_NotTheStage()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Ready(tuning);
			state.Stage = 50;
			IdleDungeonStageSpec cell = IdleDungeons.CellOf(tuning, IdleDungeonKind.Gold, 0, 0);
			Assert.IsTrue(IdleDungeons.TryStart(state, tuning, IdleDungeonKind.Gold, 0, 0));

			IdleBattleSim.Advance(state, tuning, 0.1d);

			IdleFoe foe = state.Dungeon.Battle.Foes[0];
			Assert.AreEqual(IdleModel.TargetHealthAt(cell.Level, tuning), foe.MaxHealth, 1e-6d, "던전 적 체력이 칸 구역 것이 아니다");
			Assert.Less(foe.MaxHealth, IdleModel.TargetHealthAt(state.Stage, tuning), "본판 구역 체력을 따라갔다");
		}

		/// <summary>★ 시뮬 안: 보스 던전은 보스 하나, 재화 던전은 잡몹 무리. 본판 구역 진행도와 무관</summary>
		[Test]
		public void Sim_DungeonWaves_IgnoreStageProgress()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState boss = Ready(tuning);
			Assert.IsTrue(IdleDungeons.TryStart(boss, tuning, IdleDungeonKind.Boss, 0, 0));
			IdleBattleSim.Advance(boss, tuning, 0.1d);
			Assert.AreEqual(1, boss.Dungeon.Battle.Foes.Count);
			Assert.IsTrue(boss.Dungeon.Battle.Foes[0].Boss, "보스 던전 첫 웨이브가 보스가 아니다");

			IdleState gold = Ready(tuning);
			gold.KillsInStage = tuning.KillsPerStage - 1;
			IdleDungeonStageSpec cell = IdleDungeons.CellOf(tuning, IdleDungeonKind.Gold, 0, 0);
			Assert.IsTrue(IdleDungeons.TryStart(gold, tuning, IdleDungeonKind.Gold, 0, 0));
			IdleBattleSim.Advance(gold, tuning, 0.1d);
			Assert.AreEqual(cell.WaveSize, gold.Dungeon.Battle.Foes.Count);
			Assert.IsFalse(gold.Dungeon.Battle.Foes[0].Boss, "재화 던전에 구역 보스가 나왔다");
			Assert.IsTrue(gold.Battle.Foes[0].Boss, "본판 전장의 보스 웨이브가 사라졌다");
		}

		/// <summary>★ 본판은 뒤에서 계속 돈다 (사용자 2026-09-08). 던전 판 동안 본판 구역이 오르고 던전 처치는 구역 셈에 안 들어간다</summary>
		[Test]
		public void Sim_MainStage_KeepsRunning_UnderTheDungeon()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState alone = Ready(tuning);
			IdleBattleSim.Advance(alone, tuning, 60d);

			IdleState under = Ready(tuning);
			Assert.IsTrue(IdleDungeons.TryStart(under, tuning, IdleDungeonKind.Gold, 0, 0));
			IdleBattleSim.Advance(under, tuning, 60d);

			Assert.Greater(alone.Stage, 1, "60초면 본판 구역이 올라야 시험이 뜻이 있다");
			Assert.AreEqual(alone.Stage, under.Stage, "던전 판이 본판 구역 진행을 바꿨다");
			Assert.AreEqual(alone.KillsInStage, under.KillsInStage, "던전 처치가 구역 처치로 셌다");
			Assert.AreEqual(alone.Battle.Wave, under.Battle.Wave, "본판 웨이브가 던전 때문에 달라졌다");
			Assert.Greater(under.Dungeon.Kills + under.LastDungeonResult.Kills, 0L, "던전 판에서 아무것도 안 잡았다");
			Assert.Greater(under.Kills, alone.Kills, "던전 처치가 총 처치에 안 들어갔다");
		}

		/// <summary>★ 던전 전멸은 본판 체력에 무관, 본판 구역도 안 물린다. 얻은 것만 들고 나감</summary>
		[Test]
		public void Sim_Wipe_EndsTheRun_KeepsLoot_LeavesTheMainBattleAlone()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Ready(tuning);
			state.Stage = 5;
			IdleBattleSim.Advance(state, tuning, 0.1d);
			double[] mainHealth = (double[])state.SeatHealth.Clone();
			Assert.IsTrue(IdleDungeons.TryStart(state, tuning, IdleDungeonKind.Gold, 0, 0));
			IdleDungeons.OnKill(state, tuning, false);
			double loot = state.Resource;
			Assert.Greater(loot, 0d);

			for (int seat = 0; seat < IdleSquad.SEAT_COUNT; seat++)
			{
				state.Dungeon.SeatHealth[seat] = 0d;
			}

			IdleBattleSim.Advance(state, tuning, 0.1d);

			Assert.IsTrue(state.Dungeon.Finished, "전멸했는데 판이 안 끝났다");
			Assert.IsFalse(state.Dungeon.Cleared);
			Assert.IsTrue(IdleDungeons.Leave(state, tuning), "결과를 보고 나가기가 안 됐다");
			Assert.IsFalse(state.Dungeon.Active);
			Assert.IsFalse(state.LastDungeonResult.Cleared);
			Assert.AreEqual(loot, state.LastDungeonResult.Gold, 1e-9d);
			Assert.AreEqual(loot, state.Resource, 1e-9d);
			Assert.AreEqual(5, state.Stage, "던전 전멸이 본판 구역을 물렸다");
			Assert.IsFalse(state.Repeating, "던전 전멸이 본판을 반복으로 돌렸다");
			for (int seat = 0; seat < IdleSquad.SEAT_COUNT; seat++)
			{
				Assert.AreEqual(mainHealth[seat], state.SeatHealth[seat], mainHealth[seat] * 0.5d + 1e-9d, "던전 전멸이 본판 체력을 건드렸다 (자리 " + seat + ")");
			}
		}

		/// <summary>★ 나가기는 얻은 것 들고 즉시, 판은 실패 (소탕 안 열림)</summary>
		[Test]
		public void Leave_KeepsLoot_CountsAsFailure()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Ready(tuning);
			Assert.IsFalse(IdleDungeons.Leave(state, tuning), "판이 없는데 나갔다");
			Assert.IsTrue(IdleDungeons.TryStart(state, tuning, IdleDungeonKind.Gold, 0, 0));
			IdleDungeons.OnKill(state, tuning, false);
			double loot = state.Resource;

			Assert.IsTrue(IdleDungeons.Leave(state, tuning));

			Assert.IsFalse(state.Dungeon.Active);
			Assert.IsFalse(state.LastDungeonResult.Cleared);
			Assert.AreEqual(loot, state.Resource, 1e-9d);
			Assert.IsFalse(IdleDungeons.IsCleared(state, IdleDungeonKind.Gold, 0, 0));
			Assert.IsFalse(state.ActiveArena.Dungeon, "나갔는데 보는 전장이 던전이다");
		}

		/// <summary>★ 소탕은 깬 칸만. 한 판은 그 칸 풀 클리어 몫</summary>
		[Test]
		public void Sweep_NeedsAClearedCell_ThenPaysFullClearsPerTicket()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Ready(tuning);
			Assert.IsFalse(IdleDungeons.TrySweep(state, tuning, IdleDungeonKind.Gold, 0, 0, out IdleDungeonReward none));
			Assert.AreEqual(0, none.Runs);
			Assert.AreEqual(tuning.TicketsPerDay, IdleDungeons.TicketsOf(state, IdleDungeonKind.Gold), "안 깬 칸 소탕이 입장권을 썼다");

			Assert.IsTrue(IdleDungeons.TryStart(state, tuning, IdleDungeonKind.Gold, 0, 0));
			IdleDungeonStageSpec cell = IdleDungeons.CellOf(tuning, IdleDungeonKind.Gold, 0, 0);
			IdleDungeons.TickRun(state, tuning, cell.TimeLimitSeconds);
			Assert.IsTrue(IdleDungeons.Dismiss(state), "시간이 다 된 판의 결과를 못 닫았다");
			double before = state.Resource;
			double perRun = IdleDungeons.SweepGoldOf(state, tuning, IdleDungeonKind.Gold, cell);

			Assert.IsFalse(IdleDungeons.TrySweep(state, tuning, IdleDungeonKind.Gold, 0, 1, out IdleDungeonReward next), "안 깬 옆 칸이 소탕됐다");
			Assert.AreEqual(0, next.Runs);
			Assert.IsTrue(IdleDungeons.TrySweep(state, tuning, IdleDungeonKind.Gold, 0, 0, out IdleDungeonReward all));

			Assert.AreEqual(tuning.TicketsPerDay - 1L, (long)all.Runs);
			Assert.AreEqual(perRun * all.Runs, all.Gold, 1e-6d);
			Assert.AreEqual(before + all.Gold, state.Resource, 1e-6d);
			Assert.AreEqual(0L, IdleDungeons.TicketsOf(state, IdleDungeonKind.Gold));
			Assert.IsFalse(state.Dungeon.Active, "소탕이 판을 열었다");
		}

		[Test]
		public void Sweep_WhileRunning_OrWithNoTickets_DoesNothing()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Ready(tuning);
			MarkCleared(state, IdleDungeonKind.Boss, 0, 0);

			Assert.IsTrue(IdleDungeons.TryStart(state, tuning, IdleDungeonKind.Boss, 0, 0));
			Assert.IsFalse(IdleDungeons.TrySweep(state, tuning, IdleDungeonKind.Boss, 0, 0, out IdleDungeonReward _), "판 안에서 소탕이 됐다");
			IdleDungeons.EndRun(state, tuning, false);
			Assert.IsFalse(IdleDungeons.TrySweep(state, tuning, IdleDungeonKind.Boss, 0, 0, out IdleDungeonReward _), "결과 보는 중에 소탕이 됐다");
			Assert.IsTrue(IdleDungeons.Dismiss(state));

			Assert.IsTrue(IdleDungeons.TrySweep(state, tuning, IdleDungeonKind.Boss, 0, 0, out IdleDungeonReward all));
			Assert.AreEqual(tuning.TicketsPerDay - 1L, (long)all.Runs);
			Assert.IsFalse(IdleDungeons.TrySweep(state, tuning, IdleDungeonKind.Boss, 0, 0, out IdleDungeonReward again));
			Assert.AreEqual(0, again.Runs);
		}

		/// <summary>★ 깬 칸 기록은 저장에 남고 (60 비트) 판은 안 남음</summary>
		[Test]
		public void Save_KeepsClearedCells_DropsTheRun()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Ready(tuning);
			MarkCleared(state, IdleDungeonKind.Gear, 2, 4);
			Assert.IsTrue(IdleDungeons.TryStart(state, tuning, IdleDungeonKind.Gold, 0, 0));
			IdleDungeons.TickRun(state, tuning, 1e9d);
			Assert.IsFalse(IdleDungeons.TryStart(state, tuning, IdleDungeonKind.Boss, 0, 0), "결과 보는 중에 다른 판이 열렸다");
			Assert.IsTrue(IdleDungeons.Dismiss(state));
			Assert.IsTrue(IdleDungeons.TryStart(state, tuning, IdleDungeonKind.Boss, 0, 0));

			IdleState loaded = new IdleState();
			loaded.Load(state.Save());

			Assert.IsTrue(IdleDungeons.IsCleared(loaded, IdleDungeonKind.Gold, 0, 0));
			Assert.IsTrue(IdleDungeons.IsCleared(loaded, IdleDungeonKind.Gear, 2, 4), "마지막 비트 (59) 가 왕복에서 사라졌다");
			Assert.IsFalse(IdleDungeons.IsCleared(loaded, IdleDungeonKind.Boss, 0, 0));
			Assert.IsFalse(loaded.Dungeon.Active);
		}

		/// <summary>★ 자리를 비우면 판은 그 자리에서 끝. 사진에 결과와 칸 60개가 실림</summary>
		[Test]
		public void Session_CatchUp_EndsTheRun_AndTheSnapshotCarriesCellsAndResult()
		{
			IdleTuning tuning = new IdleTuning();
			IdleSession session = new IdleSession(tuning);
			const long NOON = 1_700_000_000L;
			session.CatchUp(NOON);
			Assert.IsTrue(session.TryEnterDungeon(IdleDungeonKind.Gold, 0, 0));
			Assert.IsTrue(session.Capture().DungeonRun.Active);

			session.CatchUp(NOON + 600L);
			IdleSnapshot snapshot = session.Capture();

			Assert.IsFalse(snapshot.DungeonRun.Active);
			Assert.AreEqual(1L, snapshot.DungeonResultSequence);
			Assert.IsFalse(snapshot.LastDungeonResult.Cleared);
			Assert.AreEqual(IdleDungeons.COUNT * IdleDungeons.DIFFICULTY_COUNT * IdleDungeons.STAGE_COUNT, snapshot.DungeonCells.Length);
			IdleDungeonCellView first = snapshot.DungeonCells[IdleDungeons.CellIndexOf(IdleDungeonKind.Gold, 0, 0)];
			Assert.IsTrue(first.Open && first.Unlocked && first.Cleared == false);
			Assert.IsFalse(snapshot.DungeonCells[IdleDungeons.CellIndexOf(IdleDungeonKind.Gold, 0, 1)].Unlocked);
			Assert.IsFalse(snapshot.DungeonCells[IdleDungeons.CellIndexOf(IdleDungeonKind.Skill, 0, 0)].Open);
		}
	}
}
