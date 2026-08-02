using UnityEngine;
namespace WitchMendokusai
{
	/// <summary>
	/// 특수시공 개척(TD) 진행 브레인 — 건설↔웨이브 국면 전이 + 자원 경제 + 승패를 *단 한 번* 확정.
	/// 순수(MonoBehaviour/물리/이벤트버스 0) → EditMode 로 규칙 전량 검증. 셸(TowerDefenseMatch)은
	/// Tick 이 돌려주는 TowerDefenseSignal 로 스폰/UI/정리만 수행(규칙 판단 0).
	///
	/// 웨이브 클리어 판정은 ConfirmWaveSpawned() 로 스폰이 실제 확인된 뒤에만 활성 — 스폰 전
	/// aliveEnemies==0 을 "격퇴" 로 오인해 웨이브를 통째 건너뛰는 false-clear 차단(ArenaMatchCore 의
	/// "0틱 종료" 방어와 동형).
	///
	/// rules.IsEndless(WaveCount 이하 0) 면 Victory 가 없다 — 코어가 부서질 때까지 웨이브가 영원히
	/// 이어지고 격파한 WaveIndex 가 곧 점수(디폴트, "고작 N웨이브" 유한 스테이지 거부). WaveCount>0 인
	/// 유한 스테이지는 기존 그대로 해당 파 격퇴 시 Victory.
	/// </summary>
	public class TowerDefenseCore
	{
		private readonly TowerDefenseRules rules;

		private float prepareRemaining;
		private bool waveSpawnConfirmed;
		private bool nextWaveRequested;

		/// <summary>
		/// 건설 국면이 시간으로 자동 종료되는가(자동 진행) — false 면 <see cref="RequestNextWave"/> 를
		/// 받을 때까지 무한정 기다린다(수동 진행).
		///
		/// 두 방식은 성격이 다른 재미다: 자동은 압박·리듬, 수동은 「완벽히 준비하고 부른다」는 계획.
		/// 어느 하나만 두면 다른 쪽 플레이가 아예 불가능해지므로 규칙 층에서 갈라 놓는다
		/// (사용자 지시: "자동 진행이랑, 수동 진행 설정 가능하면 좋겠음").
		/// </summary>
		public bool AutoAdvance { get; set; } = true;

		/// <summary>
		/// 이 웨이브부터 자동 진행. 1 이면 첫 파도는 사람이 부를 때까지 오지 않는다 —
		/// 판이 매번 새로 생성되므로 시작하자마자 시계가 돌면 지형을 볼 시간이 없다(사용자 지시).
		/// </summary>
		public int FirstAutoWave { get; set; }

		/// <summary> 수동 진행에서 다음 웨이브가 예약됐는지 — HUD 가 「호출됨」 표시에 쓴다. </summary>
		public bool IsNextWaveRequested => nextWaveRequested;

		public TowerDefensePhase Phase { get; private set; } = TowerDefensePhase.Prepare;
		public TowerDefenseOutcome Outcome { get; private set; } = TowerDefenseOutcome.InProgress;

		/// <summary> 현재(또는 다음) 웨이브 번호 0-based. 승리 시 WaveCount 와 같아진다. </summary>
		public int WaveIndex { get; private set; }
		public int Resource { get; private set; }
		public int HarvesterCount { get; private set; }

		/// <summary> 남은 목숨. 유출제를 안 쓰는 스테이지(StartingLives<=0)면 항상 0 이고 무시된다. </summary>
		public int Lives { get; private set; }

		/// <summary> 이 스테이지가 유출제인가. </summary>
		public bool UsesLives => rules.StartingLives > 0;

		// 채집 인형들의 벌이 배수 합 — 마리수가 아니라 *어디에 세웠는지*가 수입을 만든다.
		private float harvesterIncomeWeight;
		public float PrepareRemaining => prepareRemaining;

		/// <summary> 이번 웨이브에 스폰될 적 수 — 셸이 WaveStarted 신호를 받고 읽는다. </summary>
		public int CurrentWaveEnemyCount => rules.EnemiesInWave(WaveIndex);

		public TowerDefenseCore(TowerDefenseRules rules)
		{
			this.rules = rules;
			Resource = rules.StartingResource;
			Lives = rules.StartingLives;
			prepareRemaining = rules.PrepareSeconds;
		}

		/// <summary>
		/// 한 틱 평가. coreAlive=false 면 즉시 패배. Prepare 는 시간이 다하면 WaveStarted,
		/// Assault 는 스폰 확인 후 aliveEnemies==0 이면 정산 → 다음 파 또는 승리.
		/// 전이가 *이번 호출에서 처음* 발생할 때만 해당 신호. 종료 후엔 항상 None. 멱등.
		/// </summary>
		public TowerDefenseSignal Tick(float deltaSeconds, int aliveEnemies, bool coreAlive)
		{
			if (Outcome != TowerDefenseOutcome.InProgress)
				return TowerDefenseSignal.None;

			if (coreAlive == false)
			{
// 유출제에서는 코어가 맞아 죽는 일이 없다(마수는 닿는 순간 사라진다) — 목숨 소진이 패배다.
								Outcome = TowerDefenseOutcome.Defeat;
				Phase = TowerDefensePhase.Concluded;
				return TowerDefenseSignal.Defeat;
			}

			if (Phase == TowerDefensePhase.Prepare)
			{
				if (AutoAdvance && WaveIndex >= FirstAutoWave)
					prepareRemaining -= deltaSeconds;

				// 호출(RequestNextWave)은 두 방식 모두에서 즉시 시작 — 자동에서도 "준비 끝났으니 지금 와라"가
				// 가능해야 기다리는 시간이 벌칙이 되지 않는다.
				bool timeUp = AutoAdvance && WaveIndex >= FirstAutoWave && prepareRemaining <= 0f;
				if (timeUp == false && nextWaveRequested == false)
					return TowerDefenseSignal.None;

				prepareRemaining = 0f;
				nextWaveRequested = false;
				Phase = TowerDefensePhase.Assault;
				waveSpawnConfirmed = false;
				return TowerDefenseSignal.WaveStarted;
			}

			if (Phase == TowerDefensePhase.Assault)
			{
				// 스폰 확인 전에는 클리어 판정 자체를 안 함 = false-clear 차단.
				if (waveSpawnConfirmed == false || aliveEnemies > 0)
					return TowerDefenseSignal.None;

				Resource += NextWaveIncome;
				WaveIndex++;

				// 엔드리스(IsEndless)는 이 분기에 절대 안 들어옴 — 무조건 다음 Prepare 로 순환.
				if (rules.IsEndless == false && WaveIndex >= rules.WaveCount)
				{
					Outcome = TowerDefenseOutcome.Victory;
					Phase = TowerDefensePhase.Concluded;
					return TowerDefenseSignal.Victory;
				}

				Phase = TowerDefensePhase.Prepare;
				prepareRemaining = rules.PrepareSeconds;
				return TowerDefenseSignal.WaveCleared;
			}

			return TowerDefenseSignal.None;
		}

		/// <summary>
		/// 다음 웨이브 호출 — 건설 국면에서만 성립. 진행 중이 아니거나 이미 교전 중이면 false(상태 무변경).
		/// 실제 전이는 다음 Tick 에서 일어난다(전이 지점을 Tick 한 곳으로 유지 — 신호가 두 경로로 새지 않게).
		/// </summary>
		public bool RequestNextWave()
		{
			if (Outcome != TowerDefenseOutcome.InProgress || Phase != TowerDefensePhase.Prepare)
				return false;

			nextWaveRequested = true;
			return true;
		}

		/// <summary> 셸이 WaveStarted 를 받아 적을 실제 스폰한 뒤 호출. 이 호출 전에는 웨이브가 클리어되지 않는다. </summary>
		public void ConfirmWaveSpawned()
		{
			waveSpawnConfirmed = true;
		}

		/// <summary> 자원이 충분하면 차감하고 true. 부족하면 상태 무변경 + false(배치 거절). </summary>
		public bool TrySpend(int cost)
		{
			if (cost < 0 || Resource < cost)
				return false;

			Resource -= cost;
			return true;
		}

		/// <summary>
		/// 채집건물이 하나 가동 시작 — 다음 정산부터 수입 증가(= 개척 보상).
		/// incomeMultiplier = 그 자리의 벌이 배수(먼 노드일수록 크다). 「멀리 나갈수록 많이 번다」가
		/// 이 인자 하나로 성립한다 — 안 그러면 어느 노드를 잡든 똑같아서 개척할 이유가 없다.
		/// </summary>
		public void AddHarvester(float incomeMultiplier = 1f)
		{
			HarvesterCount++;
			harvesterIncomeWeight += incomeMultiplier > 0f ? incomeMultiplier : 0f;
		}

		/// <summary>
		/// 마수 한 마리가 목표에 닿았다 — 그 마수는 사라지고 목숨이 하나 준다.
		/// 「코어를 갉는다」가 아니라 「새면 잃는다」라, 한 마리를 놓치는 것 자체가 대가다.
		/// </summary>
		public void RegisterLeak()
		{
			if (UsesLives == false || Outcome != TowerDefenseOutcome.InProgress)
				return;

			Lives = Mathf.Max(0, Lives - 1);
			if (Lives > 0)
				return;

			Outcome = TowerDefenseOutcome.Defeat;
			Phase = TowerDefensePhase.Concluded;
		}

		/// <summary> 채집건물을 팔았다 — 수입도 같이 줄어야 「판다」가 공짜가 되지 않는다. </summary>
		public void RemoveHarvester(float incomeMultiplier)
		{
			HarvesterCount = Mathf.Max(0, HarvesterCount - 1);
			harvesterIncomeWeight = Mathf.Max(0f, harvesterIncomeWeight - Mathf.Max(0f, incomeMultiplier));
		}

		/// <summary>
		/// 다음 웨이브를 격퇴하면 들어올 정산액. 채집 인형이 *무슨 역할인지* 를 화면이 말해주는 유일한 숫자 —
		/// 인형을 하나 세울 때마다 이 값이 오르는 걸 보여야 「자원 캐는 건물」의 의미가 전달된다
		/// (사용자 실증: "자원 캐는 건물이 어떤 역할인지 전혀 모르겠어").
		/// </summary>
		public int NextWaveIncome => rules.BaseWaveIncome + Mathf.RoundToInt(rules.IncomePerHarvester * harvesterIncomeWeight);

		/// <summary> 웨이브 정산 외 즉시 지급(격파 보상 등). 음수 무시 — 자원이 조용히 줄어드는 경로를 만들지 않는다. </summary>
		public void AddResource(int amount)
		{
			if (amount <= 0)
				return;

			Resource += amount;
		}

		/// <summary> 마수 1기 격파 보상액(0 이면 격파 보상 없음). </summary>
		public int BountyPerKill => rules.BountyPerKill;
	}
}
