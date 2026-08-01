using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TD 진행 브레인 회귀 — 건설↔웨이브 전이 / false-clear 차단 / 경제(수입·차감) / 승패 확정·멱등.
	/// 순수 코어라 씬·물리·SO 0. TASK-WM-194 v0.
	/// </summary>
	public class TowerDefenseCoreTests
	{
		// 2파, 준비 1초, 시작자원 10, 기본수입 5, 채집당 +3, 1파 2마리 +1씩 증가.
		private static TowerDefenseRules Rules()
		{
			return new TowerDefenseRules
			{
				WaveCount = 2,
				PrepareSeconds = 1f,
				StartingResource = 10,
				BaseWaveIncome = 5,
				IncomePerHarvester = 3,
				FirstWaveEnemyCount = 2,
				EnemyCountGrowth = 1,
				BountyPerKill = 2,
			};
		}

		private static TowerDefenseCore Core()
		{
			return new TowerDefenseCore(Rules());
		}

		[Test]
		public void StartsInPrepare_WithStartingResource()
		{
			TowerDefenseCore core = Core();

			Assert.AreEqual(TowerDefensePhase.Prepare, core.Phase);
			Assert.AreEqual(TowerDefenseOutcome.InProgress, core.Outcome);
			Assert.AreEqual(10, core.Resource);
			Assert.AreEqual(0, core.WaveIndex);
		}

		[Test]
		public void PrepareElapsed_EmitsWaveStarted_WithScaledEnemyCount()
		{
			TowerDefenseCore core = Core();

			Assert.AreEqual(TowerDefenseSignal.None, core.Tick(0.5f, 0, true));
			Assert.AreEqual(TowerDefenseSignal.WaveStarted, core.Tick(0.5f, 0, true));
			Assert.AreEqual(TowerDefensePhase.Assault, core.Phase);
			Assert.AreEqual(2, core.CurrentWaveEnemyCount); // 1파 = FirstWaveEnemyCount
		}

		// 핵심 회귀: 스폰 확인 전 aliveEnemies==0 을 격퇴로 오인하면 웨이브가 통째 스킵된다.
		[Test]
		public void BeforeSpawnConfirmed_ZeroEnemies_DoesNotClearWave()
		{
			TowerDefenseCore core = Core();
			core.Tick(1f, 0, true); // WaveStarted

			Assert.AreEqual(TowerDefenseSignal.None, core.Tick(0.1f, 0, true));
			Assert.AreEqual(TowerDefenseSignal.None, core.Tick(0.1f, 0, true));
			Assert.AreEqual(TowerDefensePhase.Assault, core.Phase);
			Assert.AreEqual(0, core.WaveIndex);
		}

		[Test]
		public void WaveCleared_PaysIncome_AndReturnsToPrepare()
		{
			TowerDefenseCore core = Core();
			core.Tick(1f, 0, true); // WaveStarted
			core.ConfirmWaveSpawned();

			Assert.AreEqual(TowerDefenseSignal.None, core.Tick(0.1f, 2, true)); // 교전 중
			Assert.AreEqual(TowerDefenseSignal.WaveCleared, core.Tick(0.1f, 0, true));

			Assert.AreEqual(TowerDefensePhase.Prepare, core.Phase);
			Assert.AreEqual(1, core.WaveIndex);
			Assert.AreEqual(15, core.Resource); // 10 + BaseWaveIncome 5, 채집 0
		}

		// 개척 보상 — 채집건물을 지을수록 웨이브 정산 수입이 는다.
		[Test]
		public void Harvesters_IncreaseWaveIncome()
		{
			TowerDefenseCore core = Core();
			core.AddHarvester();
			core.AddHarvester();

			core.Tick(1f, 0, true);
			core.ConfirmWaveSpawned();
			core.Tick(0.1f, 1, true);
			core.Tick(0.1f, 0, true); // WaveCleared

			Assert.AreEqual(2, core.HarvesterCount);
			Assert.AreEqual(21, core.Resource); // 10 + (5 + 2*3)
		}

		[Test]
		public void EnemyCount_GrowsEachWave()
		{
			TowerDefenseCore core = Core();
			core.Tick(1f, 0, true);
			core.ConfirmWaveSpawned();
			core.Tick(0.1f, 0, true); // 1파 격퇴 → WaveIndex 1

			Assert.AreEqual(3, core.CurrentWaveEnemyCount); // 2 + 1*1
		}

		[Test]
		public void AllWavesCleared_Victory()
		{
			TowerDefenseCore core = Core();

			core.Tick(1f, 0, true);
			core.ConfirmWaveSpawned();
			core.Tick(0.1f, 0, true); // 1파 격퇴

			core.Tick(1f, 0, true);   // 2파 시작
			core.ConfirmWaveSpawned();
			Assert.AreEqual(TowerDefenseSignal.Victory, core.Tick(0.1f, 0, true));

			Assert.AreEqual(TowerDefenseOutcome.Victory, core.Outcome);
			Assert.AreEqual(TowerDefensePhase.Concluded, core.Phase);
		}

		[Test]
		public void CoreDestroyed_Defeat_EvenMidWave()
		{
			TowerDefenseCore core = Core();
			core.Tick(1f, 0, true);
			core.ConfirmWaveSpawned();

			Assert.AreEqual(TowerDefenseSignal.Defeat, core.Tick(0.1f, 3, false));
			Assert.AreEqual(TowerDefenseOutcome.Defeat, core.Outcome);
			Assert.AreEqual(TowerDefensePhase.Concluded, core.Phase);
		}

		[Test]
		public void Concluded_IsIdempotent_NoFurtherSignals()
		{
			TowerDefenseCore core = Core();
			core.Tick(1f, 0, true);
			core.ConfirmWaveSpawned();
			core.Tick(0.1f, 0, false); // Defeat

			Assert.AreEqual(TowerDefenseSignal.None, core.Tick(0.1f, 0, false));
			Assert.AreEqual(TowerDefenseSignal.None, core.Tick(0.1f, 5, true));
			Assert.AreEqual(TowerDefenseOutcome.Defeat, core.Outcome);
		}

		[Test]
		public void TrySpend_DeductsWhenAffordable_RejectsWhenNot()
		{
			TowerDefenseCore core = Core();

			Assert.IsTrue(core.TrySpend(4));
			Assert.AreEqual(6, core.Resource);

			Assert.IsFalse(core.TrySpend(7)); // 부족
			Assert.AreEqual(6, core.Resource); // 상태 무변경
		}

		// 엔드리스(WaveCount<=0) 회귀 — "고작 3웨이브" 유한 스테이지 거부, 격파 수가 곧 점수.
		private static TowerDefenseRules EndlessRules()
		{
			TowerDefenseRules rules = Rules();
			rules.WaveCount = 0; // 센티널 = 무한.
			return rules;
		}

		[Test]
		public void Endless_NeverVictory_KeepsCyclingWaves()
		{
			TowerDefenseCore core = new(EndlessRules());

			for (int waveNumber = 1; waveNumber <= 5; waveNumber++)
			{
				Assert.AreEqual(TowerDefenseSignal.WaveStarted, core.Tick(1f, 0, true));
				core.ConfirmWaveSpawned();
				Assert.AreEqual(TowerDefenseSignal.WaveCleared, core.Tick(0.1f, 0, true));

				Assert.AreEqual(TowerDefenseOutcome.InProgress, core.Outcome);
				Assert.AreEqual(TowerDefensePhase.Prepare, core.Phase);
				Assert.AreEqual(waveNumber, core.WaveIndex);
			}
		}

		[Test]
		public void Endless_CoreDestroyed_StillDefeat()
		{
			TowerDefenseCore core = new(EndlessRules());

			for (int waveNumber = 1; waveNumber <= 3; waveNumber++)
			{
				core.Tick(1f, 0, true); // WaveStarted
				core.ConfirmWaveSpawned();
				core.Tick(0.1f, 0, true); // WaveCleared
			}

			core.Tick(1f, 0, true); // 4파 시작(교전 중)
			core.ConfirmWaveSpawned();

			Assert.AreEqual(TowerDefenseSignal.Defeat, core.Tick(0.1f, 2, false));
			Assert.AreEqual(TowerDefenseOutcome.Defeat, core.Outcome);
			Assert.AreEqual(3, core.WaveIndex); // 격파한 파 수 = 점수, 패배해도 보존.
		}

		[Test]
		public void Finite_StillVictoryAtWaveCount()
		{
			// 기존 AllWavesCleared_Victory 와 동일 전제 — 유한 스테이지(WaveCount>0) 회귀 보존.
			TowerDefenseCore core = Core();

			core.Tick(1f, 0, true);
			core.ConfirmWaveSpawned();
			core.Tick(0.1f, 0, true); // 1파 격퇴

			core.Tick(1f, 0, true);   // 2파 시작
			core.ConfirmWaveSpawned();
			Assert.AreEqual(TowerDefenseSignal.Victory, core.Tick(0.1f, 0, true));

			Assert.AreEqual(TowerDefenseOutcome.Victory, core.Outcome);
			Assert.AreEqual(TowerDefensePhase.Concluded, core.Phase);
		}

		// === 웨이브 진행 방식(자동/수동) — TASK-WM-194 ===
		// 자동만 있으면 준비 시간을 시계가 뺏고, 수동만 있으면 리듬이 사라진다. 두 방식이 *규칙 층에서*
		// 갈라져 있는지 고정한다. 셸(UI/버튼)은 이 규칙을 부를 뿐이므로 여기가 정본.

		[Test]
		public void ManualMode_PrepareNeverTimesOut()
		{
			TowerDefenseCore core = Core();
			core.AutoAdvance = false;

			// 준비시간(1초)의 몇 배가 지나도 스스로 시작하지 않아야 한다.
			for (int i = 0; i < 20; i++)
				Assert.AreEqual(TowerDefenseSignal.None, core.Tick(0.5f, 0, true));

			Assert.AreEqual(TowerDefensePhase.Prepare, core.Phase);
			Assert.AreEqual(0, core.WaveIndex);
		}

		[Test]
		public void ManualMode_RequestNextWave_StartsWave()
		{
			TowerDefenseCore core = Core();
			core.AutoAdvance = false;
			core.Tick(5f, 0, true); // 시간은 아무 의미 없어야 한다.

			Assert.IsTrue(core.RequestNextWave());
			Assert.IsTrue(core.IsNextWaveRequested);

			Assert.AreEqual(TowerDefenseSignal.WaveStarted, core.Tick(0.1f, 0, true));
			Assert.AreEqual(TowerDefensePhase.Assault, core.Phase);
			Assert.IsFalse(core.IsNextWaveRequested); // 예약은 1회성 — 소비돼야 한다.
		}

		[Test]
		public void AutoMode_RequestNextWave_SkipsRemainingPrepare()
		{
			TowerDefenseCore core = Core();

			Assert.AreEqual(TowerDefenseSignal.None, core.Tick(0.1f, 0, true)); // 아직 준비 중.
			Assert.IsTrue(core.RequestNextWave());
			// 남은 준비 시간을 기다리지 않고 즉시 시작 — 기다림이 벌칙이 되지 않게.
			Assert.AreEqual(TowerDefenseSignal.WaveStarted, core.Tick(0.01f, 0, true));
		}

		[Test]
		public void RequestNextWave_RejectedOutsidePrepare()
		{
			TowerDefenseCore core = Core();
			core.Tick(1f, 0, true); // → Assault

			Assert.AreEqual(TowerDefensePhase.Assault, core.Phase);
			Assert.IsFalse(core.RequestNextWave()); // 교전 중 호출 불가.

			core.Tick(0.1f, 0, false); // 코어 파괴 → 종료.
			Assert.AreEqual(TowerDefenseOutcome.Defeat, core.Outcome);
			Assert.IsFalse(core.RequestNextWave()); // 끝난 뒤에도 불가.
		}

		[Test]
		public void ManualMode_StillDefeatsWhenCoreDies()
		{
			// 수동 진행이라고 패배 판정이 멈추면 안 된다(웨이브를 안 부르면 무적이 되는 구멍 차단).
			TowerDefenseCore core = Core();
			core.AutoAdvance = false;

			Assert.AreEqual(TowerDefenseSignal.Defeat, core.Tick(0.1f, 0, false));
			Assert.AreEqual(TowerDefenseOutcome.Defeat, core.Outcome);
		}

		[Test]
		public void AddResource_격파보상은_즉시_들어온다()
		{
			TowerDefenseCore core = Core();
			int before = core.Resource;

			core.AddResource(core.BountyPerKill);

			Assert.AreEqual(before + 2, core.Resource);
		}

		[Test]
		public void AddResource_음수는_무시된다()
		{
			TowerDefenseCore core = Core();
			int before = core.Resource;

			core.AddResource(-100);

			Assert.AreEqual(before, core.Resource, "자원이 조용히 줄어드는 경로를 만들면 안 된다.");
		}

		[Test]
		public void NextWaveIncome_채집인형마다_오른다()
		{
			TowerDefenseCore core = Core();
			Assert.AreEqual(5, core.NextWaveIncome);

			core.AddHarvester();
			Assert.AreEqual(8, core.NextWaveIncome);

			core.AddHarvester();
			Assert.AreEqual(11, core.NextWaveIncome, "화면이 이 숫자로 채집 인형의 역할을 설명한다.");
		}

	}
}
