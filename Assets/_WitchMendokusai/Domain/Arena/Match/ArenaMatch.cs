using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 아레나 매치 오케스트레이터 — 맵 생성 → 유닛 스폰(기존 풀, 자동 DI) → ArenaCombatant/TacticDriver 부착
	/// → TargetingSystem 등록 → TimeManager 틱으로 ArenaMatchCore 폴 → 종료 시 MatchEnded + 드라이버 정지.
	/// 스코프 미배선(콘텐츠/item 9 슬라이스) — ObjectPoolManager/TimeManager 는 static Instance 로 캡처
	/// (World 부팅 후 보장). item 9 에서 스코프 등록 시 [Inject] Construct 로 전환 예정.
	/// ⚠ 라이브 거동(스폰/전투/종료)은 PlayMode 검증 필요. 코드코어 ArenaMatchCore 는 EditMode 검증됨.
	/// ⚠ 로스터 prefab 컴포넌트가 MonsterObject 면 던전 전용 side-effect(loot/stat/Camera.main)가 샐 수 있음 —
	///   실 유닛 타입 확정 + PlayMode 검증 시 ArenaUnitObject 또는 IsDungeon 가드로 격리(WM-165 후속).
	/// </summary>
	public class ArenaMatch : MonoBehaviour
	{
		[field: Header("_" + nameof(ArenaMatch))]
		[SerializeField] private ArenaMatchConfig config;
		[SerializeField] private Transform arenaRoot;

		private TargetingSystem targeting;
		private ArenaMatchCore core;
		private List<ArenaTeam> teams;
		private readonly List<TacticDriver> drivers = new();
		private bool started;
		private bool ticking;

		// behavior-verify 계측([Arena-Verify]) — 패트롤 vs 전진·교전 결착을 Editor.log 단독 판별(MCP wedge 직교).
		// 관측 누적치(수치 설정값 X) → 수치노출 룰 무관. 종결 1줄 + 첫교전 시각으로 완결.
		private readonly List<UnitHealth> hookedHealths = new();
		private int hitCount;
		private int tickCount;
		private bool firstContactLogged;
		private int runId;
		private static int nextRunId = 0;

		// 생명주기 정리(재매치 누수 방지 — 구조리뷰 fix-before-content) — 스폰 유닛/등록 참가자 추적 → Dispose 에서 despawn/unregister/맵 정리.
		private readonly List<GameObject> spawnedUnits = new();
		private readonly List<ICombatant> registered = new();
		private bool disposed;

		public event System.Action<int> MatchEnded = delegate { };

		public bool IsConcluded => core != null && core.IsConcluded;
		public int WinnerTeamId => core != null ? core.WinnerTeamId : ArenaModeSO.NO_WINNER;

		/// <summary> 프로그래매틱 시작(런처/모드 진입용) — config·arenaRoot 주입 후 Begin. </summary>
		public void Begin(ArenaMatchConfig matchConfig, Transform root)
		{
			config = matchConfig;
			arenaRoot = root;
			Begin();
		}

		public void Begin()
		{
			if (started)
			{
				Debug.LogWarning($"{nameof(ArenaMatch)}: 이미 진행 중 — 중복 Begin 무시(재진입 가드).");
				return;
			}
			if (config == null || arenaRoot == null || config.Map == null || config.Mode == null)
			{
				Debug.LogError($"{nameof(ArenaMatch)}: config/arenaRoot/Map/Mode 미할당 — 시작 불가.");
				return;
			}
			if (ValidateRoster() == false)
				return;

			started = true;
			StartCoroutine(BeginRoutine());
		}

		/// <summary> 로스터 ↔ 맵 정합 FastFail 검증 — 맵이 선언한 팀 수/팀당 스폰과 로스터를 대조(silent 겹침/0틱종료 차단). </summary>
		private bool ValidateRoster()
		{
			int teamCount = config.Map.TeamCount;
			int spawnsPerTeam = config.Map.SpawnsPerTeam;
			Dictionary<int, int> perTeam = new();

			foreach (ArenaMatchConfig.ArenaUnitEntry entry in config.Roster)
			{
				if (entry.UnitData == null || entry.UnitData.Prefab == null)
					continue;

				if (entry.TeamId < 0 || entry.TeamId >= teamCount)
				{
					Debug.LogError($"{nameof(ArenaMatch)}: 로스터 TeamId {entry.TeamId} 가 맵 TeamCount({teamCount}) 범위 밖 — 시작 불가.");
					return false;
				}
				perTeam[entry.TeamId] = (perTeam.TryGetValue(entry.TeamId, out int count) ? count : 0) + 1;
			}

			if (perTeam.Count < 2)
			{
				Debug.LogError($"{nameof(ArenaMatch)}: 유효 팀 {perTeam.Count} 개 — 한타는 최소 2팀 필요. 시작 불가.");
				return false;
			}

			foreach (KeyValuePair<int, int> pair in perTeam)
			{
				if (pair.Value > spawnsPerTeam)
				{
					Debug.LogError($"{nameof(ArenaMatch)}: 팀 {pair.Key} 유닛 {pair.Value} > 맵 SpawnsPerTeam({spawnsPerTeam}) — 스폰 겹침. 시작 불가.");
					return false;
				}
			}

			return true;
		}

		private IEnumerator BeginRoutine()
		{
			// init-order-ok: World 부팅 후 호출 보장(스코프 미배선 v1 — item 9 에서 [Inject] 전환). 진입부 1회 캡처(fail-fast).
			ObjectPoolManager pool = ObjectPoolManager.Instance;
			TimeManager timeManager = TimeManager.Instance;
			if (pool == null || timeManager == null)
			{
				Debug.LogError($"{nameof(ArenaMatch)}: ObjectPoolManager/TimeManager Instance null — World 부팅 후 호출 필요.");
				started = false;
				yield break;
			}

			hitCount = 0;
			tickCount = 0;
			firstContactLogged = false;
			runId = ++nextRunId;
			Debug.Log($"[Arena-Verify] MATCH-START runId={runId} z={arenaRoot.position.z}");

			config.Map.Build(arenaRoot);
			targeting = new TargetingSystem();

			Dictionary<int, List<ICombatant>> teamMembers = new();
			Dictionary<int, int> teamSpawnIndex = new();
			int combatantId = 0;

			foreach (ArenaMatchConfig.ArenaUnitEntry entry in config.Roster)
			{
				if (entry.UnitData == null || entry.UnitData.Prefab == null)
				{
					Debug.LogWarning($"{nameof(ArenaMatch)}: 로스터 entry skip — UnitData/Prefab 누락.");
					continue;
				}

				GameObject unitGameObject = pool.Spawn(entry.UnitData.Prefab);
				UnitObject unitObject = unitGameObject.GetComponent<UnitObject>();
				if (unitObject == null)
				{
					Debug.LogWarning($"{nameof(ArenaMatch)}: {entry.UnitData.Prefab.name} 에 UnitObject 컴포넌트 없음 — skip.");
					continue;
				}
				spawnedUnits.Add(unitGameObject); // Dispose 시 풀 반환(누수 방지).

				int memberIndex = teamSpawnIndex.TryGetValue(entry.TeamId, out int existing) ? existing : 0;
				teamSpawnIndex[entry.TeamId] = memberIndex + 1;

				IReadOnlyList<Vector3> teamSpawns = config.Map.GetSpawns(entry.TeamId);
				Vector3 localSpawn = teamSpawns.Count > 0 ? teamSpawns[memberIndex % teamSpawns.Count] : Vector3.zero;
				unitGameObject.transform.position = arenaRoot.TransformPoint(localSpawn);

				unitObject.Init(entry.UnitData);
				// 트랩#1: 전술 코어가 유일 시전자 → 자동시전 즉시 차단. UnitObject.Init 보존 패치로 Start 재-Init 후도 유지.
				unitObject.SkillHandler.AutoCastEnabled = false;

				ArenaCombatant combatant = unitObject.GetComponent<ArenaCombatant>();
				if (combatant == null)
					combatant = unitObject.gameObject.AddComponent<ArenaCombatant>();
				combatant.SetTeam(entry.TeamId, combatantId);
				combatantId++;

				unitGameObject.SetActive(true);

				// 자율 brain 격리 — prefab 내장 FSM(FSMSlime/FSMWisp 등)이 BT_MoveToPlayer 로 같은 UnitMovement 채널을
				// TacticDriver 와 last-writer-wins 경쟁(패트롤/지터/전진실패) → 아레나 출전 유닛은 brain 비활성.
				// SetActive(true) 직후라 OnEnable→코루틴 시작됨 → enabled=false 가 OnDisable→Dispose 로 코루틴 정지.
				// UnitBrain 마커 베이스로 일괄(구체 타입 enumerate X = 새 brain 자동 격리).
				foreach (UnitBrain brain in unitObject.GetComponents<UnitBrain>())
					brain.enabled = false;

				// 팀 식별 틴트 — 팀0(욘/아군)=하늘색, 팀1(라이벌)=빨강. v1: 풀 반환 시 색 잔존(teardown/ArenaUnitObject 후속서 리셋).
				if (unitObject.SpriteRenderer != null)
					unitObject.SpriteRenderer.color = entry.TeamId == 0 ? new Color(0.45f, 0.75f, 1f) : new Color(1f, 0.45f, 0.45f);

				TacticDriver driver = unitObject.GetComponent<TacticDriver>();
				if (driver == null)
					driver = unitObject.gameObject.AddComponent<TacticDriver>();
				driver.Initialize(entry.Tactic, targeting, timeManager);
				drivers.Add(driver);

				targeting.Register(combatant);
				registered.Add(combatant); // Dispose 시 unregister.

				// behavior-verify 교전 구독 — 첫 피격 = 유닛이 실제 교전(패트롤이면 영영 안 찍힘). 종결 시 UnhookHealths 로 해제.
				unitObject.Health.OnTakeDamage += OnCombatHit;
				hookedHealths.Add(unitObject.Health);

				if (teamMembers.ContainsKey(entry.TeamId) == false)
					teamMembers[entry.TeamId] = new List<ICombatant>();
				teamMembers[entry.TeamId].Add(combatant);
			}

			teams = new List<ArenaTeam>();
			foreach (KeyValuePair<int, List<ICombatant>> pair in teamMembers)
				teams.Add(new ArenaTeam(pair.Key, pair.Value));

			core = new ArenaMatchCore(teams, config.Mode, config.TimeLimitSeconds);
			timeManager.RegisterCallback(Tick);
			ticking = true;
		}

		private void Tick()
		{
			if (ticking == false || core == null)
				return;

			tickCount++;

			if (core.Poll(TimeManager.TICK))
			{
				ticking = false;
				if (TimeManager.TryGetExistingInstance(out TimeManager timeManager))
					timeManager.RemoveCallback(Tick);

				// 종료 = 브레인(core)이 actuation 정지 권한 행사 — 전 드라이버 정지(좀비 틱 방지).
				foreach (TacticDriver driver in drivers)
				{
					if (driver != null)
						driver.StopDriving();
				}

				// behavior-verify 종결 1줄 — reason=ELIMINATION(전진·교전 결착) vs TIMEOUT(교착=패트롤 의심).
				// alive snapshot 으로 누가 죽었나 + hits 로 교전 강도 노출. Editor.log grep 1방.
				string reason = core.ConcludedByElimination ? "ELIMINATION" : "TIMEOUT";
				string aliveSnapshot = "";
				if (teams != null)
				{
					foreach (ArenaTeam team in teams)
						aliveSnapshot += team.TeamId + ":" + team.AliveCount() + " ";
				}
				Debug.Log($"[Arena-Verify] MATCH-END runId={runId} reason={reason} winner={core.WinnerTeamId} hits={hitCount} ticks={tickCount} alive=[{aliveSnapshot.Trim()}]");
				UnhookHealths();

				MatchEnded(core.WinnerTeamId);
			}
		}

		// 첫 피격 = 유닛이 실제 교전(전진 증거). 패트롤이면 FIRST-CONTACT 영영 안 찍힘.
		private void OnCombatHit(DamageInfo damageInfo)
		{
			hitCount++;
			if (firstContactLogged == false)
			{
				firstContactLogged = true;
				Debug.Log($"[Arena-Verify] FIRST-CONTACT runId={runId} atTick={tickCount}");
			}
		}

		// 교전 구독 일괄 해제 — 풀 재사용 유닛 좀비 구독 방지(명시 teardown, FastFail 정합). 멱등.
		private void UnhookHealths()
		{
			foreach (UnitHealth health in hookedHealths)
			{
				if (health != null)
					health.OnTakeDamage -= OnCombatHit;
			}
			hookedHealths.Clear();
		}

		/// <summary>
		/// 매치 생명주기 정리(재매치 누수 방지 — 구조리뷰 fix-before-content). 단일 경로:
		/// 틱 콜백 해제 + 드라이버 정지 + 교전 구독 해제 + targeting unregister + 스폰 유닛 풀 반환 + 맵 기하 파괴.
		/// 멱등(disposed). OnDestroy = fallback. 결착(Tick)은 시각 보존 위해 dispose 안 함 — dispose 는 컴포넌트 파괴/재매치 진입 시.
		/// </summary>
		public void Dispose()
		{
			if (disposed)
				return;
			disposed = true;
			ticking = false;

			if (TimeManager.TryGetExistingInstance(out TimeManager timeManager))
				timeManager.RemoveCallback(Tick);

			foreach (TacticDriver driver in drivers)
			{
				if (driver != null)
					driver.StopDriving();
			}

			UnhookHealths();

			if (targeting != null)
			{
				foreach (ICombatant combatant in registered)
					targeting.Unregister(combatant);
			}
			registered.Clear();

			if (ObjectPoolManager.TryGetExistingInstance(out ObjectPoolManager pool))
			{
				foreach (GameObject unit in spawnedUnits)
				{
					if (unit != null)
						pool.Despawn(unit);
				}
			}
			spawnedUnits.Clear();

			if (config != null && config.Map != null && arenaRoot != null)
				config.Map.Teardown(arenaRoot);
		}

		private void OnDestroy()
		{
			Dispose();
		}
	}
}
