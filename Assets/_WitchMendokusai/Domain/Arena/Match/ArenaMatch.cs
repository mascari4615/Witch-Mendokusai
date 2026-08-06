using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 아레나 매치 오케스트레이터 — 맵 생성 → 유닛 스폰(기존 풀, 자동 DI) → MatchCombatant/TacticDriver 부착
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

		// 팀 식별 틴트 — 관전자가 한눈에 편을 가르는 색이라 눈으로 맞춰야 한다.
		// 팀0 = 욘/아군, 팀1 = 라이벌.
		[Tooltip("스폰끼리 최소한 떨어져야 하는 거리. 겹쳐 세우면 물리가 유닛을 맵 밖으로 튕긴다. 0 = 검사 끔.")]
		[SerializeField] private float minSpawnSeparation = 1f;

		[Header("Team Tint")]
		[SerializeField] private Color team0Tint = new(0.45f, 0.75f, 1f);
		[SerializeField] private Color team1Tint = new(1f, 0.45f, 0.45f);

		private TargetingSystem targeting;
		private ArenaMatchCore core;
		private List<MatchTeam> teams;
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

		// 팀 틴트를 칠하기 *전* 색. 풀은 색을 안 되돌리므로 칠한 채로 반납하면 그 유닛이 다음에
		// 던전이나 본편에서 나올 때 하늘색·빨강 그대로 나온다(스폰 경로에 색 초기화가 없다 — 실측).
		private readonly List<(SpriteRenderer Renderer, Color Original)> tintedRenderers = new();

		public event System.Action<int> MatchEnded = delegate { };

		/// <summary>
		/// Begin 이 실제로 매치를 띄웠나. **검증 하네스가 이걸 봐야 한다** — `Begin` 은 로스터·맵 검증에
		/// 걸리면 LogError 만 남기고 조용히 돌아오는데, 그러면 `MatchEnded` 도 영영 안 온다.
		/// 그걸 모르면 자동 검증이 판정 없이 Play 에 매달린 채로 끝난다.
		/// </summary>
		public bool IsRunning => started;

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
			int skipped = 0;

			for (int entryIndex = 0; entryIndex < config.Roster.Count; entryIndex++)
			{
				ArenaMatchConfig.ArenaUnitEntry entry = config.Roster[entryIndex];

				// ★ 여기서 조용히 넘기면 **그 다음 판정이 전부 거짓말이 된다.** 이 검사가 센 팀 인원이
				//   로스터에 보이는 인원과 달라지기 때문이다: 3v3 에서 프리팹 하나가 비면 검증은
				//   3v2 를 통과시키고(사람은 3v3 인 줄 안다), 더 비면 「유효 팀 1 개 — 최소 2팀 필요」로
				//   죽는데 그건 **원인이 아니라 증상**이다(로스터엔 팀이 둘 다 있다).
				//   스폰 루프는 이미 같은 조건에 경고를 찍고 있었다 — 정작 먼저 도는 이쪽만 말이 없었다.
				if (entry.UnitData == null || entry.UnitData.Prefab == null)
				{
					skipped++;
					Debug.LogWarning($"{nameof(ArenaMatch)}: 로스터 {entryIndex} 번(팀 {entry.TeamId}) 은 "
						+ (entry.UnitData == null ? "UnitData 가 비었다" : $"{entry.UnitData.name} 의 Prefab 이 비었다")
						+ " — 이 줄은 인원에서 빠진다.");
					continue;
				}

				if (entry.TeamId < 0 || entry.TeamId >= teamCount)
				{
					Debug.LogError($"{nameof(ArenaMatch)}: 로스터 TeamId {entry.TeamId} 가 맵 TeamCount({teamCount}) 범위 밖 — 시작 불가.");
					return false;
				}
				perTeam[entry.TeamId] = (perTeam.TryGetValue(entry.TeamId, out int count) ? count : 0) + 1;
			}

			if (perTeam.Count < 2)
			{
				// 빠진 줄이 있으면 그것부터 말한다 — 「팀이 1개」는 대개 결과지 원인이 아니다.
				Debug.LogError($"{nameof(ArenaMatch)}: 유효 팀 {perTeam.Count} 개 — 한타는 최소 2팀 필요. 시작 불가."
					+ (skipped > 0 ? $" (로스터 {config.Roster.Count} 줄 중 {skipped} 줄이 UnitData/Prefab 누락으로 빠졌다 — 위 경고 확인)" : string.Empty));
				return false;
			}

			foreach (KeyValuePair<int, int> pair in perTeam)
			{
				if (pair.Value > spawnsPerTeam)
				{
					Debug.LogError($"{nameof(ArenaMatch)}: 팀 {pair.Key} 유닛 {pair.Value} > 맵 SpawnsPerTeam({spawnsPerTeam}) — 스폰 겹침. 시작 불가.");
					return false;
				}

				// ★ 개수만 맞아도 자리가 겹칠 수 있다 — 맵이 데이터라 사람이 수치를 만지고,
				//   폭보다 여백을 크게 잡는 순간 스폰이 한 점으로 모인다. 겹쳐 세우면 물리가
				//   밀어낼 방향을 못 찾아 유닛을 맵 밖으로 날리고, 그 유닛은 죽지도 않아 매치가 안 끝난다.
				//   이 가드의 주석이 원래부터 「스폰 겹침 차단」이라고 말하고 있었는데 실제로는 개수만 봤다.
				IReadOnlyList<Vector3> teamSpawns = config.Map.GetSpawns(pair.Key);

				// ★ 선언(SpawnsPerTeam)과 실제(GetSpawns().Count)는 **다를 수 있다.** 위 검사는 선언만 봤다.
				//   스폰 배치는 `teamSpawns[memberIndex % teamSpawns.Count]` 로 집으므로, 실제가 유닛 수보다
				//   적으면 **modulo 가 돌아 뒷 유닛이 앞 유닛과 정확히 같은 점에 선다** — 겹침 그 자체다.
				//   `RectangleArenaMap` 은 둘 다 `PerTeam` 에서 나와 어긋날 수 없지만, `ArenaMapSO` 는
				//   abstract 이고 WM-165 는 원형·레인 맵을 예고한다. 계약을 지키는 쪽에서 못 박는다.
				if (teamSpawns.Count < pair.Value)
				{
					Debug.LogError($"{nameof(ArenaMatch)}: 팀 {pair.Key} 유닛 {pair.Value} 인데 맵이 준 스폰은 "
						+ $"{teamSpawns.Count} 개다(선언은 {spawnsPerTeam}) — 모자란 만큼 앞자리에 겹쳐 선다. 시작 불가. "
						+ $"{config.Map.GetType().Name} 의 SpawnsPerTeam 과 GetSpawns 가 어긋났다.");
					return false;
				}

				if (SpawnRules.TryFindOverlap(teamSpawns, minSpawnSeparation, out int firstSpawn, out int secondSpawn))
				{
					Debug.LogError($"{nameof(ArenaMatch)}: 팀 {pair.Key} 스폰 {firstSpawn}·{secondSpawn} 이 "
						+ $"{minSpawnSeparation} 보다 가깝다({teamSpawns[firstSpawn]} / {teamSpawns[secondSpawn]}) — "
						+ $"맵 수치(폭 대비 여백) 확인. 시작 불가.");
					return false;
				}
			}

			// ★ 팀 *사이* 겹침 — 위 루프는 팀 안만 본다. 그런데 가장 나쁜 겹침은 팀끼리다:
			//   RectangleArenaMap 은 스폰 z 를 `±(Length/2 - SpawnInset)` 로 잡으므로,
			//   SpawnInset 이 Length/2 면 **두 팀이 똑같이 z=0** 에 선다. 이때 **넓은 판**(Width > 2*SpawnInset)
			//   이면 X 는 멀쩡히 퍼져 있어 팀 안 검사엔 아무 이상도 안 보이고 **팀끼리만 정확히 포개진다**
			//   (실측: 40×20 inset 10 → 팀 안 정상 / 팀 간 완전 겹침. 좁은 판이면 X 도 같이 붕괴해 위 검사가 먼저 잡는다).
			//   적끼리 완전히 포개진 캡슐이야말로 물리가 밀 방향을 못 찾는 그 경우다.
			List<int> teamIds = new(perTeam.Keys);
			for (int left = 0; left < teamIds.Count; left++)
			{
				for (int right = left + 1; right < teamIds.Count; right++)
				{
					IReadOnlyList<Vector3> leftSpawns = config.Map.GetSpawns(teamIds[left]);
					IReadOnlyList<Vector3> rightSpawns = config.Map.GetSpawns(teamIds[right]);
					if (SpawnRules.TryFindOverlapAcross(leftSpawns, rightSpawns, minSpawnSeparation, out int leftIndex, out int rightIndex))
					{
						Debug.LogError($"{nameof(ArenaMatch)}: 팀 {teamIds[left]} 스폰 {leftIndex} 와 "
							+ $"팀 {teamIds[right]} 스폰 {rightIndex} 가 {minSpawnSeparation} 보다 가깝다"
							+ $"({leftSpawns[leftIndex]} / {rightSpawns[rightIndex]}) — "
							+ "맵 수치 확인(SpawnInset 이 Length/2 에 가까우면 두 팀이 한 줄에 선다). 시작 불가.");
						return false;
					}
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

				// 전술이 비면 그 유닛은 **가만히 서 있는다** — 예외도 로그도 없이. 이 게임의 「왜 안 움직이지」는
				// 지금까지 대부분 원인이 달랐고(brain 경쟁·좀비 드라이버·사거리) 매번 찾는 데 오래 걸렸다.
				// 적어도 「전술이 비었다」는 경우만은 시작할 때 이름을 붙여준다. 막지는 않는다 —
				// 표적 더미처럼 일부러 안 움직이는 유닛도 있을 수 있다.
				if (entry.Tactic == null || entry.Tactic.Rules == null || entry.Tactic.Rules.Count == 0)
				{
					Debug.LogWarning($"{nameof(ArenaMatch)}: 팀 {entry.TeamId} 의 {entry.UnitData.name} 은 전술이 비었다 "
						+ "— 스폰은 되지만 아무것도 안 한다(의도한 게 아니면 로스터의 Tactic 확인).");
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

				// Init → 트랩#1(자동시전 차단) → MatchCombatant 부여 = 개척과 공유하는 편입 절차.
				MatchCombatant combatant = CombatUnitSpawner.Enlist(unitObject, entry.UnitData, entry.TeamId, combatantId);
				combatantId++;

				unitGameObject.SetActive(true);

				// 트랩#2 — prefab 내장 FSM(FSMSlime/FSMWisp 등)이 BT_MoveToPlayer 로 같은 UnitMovement 채널을
				// TacticDriver 와 last-writer-wins 경쟁(패트롤/지터/전진실패)하므로 출전 유닛은 brain 비활성.
				CombatUnitSpawner.SilenceBrains(unitGameObject);

				// 팀 식별 틴트. 칠하기 전 색을 적어둔다 — Dispose 가 되돌린다(안 되돌리면 풀이 물고 간다).
				if (unitObject.SpriteRenderer != null)
				{
					tintedRenderers.Add((unitObject.SpriteRenderer, unitObject.SpriteRenderer.color));
					unitObject.SpriteRenderer.color = entry.TeamId == 0 ? team0Tint : team1Tint;
				}

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

			teams = new List<MatchTeam>();
			foreach (KeyValuePair<int, List<ICombatant>> pair in teamMembers)
				teams.Add(new MatchTeam(pair.Key, pair.Value));

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
					foreach (MatchTeam team in teams)
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
		/// 매치 생명주기 정리 — 단일 경로 + *재진입 가능*(모드 enter→exit→enter, TASK-WM-165 item9):
		/// 틱 콜백 해제 + 드라이버 정지/클리어 + 교전 구독 해제 + targeting unregister + 스폰 유닛 풀 반환 + 맵 기하 파괴
		/// + 진입 상태 리셋(started 가드 해제 → 다음 Begin 허용). 컬렉션 비움으로 멱등(런처 Dispose→Destroy→OnDestroy
		/// 이중 호출 무해). 결착(Tick)은 시각 보존 위해 dispose 안 함 — dispose 는 모드 이탈/컴포넌트 파괴 시.
		/// </summary>
		public void Dispose()
		{
			ticking = false;

			if (TimeManager.TryGetExistingInstance(out TimeManager timeManager))
				timeManager.RemoveCallback(Tick);

			foreach (TacticDriver driver in drivers)
			{
				if (driver != null)
					driver.StopDriving();
			}
			drivers.Clear();

			UnhookHealths();

			if (targeting != null)
			{
				foreach (ICombatant combatant in registered)
					targeting.Unregister(combatant);
			}
			registered.Clear();

			// ★ 반납 *전에* 팀 틴트를 되돌린다. 풀은 색을 안 되돌리므로 칠한 채로 보내면 그 인스턴스가
			//   다음에 던전에서 나올 때 하늘색 슬라임이 된다 — 관전용 색이 본편으로 새는 것이다.
			foreach ((SpriteRenderer renderer, Color original) in tintedRenderers)
			{
				if (renderer != null)
					renderer.color = original;
			}
			tintedRenderers.Clear();

			if (ObjectPoolManager.TryGetExistingInstance(out ObjectPoolManager pool))
			{
				foreach (GameObject unit in spawnedUnits)
				{
					if (unit != null)
						pool.Despawn(unit);
				}
			}
			spawnedUnits.Clear();

			// ⚠ Teardown 은 `arenaRoot` 의 **자식을 전부 Destroy** 한다. 그래서 스폰 유닛을 arenaRoot
			//   밑에 매달면 안 된다 — 지금은 안 매단다(풀이 준 부모 그대로 두고 위치만 옮긴다).
			//
			//   「관리하기 좋게 매치 유닛을 arenaRoot 자식으로 모으자」는 자연스러운 정리처럼 보이는데,
			//   그렇게 하면 **풀이 깨진다**: 바로 위 `pool.Despawn` 의 반납 경로(ObjectPool.Push)는
			//   비활성화·스택 push 까지만 즉시 하고 **부모 되돌리기를 UniTask.DelayFrame(1) 로 미룬다.**
			//   즉 이 줄이 도는 같은 프레임엔 유닛이 아직 arenaRoot 자식이라, Teardown 이 **풀 스택 안에
			//   들어있는 오브젝트를 Destroy** 한다 → 다음 Pop 이 파괴된 오브젝트를 돌려준다(MissingReference).
			//   증상은 투기장이 아니라 **다음에 그 프리팹을 쓰는 아무 곳에서나** 터진다.
			if (config != null && config.Map != null && arenaRoot != null)
				config.Map.Teardown(arenaRoot);

			// 재진입 — 다음 Begin() 이 새 매치를 돌릴 수 있게 진입 상태 리셋(started 가드 해제 + 코어/팀 비움).
			core = null;
			teams = null;
			started = false;
		}

		private void OnDestroy()
		{
			Dispose();
		}
	}
}
