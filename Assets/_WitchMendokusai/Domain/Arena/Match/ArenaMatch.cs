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
		private readonly List<TacticDriver> drivers = new();
		private bool started;
		private bool ticking;

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

				// 팀 식별 틴트 — 팀0(욘/아군)=하늘색, 팀1(라이벌)=빨강. v1: 풀 반환 시 색 잔존(teardown/ArenaUnitObject 후속서 리셋).
				if (unitObject.SpriteRenderer != null)
					unitObject.SpriteRenderer.color = entry.TeamId == 0 ? new Color(0.45f, 0.75f, 1f) : new Color(1f, 0.45f, 0.45f);

				TacticDriver driver = unitObject.GetComponent<TacticDriver>();
				if (driver == null)
					driver = unitObject.gameObject.AddComponent<TacticDriver>();
				driver.Initialize(entry.Tactic, targeting, timeManager);
				drivers.Add(driver);

				targeting.Register(combatant);

				if (teamMembers.ContainsKey(entry.TeamId) == false)
					teamMembers[entry.TeamId] = new List<ICombatant>();
				teamMembers[entry.TeamId].Add(combatant);
			}

			List<ArenaTeam> teams = new();
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

				MatchEnded(core.WinnerTeamId);
			}
		}

		private void OnDestroy()
		{
			if (ticking && TimeManager.TryGetExistingInstance(out TimeManager timeManager))
				timeManager.RemoveCallback(Tick);
		}
	}
}
