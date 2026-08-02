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

		// ── 실시간(RTS) 시계 ─────────────────────────────────────────────────────
		// ★ 왜 페이즈를 없앴나 (사용자 지시, 데아빌 지목): 「건설 페이즈 / 싸움 페이즈」는 판을 턴제처럼
		//   토막 낸다. 실시간이면 *언제나 지을 수 있고 언제나 위험하다* — 지금 이 순간 무엇에 손을 댈지가
		//   매초의 선택이 된다. 대신 세 개의 시계가 각자 돈다:
		//   ① 큰 무리(WaveInterval) ② 정산(IncomeInterval) ③ 상시로 흘러나오는 마수(TrickleInterval).
		private float waveAccumulated;
		private float incomeAccumulated;
		private float trickleAccumulated;
		private bool pendingWave;
		private bool pendingIncome;
		private bool pendingTrickle;

		/// <summary> 판이 시작되고 흐른 시간 — 실시간에서 진행도는 웨이브 수가 아니라 버틴 시간이다. </summary>
		public float ElapsedSeconds { get; private set; }

		/// <summary> 다음 큰 무리까지 남은 시간(초). 화면이 「곧 온다」를 보여주는 유일한 숫자. </summary>
		public float NextWaveIn => Mathf.Max(0f, rules.WaveInterval - waveAccumulated);

		/// <summary> 다음 정산까지 남은 시간(초). </summary>
		public float NextIncomeIn => Mathf.Max(0f, rules.IncomeInterval - incomeAccumulated);

		/// <summary> 지금 마수에 걸리는 압력 배수 — 오래 버틸수록 마수가 단단해진다. </summary>
		public float Pressure => rules.PressureAt(ElapsedSeconds);

		/// <summary>
		/// (구 페이즈제 잔재) 실시간에서는 늘 흐른다 — 남겨둔 이유는 화면·씬 참조가 아직 읽기 때문.
		/// 값은 진행에 영향을 주지 않는다.
		/// </summary>
		public bool AutoAdvance { get; set; } = true;
		public int FirstAutoWave { get; set; }

		/// <summary> 「지금 와라」가 예약됐는지 — 다음 틱에 큰 무리가 나온다. </summary>
		public bool IsNextWaveRequested => nextWaveRequested;

		// 실시간이라 국면이 없다 — 끝났는지만 구분한다(화면·하네스가 아직 이 값을 읽는다).
		public TowerDefensePhase Phase { get; private set; } = TowerDefensePhase.Assault;
		public TowerDefenseOutcome Outcome { get; private set; } = TowerDefenseOutcome.InProgress;

		/// <summary> 현재(또는 다음) 웨이브 번호 0-based. 승리 시 WaveCount 와 같아진다. </summary>
		public int WaveIndex { get; private set; }
		public int Resource { get; private set; }
		public int HarvesterCount { get; private set; }

		/// <summary>
		/// 정수 — 바깥 노드에서만 나오는 귀한 자원. 강화(승급·연구)의 유일한 통로다.
		/// ★ 자원이 한 종류면 「멀리 나간다」의 보상이 그냥 숫자가 더 큰 것뿐이라 위험을 감수할 이유가 약하다.
		///   바깥에서만 나오는 것을 강화에 묶으면 개척이 *강해지는 유일한 길*이 된다.
		/// </summary>
		public int Essence { get; private set; }

		private float essenceHarvesterWeight;

		/// <summary> 정수 채집 인형 하나 가동(바깥 노드). </summary>
		public void AddEssenceHarvester(float incomeMultiplier)
		{
			essenceHarvesterWeight += incomeMultiplier > 0f ? incomeMultiplier : 0f;
		}

		/// <summary> 정수 채집 인형을 팔았다. </summary>
		public void RemoveEssenceHarvester(float incomeMultiplier)
		{
			essenceHarvesterWeight = Mathf.Max(0f, essenceHarvesterWeight - Mathf.Max(0f, incomeMultiplier));
		}

		/// <summary> 다음 정산에 들어올 정수. </summary>
		public int NextWaveEssence => Mathf.RoundToInt(rules.EssencePerHarvester * essenceHarvesterWeight);

		/// <summary> 정수가 충분하면 차감하고 true. </summary>
		public bool TrySpendEssence(int cost)
		{
			if (cost < 0 || Essence < cost)
				return false;

			Essence -= cost;
			return true;
		}

		/// <summary> 남은 목숨. 유출제를 안 쓰는 스테이지(StartingLives<=0)면 항상 0 이고 무시된다. </summary>
		public int Lives { get; private set; }

		/// <summary> 이 스테이지가 유출제인가. </summary>
		public bool UsesLives => rules.StartingLives > 0;

		// 채집 인형들의 벌이 배수 합 — 마리수가 아니라 *어디에 세웠는지*가 수입을 만든다.
		private float harvesterIncomeWeight;
		/// <summary> (구 페이즈제 잔재) 실시간에는 건설 시간이 없다 — 다음 큰 무리까지 남은 시간을 준다. </summary>
		public float PrepareRemaining => NextWaveIn;

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

			ElapsedSeconds += deltaSeconds;
			waveAccumulated += deltaSeconds;
			incomeAccumulated += deltaSeconds;
			trickleAccumulated += deltaSeconds;

			// 큰 무리 — 시계가 부른다. 사람이 「지금 와라」로 앞당길 수도 있다(기다림이 벌칙이 되지 않게).
			if (nextWaveRequested || (rules.WaveInterval > 0f && waveAccumulated >= rules.WaveInterval))
			{
				nextWaveRequested = false;
				waveAccumulated = 0f;
				WaveIndex++;
				pendingWave = true;
			}

			if (rules.IncomeInterval > 0f && incomeAccumulated >= rules.IncomeInterval)
			{
				incomeAccumulated -= rules.IncomeInterval;
				pendingIncome = true;
			}

			if (rules.TrickleInterval > 0f && trickleAccumulated >= rules.TrickleInterval)
			{
				trickleAccumulated -= rules.TrickleInterval;
				pendingTrickle = true;
			}

			// 한 틱에 여럿이 겹치면 무거운 것부터 하나씩 — 남은 것은 다음 틱에 나온다(틱이 초당 여러 번이라
			// 사람이 느낄 지연이 없다). 신호를 묶어 보내면 셸이 어느 것부터 처리할지 매번 다시 정해야 한다.
			if (pendingWave)
			{
				pendingWave = false;
				return TowerDefenseSignal.WaveStarted;
			}

			if (pendingIncome)
			{
				pendingIncome = false;
				Resource += NextWaveIncome;
				Essence += NextWaveEssence;
				return TowerDefenseSignal.IncomeDue;
			}

			if (pendingTrickle)
			{
				pendingTrickle = false;
				return TowerDefenseSignal.TrickleDue;
			}

			return TowerDefenseSignal.None;
		}

		/// <summary>
		/// 다음 웨이브 호출 — 건설 국면에서만 성립. 진행 중이 아니거나 이미 교전 중이면 false(상태 무변경).
		/// 실제 전이는 다음 Tick 에서 일어난다(전이 지점을 Tick 한 곳으로 유지 — 신호가 두 경로로 새지 않게).
		/// </summary>
		public bool RequestNextWave()
		{
			if (Outcome != TowerDefenseOutcome.InProgress)
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

		/// <summary>
		/// 지금 유효한 채집 가중치를 통째로 정한다 — 보급이 끊기면 그 채집은 없는 것과 같으므로,
		/// 「지을 때 더한다」가 아니라 「지금 몇 개가 이어져 있나」를 매번 다시 알려주는 쪽이 진실이다.
		/// </summary>
		public void SetHarvesterWeights(float resourceWeight, float essenceWeight)
		{
			harvesterIncomeWeight = Mathf.Max(0f, resourceWeight);
			essenceHarvesterWeight = Mathf.Max(0f, essenceWeight);
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
		public int NextWaveIncome => Mathf.RoundToInt(
			(rules.BaseWaveIncome + rules.IncomePerHarvester * harvesterIncomeWeight) * Mathf.Max(0f, IncomeMultiplier));

		/// <summary> 웨이브 정산 외 즉시 지급(격파 보상 등). 음수 무시 — 자원이 조용히 줄어드는 경로를 만들지 않는다. </summary>
		public void AddResource(int amount)
		{
			if (amount <= 0)
				return;

			Resource += amount;
		}

		/// <summary> 마수 1기 격파 보상액(0 이면 격파 보상 없음). </summary>
		public int BountyPerKill => rules.BountyPerKill;

		/// <summary>
		/// 드래프트로 쌓인 정산 배수(1 = 없음). 규칙이 아니라 *이번 판의 성격*이라 셸이 넣어준다 —
		/// 카드가 늘어도 코어는 이 숫자 하나만 본다.
		/// </summary>
		public float IncomeMultiplier { get; set; } = 1f;

		/// <summary> 목숨을 더 받는다(드래프트 카드) — 유출제가 아닌 스테이지에선 아무 일도 없다. </summary>
		public void AddLives(int amount)
		{
			if (amount <= 0 || UsesLives == false || Outcome != TowerDefenseOutcome.InProgress)
				return;

			Lives += amount;
		}

		/// <summary> 정수를 즉시 받는다(드래프트 카드). </summary>
		public void AddEssence(int amount)
		{
			if (amount <= 0)
				return;

			Essence += amount;
		}
	}
}
