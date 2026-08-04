using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TD 진행 브레인 회귀 — **실시간(RTS)** 규칙. 세 시계(큰 무리 / 정산 / 상시 마수)가 각자 돌고,
	/// 코어가 무너지면 그 자리에서 끝난다. 순수 코어라 씬·물리·SO 0. TASK-WM-194.
	///
	/// ★ 페이즈제(건설↔교전)는 폐기됐다(사용자 지시, 데아빌 지목). 옛 회귀 중 「스폰 확인 전 false-clear
	///   차단」처럼 *페이즈에만 존재하던* 것은 함께 사라졌다 — 실시간에는 「웨이브 격퇴」 판정 자체가 없다.
	/// </summary>
	public class TowerDefenseCoreTests
	{
		// 큰 무리 10초마다 / 정산 4초마다 / 상시 마수 1초마다. 시작자원 10, 기본수입 5, 채집당 +3.
		private static TowerDefenseRules Rules()
		{
			return new TowerDefenseRules
			{
				WaveCount = 0, // 실시간은 무한이 기본 — 버틴 시간이 곧 점수.
				StartingResource = 10,
				BaseWaveIncome = 5,
				IncomePerHarvester = 3,
				FirstWaveEnemyCount = 2,
				EnemyCountGrowth = 1,
				BountyPerKill = 2,
				WaveInterval = 10f,
				IncomeInterval = 4f,
				TrickleInterval = 1f,
			};
		}

		private static TowerDefenseCore Core()
		{
			return new TowerDefenseCore(Rules());
		}

		/// <summary> 신호가 나올 때까지 잘게 틱을 돌린다 — 실제 구동(초당 여러 틱)과 같은 모양. </summary>
		private static TowerDefenseSignal TickUntil(TowerDefenseCore core, TowerDefenseSignal wanted, float maxSeconds = 60f)
		{
			for (float elapsed = 0f; elapsed < maxSeconds; elapsed += 0.1f)
			{
				if (core.Tick(0.1f, true) == wanted)
					return wanted;
			}
			return TowerDefenseSignal.None;
		}

		[Test]
		public void 시작하자마자_진행중이고_시작자원을_갖는다()
		{
			TowerDefenseCore core = Core();

			Assert.AreEqual(TowerDefenseOutcome.InProgress, core.Outcome);
			Assert.AreEqual(10, core.Resource);
			Assert.AreEqual(0, core.WaveIndex);
		}

		[Test]
		public void 건설_대기_없이_바로_흐른다()
		{
			// 실시간의 전부 — 「건설 페이즈가 끝나기를 기다린다」가 없다. 시계가 곧 진행이다.
			TowerDefenseCore core = Core();

			core.Tick(0.5f, true);

			Assert.Greater(core.ElapsedSeconds, 0f);
			Assert.AreNotEqual(TowerDefensePhase.Prepare, core.Phase, "실시간에는 건설 국면이 없다.");
		}

		[Test]
		public void 상시_마수가_주기적으로_새어_나온다()
		{
			// 「웨이브 사이엔 안전하다」를 없애는 층 — 이게 없으면 실시간이라도 결국 웨이브 대기 게임이 된다.
			TowerDefenseCore core = Core();

			Assert.AreEqual(TowerDefenseSignal.TrickleDue, TickUntil(core, TowerDefenseSignal.TrickleDue, 3f));
		}

		[Test]
		public void 큰_무리는_시계가_부른다()
		{
			TowerDefenseCore core = Core();

			Assert.AreEqual(TowerDefenseSignal.WaveStarted, TickUntil(core, TowerDefenseSignal.WaveStarted, 12f));
			Assert.AreEqual(1, core.WaveIndex);
		}

		[Test]
		public void 큰_무리_전에는_웨이브가_안_오른다()
		{
			TowerDefenseCore core = Core();

			for (float elapsed = 0f; elapsed < 9f; elapsed += 0.1f)
				core.Tick(0.1f, true);

			Assert.AreEqual(0, core.WaveIndex, "주기 전에 무리가 오면 예고가 거짓말이 된다.");
		}

		[Test]
		public void 정산은_시계가_돈다()
		{
			// 페이즈제에서는 「웨이브를 격퇴해야」 벌었다 — 실시간에서 그 규칙이면 아무것도 안 들어온다.
			TowerDefenseCore core = Core();

			Assert.AreEqual(TowerDefenseSignal.IncomeDue, TickUntil(core, TowerDefenseSignal.IncomeDue, 6f));
			Assert.AreEqual(15, core.Resource); // 10 + 기본 5
		}

		[Test]
		public void 채집인형이_많을수록_정산이_크다()
		{
			TowerDefenseCore core = Core();
			core.SetHarvesterWeights(2f, 0f);

			TickUntil(core, TowerDefenseSignal.IncomeDue, 6f);

			Assert.AreEqual(21, core.Resource); // 10 + (5 + 2*3)
		}

		[Test]
		public void 지금_와라를_부르면_큰_무리가_앞당겨진다()
		{
			TowerDefenseCore core = Core();

			core.Tick(0.1f, true);
			Assert.IsTrue(core.RequestNextWave());
			Assert.AreEqual(TowerDefenseSignal.WaveStarted, core.Tick(0.1f, true));
			Assert.IsFalse(core.IsNextWaveRequested, "예약은 1회성 — 소비돼야 한다.");
		}

		[Test]
		public void 끝난_뒤에는_지금_와라도_안_먹는다()
		{
			TowerDefenseCore core = Core();
			core.Tick(0.1f, false); // 코어 파괴 → 종료

			Assert.AreEqual(TowerDefenseOutcome.Defeat, core.Outcome);
			Assert.IsFalse(core.RequestNextWave());
		}

		[Test]
		public void 마릿수는_웨이브마다_는다()
		{
			TowerDefenseCore core = Core();

			TickUntil(core, TowerDefenseSignal.WaveStarted, 12f);

			Assert.AreEqual(3, core.CurrentWaveEnemyCount); // 2 + 1*1
		}

		[Test]
		public void 코어가_무너지면_교전_중에도_즉시_패배()
		{
			TowerDefenseCore core = Core();
			core.Tick(1f, true);

			Assert.AreEqual(TowerDefenseSignal.Defeat, core.Tick(0.1f, false));
			Assert.AreEqual(TowerDefenseOutcome.Defeat, core.Outcome);
			Assert.AreEqual(TowerDefensePhase.Concluded, core.Phase);
		}

		[Test]
		public void 종료_후에는_아무_신호도_안_나온다()
		{
			TowerDefenseCore core = Core();
			core.Tick(0.1f, false); // Defeat

			Assert.AreEqual(TowerDefenseSignal.None, core.Tick(0.1f, false));
			Assert.AreEqual(TowerDefenseSignal.None, core.Tick(5f, true));
			Assert.AreEqual(TowerDefenseOutcome.Defeat, core.Outcome);
		}

		[Test]
		public void 버틴_웨이브_수는_패배해도_보존된다()
		{
			// 무한이라 승리가 없다 — 버틴 만큼이 곧 점수이므로 패배 시점에 지워지면 안 된다.
			TowerDefenseCore core = Core();
			TickUntil(core, TowerDefenseSignal.WaveStarted, 12f);
			TickUntil(core, TowerDefenseSignal.WaveStarted, 12f);

			core.Tick(0.1f, false);

			Assert.AreEqual(TowerDefenseOutcome.Defeat, core.Outcome);
			Assert.AreEqual(2, core.WaveIndex);
		}

		[Test]
		public void 다음_무리까지_남은_시간을_알려준다()
		{
			// 화면이 「곧 온다」를 말할 수 있는 유일한 숫자 — 없으면 실시간이 그냥 불안하기만 하다.
			TowerDefenseCore core = Core();

			core.Tick(4f, true);

			Assert.AreEqual(6f, core.NextWaveIn, 0.001f);
		}

		[Test]
		public void TrySpend_모자라면_거절하고_상태를_안_바꾼다()
		{
			TowerDefenseCore core = Core();

			Assert.IsTrue(core.TrySpend(4));
			Assert.AreEqual(6, core.Resource);

			Assert.IsFalse(core.TrySpend(7));
			Assert.AreEqual(6, core.Resource);
		}

		[Test]
		public void 격파보상은_즉시_들어온다()
		{
			TowerDefenseCore core = Core();
			int before = core.Resource;

			core.AddResource(core.BountyPerKill);

			Assert.AreEqual(before + 2, core.Resource);
		}

		[Test]
		public void 음수_지급은_무시된다()
		{
			TowerDefenseCore core = Core();
			int before = core.Resource;

			core.AddResource(-100);

			Assert.AreEqual(before, core.Resource, "자원이 조용히 줄어드는 경로를 만들면 안 된다.");
		}

		[Test]
		public void 정산_배수가_수입에_걸린다()
		{
			TowerDefenseCore core = Core();
			core.IncomeMultiplier = 2f;

			TickUntil(core, TowerDefenseSignal.IncomeDue, 6f);

			Assert.AreEqual(20, core.Resource); // 10 + 5*2
		}

		[Test]
		public void 목숨이_다하면_패배()
		{
			TowerDefenseRules rules = Rules();
			rules.StartingLives = 2;
			TowerDefenseCore core = new(rules);

			core.RegisterLeak();
			Assert.AreEqual(TowerDefenseOutcome.InProgress, core.Outcome);

			core.RegisterLeak();
			Assert.AreEqual(0, core.Lives);
			Assert.AreEqual(TowerDefenseOutcome.Defeat, core.Outcome);
		}

		[Test]
		public void 목숨으로_진_판은_신호가_아니라_결과로_남는다()
		{
			// ★ 목숨 소진 패배는 *신호를 내보내지 않는다* — 그래서 화면이 신호만 듣고 있으면 끝난 걸 영영
			//   모른다(실측: outcome=Defeat 인데 배너도 요약도 안 뜸). 셸이 결과를 직접 보게 만든 근거를
			//   여기에 못 박는다: 다음 틱은 None 이지만 결과는 남아 있어야 한다.
			TowerDefenseRules rules = Rules();
			rules.StartingLives = 1;
			TowerDefenseCore core = new(rules);

			core.RegisterLeak();

			Assert.AreEqual(TowerDefenseSignal.None, core.Tick(1f, true));
			Assert.AreEqual(TowerDefenseOutcome.Defeat, core.Outcome);
		}

		[Test]
		public void 목숨_추가는_유출제일_때만_먹는다()
		{
			TowerDefenseCore withoutLives = Core();
			withoutLives.AddLives(3);
			Assert.AreEqual(0, withoutLives.Lives);

			TowerDefenseRules rules = Rules();
			rules.StartingLives = 5;
			TowerDefenseCore withLives = new(rules);
			withLives.AddLives(3);
			Assert.AreEqual(8, withLives.Lives);
		}

		[Test]
		public void 정수는_바깥_채집_가중치에서_나온다()
		{
			TowerDefenseRules rules = Rules();
			rules.EssencePerHarvester = 4;
			TowerDefenseCore core = new(rules);

			core.SetHarvesterWeights(0f, 2f);

			Assert.AreEqual(8, core.NextWaveEssence);
		}

		[Test]
		public void 수동이면_시계가_큰_무리를_안_부른다()
		{
			// ★ 이 값을 규칙이 안 읽고 있어서 화면의 「진행: 자동/수동」 토글이 글자만 바뀌고
			//   판은 똑같이 흘렀다 — 스위치가 거짓말을 하고 있었다.
			TowerDefenseCore core = Core();
			core.AutoAdvance = false;

			for (float elapsed = 0f; elapsed < 30f; elapsed += 0.1f)
				core.Tick(0.1f, true);

			Assert.AreEqual(0, core.WaveIndex, "수동인데 시계가 무리를 불렀다.");
		}

		[Test]
		public void 수동이어도_지금_와라는_먹는다()
		{
			// 수동의 뜻은 「안 온다」가 아니라 「내가 부른다」다.
			TowerDefenseCore core = Core();
			core.AutoAdvance = false;
			core.Tick(0.1f, true);

			Assert.IsTrue(core.RequestNextWave());
			Assert.AreEqual(TowerDefenseSignal.WaveStarted, core.Tick(0.1f, true));
			Assert.AreEqual(1, core.WaveIndex);
		}

		[Test]
		public void 수동이어도_상시_마수는_계속_샌다()
		{
			// ★ 안 그러면 「수동」이 곧 「안전」이 되어 부르지 않는 것이 최적해가 된다 — 그건 정지지 선택이 아니다.
			TowerDefenseCore core = Core();
			core.AutoAdvance = false;

			Assert.AreEqual(TowerDefenseSignal.TrickleDue, TickUntil(core, TowerDefenseSignal.TrickleDue, 3f));
		}
	}
}
