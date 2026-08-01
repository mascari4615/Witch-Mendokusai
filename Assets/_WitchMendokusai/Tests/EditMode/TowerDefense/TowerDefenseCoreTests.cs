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
	}
}
