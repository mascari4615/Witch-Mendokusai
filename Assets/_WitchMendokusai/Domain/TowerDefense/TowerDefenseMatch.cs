using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 특수시공 개척(TD) 매치 오케스트레이터 — ArenaMatch 와 동형 셸(맵 생성 → 유닛 스폰(기존 풀, 자동 DI)
	/// → ArenaCombatant/TacticDriver 부착 → TargetingSystem 등록 → TimeManager 틱으로 TowerDefenseCore 폴).
	/// 규칙 판단은 전부 순수 코어(TowerDefenseCore)에 있고 본 셸은 그 신호(TowerDefenseSignal)를 받아
	/// 스폰/자원차감/정리 같은 actuation 만 수행 — Arena 아키텍처의 "코어=브레인, 셸=손발" 원칙 그대로 재사용.
	/// 배치 UI/입력전략/게임모드진입/카메라는 별도 증분(본 셸은 매치 진행 자체만 담당).
	/// </summary>
	public class TowerDefenseMatch : MonoBehaviour
	{
		private const int DEFENDER_TEAM = 0; // 코어/타워/채집건물 소속 팀.
		private const int ATTACKER_TEAM = 1; // 웨이브 적 소속 팀.

		[field: Header("_" + nameof(TowerDefenseMatch))]
		[SerializeField] private TowerDefenseStageSO stage;
		[SerializeField] private Transform stageRoot;

		private ObjectPoolManager pool;
		private TimeManager timeManager;
		private TargetingSystem targeting;
		private TowerDefenseCore core;
		private ArenaCombatant coreCombatant;
		private int nextCombatantId;
		private bool started;
		private bool ticking;
		private bool matchEndedFired;

		// 생명주기 정리(재매치 누수 방지, ArenaMatch 와 동형) — 스폰 유닛/등록 참가자/구동 드라이버 추적 →
		// Dispose 에서 despawn/unregister/정지.
		private readonly List<GameObject> spawnedUnits = new();
		private readonly List<ICombatant> registeredCombatants = new();
		private readonly List<TacticDriver> drivers = new();

		// 매 틱 aliveEnemies 카운트용 — 죽거나 풀 반환된(null) 엔트리는 조회 시 제거(멱등 정리).
		// 웨이브마다 SpawnWaveRoutine 시작에서 비움(이전 웨이브 잔여가 다음 웨이브에 누적되는 것 방지).
		private readonly List<ArenaCombatant> waveEnemies = new();

		// 자원 노드 점유 — 채집건물은 반드시 미점유 노드를 잡아야 가동(개척 리스크). index = stage.ResourceNodePositions 인덱스.
		private readonly HashSet<int> claimedNodes = new();

		// 셀 점유(TASK-WM-194 증분3) — 타워/채집건물 배치는 한 셀에 하나만(겹배치 차단). 키 = FloorToInt 셀(y=0 고정,
		// 층 무관 단일 격자). claimedNodes(자원 노드 자체 점유)와 직교 — 이건 "그 좌표에 뭔가 이미 서 있나"만 본다.
		private readonly HashSet<Vector3Int> occupiedCells = new();

		public event Action<TowerDefenseOutcome> MatchEnded = delegate { };

		public int Resource => core != null ? core.Resource : 0;
		public int WaveIndex => core != null ? core.WaveIndex : 0;
		public TowerDefensePhase Phase => core != null ? core.Phase : TowerDefensePhase.Prepare;
		public TowerDefenseOutcome Outcome => core != null ? core.Outcome : TowerDefenseOutcome.InProgress;
		public float PrepareRemaining => core != null ? core.PrepareRemaining : 0f;

		/// <summary> 프로그래매틱 시작(런처/모드 진입용) — stage·stageRoot 주입 후 Begin. </summary>
		public void Begin(TowerDefenseStageSO stageConfig, Transform root)
		{
			stage = stageConfig;
			stageRoot = root;
			Begin();
		}

		public void Begin()
		{
			if (started)
			{
				Debug.LogWarning($"{nameof(TowerDefenseMatch)}: 이미 진행 중 — 중복 Begin 무시(재진입 가드).");
				return;
			}
			if (stage == null || stageRoot == null)
			{
				Debug.LogError($"{nameof(TowerDefenseMatch)}: stage/stageRoot 미할당 — 시작 불가.");
				return;
			}
			if (stage.CoreUnit == null || stage.CoreUnit.Prefab == null)
			{
				Debug.LogError($"{nameof(TowerDefenseMatch)}: stage.CoreUnit/Prefab 미할당 — 코어 없이 시작 불가.");
				return;
			}

			started = true;
			StartCoroutine(BeginRoutine());
		}

		private IEnumerator BeginRoutine()
		{
			// init-order-ok: World 부팅 후 호출 보장(ArenaMatch 와 동형 — 스코프 미배선 v1). 진입부 1회 캡처(fail-fast).
			pool = ObjectPoolManager.Instance;
			timeManager = TimeManager.Instance;
			if (pool == null || timeManager == null)
			{
				Debug.LogError($"{nameof(TowerDefenseMatch)}: ObjectPoolManager/TimeManager Instance null — World 부팅 후 호출 필요.");
				started = false;
				yield break;
			}

			BuildGround();

			targeting = new TargetingSystem();
			core = new TowerDefenseCore(stage.Rules);
			nextCombatantId = 0;
			matchEndedFired = false;
			claimedNodes.Clear(); // 재진입 — 지난 매치의 노드 점유가 새 매치로 새는 것 방지.
			occupiedCells.Clear(); // 재진입 — 지난 매치의 셀 점유가 새 매치로 새는 것 방지.

			yield return SpawnCoreRoutine();
			if (coreCombatant == null)
			{
				// 코어 스폰 자체가 실패 — 이미 로그됨. 진입 상태만 리셋(started 가드 해제).
				started = false;
				yield break;
			}

			timeManager.RegisterCallback(Tick);
			ticking = true;
		}

		/// <summary> 지면(바닥) 런타임 생성 — RectangleArenaMap.Build 와 동형(Plane 스케일, SO 수치 그대로). </summary>
		private void BuildGround()
		{
			GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
			ground.name = "Ground";
			ground.transform.SetParent(stageRoot, false);
			ground.transform.localPosition = Vector3.zero;
			// Plane = 10x10 유닛 @ scale 1 → GroundWidth/GroundLength 에 맞춰 스케일.
			ground.transform.localScale = new Vector3(stage.GroundWidth / 10f, 1f, stage.GroundLength / 10f);

			ApplyGroundCheckerboard(ground);
			BuildResourceNodeMarkers();
			BuildEnemySpawnMarkers();
		}

		/// <summary>
		/// 바닥 체크무늬 — 배치는 1칸 격자에 스냅되는데 바닥이 민무늬면 "어디가 한 칸인지" 알 수 없다
		/// (사용자 실증: "땅은 격자나 체크무늬가 없어서 어디가 구분인지도 모르겠다").
		/// 텍스처를 코드로 생성 = 아트 에셋 의존 0. 타일링을 스테이지 칸 크기에 맞춰 *보이는 칸 = 배치 칸*
		/// 이 되게 한다(둘이 어긋나면 격자가 오히려 거짓말을 한다).
		/// </summary>
		private void ApplyGroundCheckerboard(GameObject ground)
		{
			Renderer groundRenderer = ground.GetComponent<Renderer>();
			if (groundRenderer == null)
				return;

			// 한 칸 = 텍스처 1장. 칸 경계에 밝은 선을 그어 격자를 *선으로* 보이게 한다
			// (2x2 체크무늬는 화면에서 거의 안 읽혔다 — 사용자 실증 "바닥 격자 좀 만들어줘").
			// 체크 음영도 함께 넣어 짝수/홀수 칸이 구분되게.
			const int CELL_PIXELS = 32;
			const int LINE_PIXELS = 2;
			Texture2D checker = new Texture2D(CELL_PIXELS, CELL_PIXELS, TextureFormat.RGBA32, mipChain: true)
			{
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Repeat,
			};
			Color fill = new Color(0.26f, 0.29f, 0.34f, 1f);
			Color gridLine = new Color(0.55f, 0.62f, 0.72f, 1f);
			for (int y = 0; y < CELL_PIXELS; y++)
			{
				for (int x = 0; x < CELL_PIXELS; x++)
				{
					bool onEdge = x < LINE_PIXELS || y < LINE_PIXELS;
					checker.SetPixel(x, y, onEdge ? gridLine : fill);
				}
			}
			checker.Apply();

			// 텍스처 1장 = 배치 1칸이므로 타일 수 = 전체 길이 / 칸크기 (보이는 칸 = 배치 칸).
			float cell = stage.GroundCellSize > 0f ? stage.GroundCellSize : 1f;
			Vector2 tiling = new Vector2(stage.GroundWidth / cell, stage.GroundLength / cell);

			Material groundMaterial = groundRenderer.material;
			groundMaterial.mainTexture = checker;
			groundMaterial.mainTextureScale = tiling;
			// URP Lit 는 _BaseMap/_BaseColor 가 정본 — mainTexture 만 세팅하면 셰이더에 따라 안 먹을 수 있다.
			if (groundMaterial.HasProperty("_BaseMap"))
			{
				groundMaterial.SetTexture("_BaseMap", checker);
				groundMaterial.SetTextureScale("_BaseMap", tiling);
			}
			if (groundMaterial.HasProperty("_BaseColor"))
				groundMaterial.SetColor("_BaseColor", Color.white);
		}


		/// <summary>
		/// 화면에서 즉시 읽히게 만드는 공통 처리 — 역할 색 + 한 칸 크기 + 애니메이터 정지.
		///
		/// ★ 애니메이터 정지가 핵심(실측): 프리팹 '[Sprite] Unit' 에 슬라임 애니메이터가 붙어 있어
		///   매 프레임 sprite 를 자기 클립으로 덮어쓴다 → 유닛 데이터의 그림을 아무리 넣어도 다음
		///   프레임에 슬라임으로 되돌아갔다(사용자 실증 2회 "여전히 슬라임"). 끄지 않으면 어떤 시각
		///   구분도 무의미.
		/// ★ 색 = 정체: 아트가 아직 없으므로 역할 4색을 서로 멀게 잡고, HUD 범례가 같은 색을 읽어
		///   화면에 이름을 띄운다(색↔이름 단일 소스 — 둘이 어긋나면 안내가 거짓말이 된다).
		/// ★ 크기 = 격자 한 칸: 칸보다 크면 서로 밀치고 소속도 안 읽힌다.
		/// </summary>
		private void ApplyReadability(UnitObject unitObject, Color tint, float scale)
		{
			if (unitObject == null)
				return;

			// 손대기 전 원본 스냅샷 — 반납 시 그대로 되돌린다(다시 시작해도 지난 매치 흔적 0).
			AcquireLease(unitObject);

			foreach (Animator animator in unitObject.GetComponentsInChildren<Animator>(true))
				animator.enabled = false;

			if (unitObject.SpriteRenderer != null)
				unitObject.SpriteRenderer.color = tint;

			unitObject.transform.localScale = Vector3.one * scale;
		}

		/// <summary> 대여 계약 부착 + 원본 스냅샷 — 멱등(이미 붙어 있으면 재사용, 스냅샷은 최초 1회만). </summary>
		private static void AcquireLease(UnitObject unitObject)
		{
			TowerDefenseUnitLease lease = unitObject.GetComponent<TowerDefenseUnitLease>();
			if (lease == null)
				lease = unitObject.gameObject.AddComponent<TowerDefenseUnitLease>();
			lease.Acquire(unitObject);
		}

		/// <summary>
		/// 풀 반납 단일 경로 — 반납 *전에* 원상복구(<see cref="TowerDefenseUnitLease.Release"/>).
		/// 이걸 거치지 않고 Despawn 하면 다음 매치가 지난 매치의 색·크기·정지된 애니메이터·역할 드라이버를
		/// 그대로 물려받는다(코어/포탑/채집/마수가 같은 프리팹 = 같은 풀이라 역할까지 섞인다).
		/// </summary>
		private static void ReleaseUnit(ObjectPoolManager targetPool, GameObject unit)
		{
			if (unit == null)
				return;

			TowerDefenseUnitLease lease = unit.GetComponent<TowerDefenseUnitLease>();
			if (lease != null)
				lease.Release(unit.GetComponent<UnitObject>());

			targetPool.Despawn(unit);
		}

		/// <summary>
		/// 자원 노드 표식 — 채집 인형은 노드 반경 안에만 설 수 있는데 노드가 안 보이면 플레이어가
		/// 어디를 클릭할지 알 수 없다(플레이 불가). 시각 표식은 순수 연출이라 콜라이더 제거 —
		/// 배치 레이캐스트를 가로채면 스냅 좌표가 표식 표면 기준으로 튄다.
		/// stageRoot 자식이라 Dispose 의 자식 파괴 경로가 그대로 정리한다.
		/// </summary>
		private void BuildResourceNodeMarkers()
		{
			if (stage.ResourceNodePositions == null)
				return;

			foreach (Vector3 localPosition in stage.ResourceNodePositions)
			{
				GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
				marker.name = "ResourceNode";
				Collider markerCollider = marker.GetComponent<Collider>();
				if (markerCollider != null)
					Destroy(markerCollider);

				marker.transform.SetParent(stageRoot, false);
				marker.transform.localPosition = localPosition;
				// 납작한 원반 — 지면에 깔리되 유닛 시야를 안 가림.
				marker.transform.localScale = new Vector3(stage.NodeCaptureRadius * 2f, 0.05f, stage.NodeCaptureRadius * 2f);

				// URP Lit 는 _BaseColor 가 정본 — material.color 만 세팅하면 셰이더에 따라 안 먹는다.
				Renderer markerRenderer = marker.GetComponent<Renderer>();
				if (markerRenderer != null)
				{
					Material markerMaterial = markerRenderer.material;
					Color nodeColor = new Color(1f, 0.82f, 0.25f, 1f); // 금빛 = "여기서 캔다". 바닥(회색)·아군(파랑)·적(빨강) 과 전부 구분.
					markerMaterial.color = nodeColor;
					if (markerMaterial.HasProperty("_BaseColor"))
						markerMaterial.SetColor("_BaseColor", nodeColor);
				}
			}
		}


		/// <summary>
		/// 마수 출현 표시 — 어디서 적이 들어오는지 모르면 방어선을 세울 수가 없다.
		/// 사용자 실증: 자원 노드 원을 "몬스터 나오는 원" 으로 오인했다. 원인은 ① 출현 지점에 아무
		/// 표시가 없었고 ② 자원 노드가 출현선 바로 앞(z=14 vs 출현 z=15)에 깔려 있어서 — 즉
		/// *표시 부재* + *배치 오류* 가 겹쳤다. 출현 지점에 붉은 표식을 세워 둘을 확실히 가른다.
		/// 노드(금빛 원반)와 형태·색을 다르게 해야 혼동이 안 난다.
		/// </summary>
		private void BuildEnemySpawnMarkers()
		{
			if (stage.EnemySpawnPoints == null)
				return;

			foreach (Vector3 localPosition in stage.EnemySpawnPoints)
			{
				GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
				marker.name = "EnemySpawnMarker";
				Collider markerCollider = marker.GetComponent<Collider>();
				if (markerCollider != null)
					Destroy(markerCollider);

				marker.transform.SetParent(stageRoot, false);
				marker.transform.localPosition = localPosition;
				// 넓고 낮은 판 — 출현 "구역" 으로 읽히게(원반=자원과 형태로 구분).
				marker.transform.localScale = new Vector3(3f, 0.06f, 1.2f);

				Renderer markerRenderer = marker.GetComponent<Renderer>();
				if (markerRenderer != null)
				{
					Material markerMaterial = markerRenderer.material;
					Color spawnColor = stage.EnemyTint;
					markerMaterial.color = spawnColor;
					if (markerMaterial.HasProperty("_BaseColor"))
						markerMaterial.SetColor("_BaseColor", spawnColor);
				}
			}
		}

		private IEnumerator SpawnCoreRoutine()
		{
			GameObject coreGameObject = pool.Spawn(stage.CoreUnit.Prefab);
			if (spawnedUnits.Contains(coreGameObject) == false)
				spawnedUnits.Add(coreGameObject); // Dispose 시 풀 반환(누수 방지). 재사용 풀 중복추적 방지.
			coreGameObject.transform.position = stageRoot.TransformPoint(stage.CorePosition);

			// 트랩#4: 스폰 직후 한 프레임 양보 — Start 시점 초기화(UnitObject 등)가 settle 된 뒤 Init.
			yield return null;

			// 이 프레임 대기 중 Dispose 됐으면(예: 웨이브 스폰 중 매치 이탈) pool/targeting/core 가 이미 null —
			// StopAllCoroutines 와 병행하는 belt-and-braces 가드(코루틴이 정지 전에 이미 재개된 경우 대비).
			if (core == null || targeting == null || pool == null)
				yield break;

			UnitObject coreUnitObject = coreGameObject.GetComponent<UnitObject>();
			if (coreUnitObject == null)
			{
				Debug.LogError($"{nameof(TowerDefenseMatch)}: {stage.CoreUnit.Prefab.name} 에 UnitObject 컴포넌트 없음 — 코어 스폰 불가.");
				yield break;
			}

			coreUnitObject.Init(stage.CoreUnit);
			// 트랩#1: 전술 코어가 유일 시전자 → 자동시전 즉시 차단(코어는 전술 없지만 스킬 SO 보유 시 대비).
			coreUnitObject.SkillHandler.AutoCastEnabled = false;

			ArenaCombatant combatant = coreUnitObject.GetComponent<ArenaCombatant>();
			if (combatant == null)
				combatant = coreUnitObject.gameObject.AddComponent<ArenaCombatant>();
			combatant.SetTeam(DEFENDER_TEAM, nextCombatantId++);

			ApplyReadability(coreUnitObject, stage.CoreTint, stage.CoreScale);
			coreGameObject.SetActive(true);

			// 트랩#2: 프리팹 내장 FSM 이 TacticDriver(추후 방어유닛)와 채널 경쟁하지 않도록 일괄 비활성.
			foreach (UnitBrain brain in coreUnitObject.GetComponents<UnitBrain>())
				brain.enabled = false;

			targeting.Register(combatant);
			targeting.RegisterObjective(combatant); // 적이 전진할 목표물로 표시 — Register 와 직교, 둘 다 필요.
			registeredCombatants.Add(combatant);

			coreCombatant = combatant;
		}

		private void Tick()
		{
			if (ticking == false || core == null)
				return;

			bool coreAlive = coreCombatant != null && coreCombatant.IsAlive;
			int aliveEnemies = CountAliveEnemies();

			TowerDefenseSignal signal = core.Tick(TimeManager.TICK, aliveEnemies, coreAlive);
			switch (signal)
			{
				case TowerDefenseSignal.WaveStarted:
					StartCoroutine(SpawnWaveRoutine());
					break;
				case TowerDefenseSignal.Victory:
					Conclude(TowerDefenseOutcome.Victory);
					break;
				case TowerDefenseSignal.Defeat:
					Conclude(TowerDefenseOutcome.Defeat);
					break;
				// WaveCleared/None = 규칙 상 상태전이 없거나 UI 연출용(추후 증분) — 셸 actuation 0.
				case TowerDefenseSignal.WaveCleared:
				case TowerDefenseSignal.None:
				default:
					break;
			}
		}

		/// <summary> WaveStarted 신호 처리 — SO 스폰 지점에 분산 스폰 후 ConfirmWaveSpawned (false-clear 차단 계약). </summary>
		private IEnumerator SpawnWaveRoutine()
		{
			waveEnemies.Clear(); // 이전 웨이브 잔여(이미 죽어 카운트 0인 엔트리) 누적 방지 — 이번 웨이브 것만 추적.

			int enemyCount = core.CurrentWaveEnemyCount;
			int spawnedCount = 0; // 실제로 UnitObject 확보 + 등록까지 끝난 수 — 이게 0 이면 ConfirmWaveSpawned 자체를 보류.

			for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
			{
				if (stage.EnemyUnit == null || stage.EnemyUnit.Prefab == null)
				{
					Debug.LogWarning($"{nameof(TowerDefenseMatch)}: stage.EnemyUnit/Prefab 미할당 — 웨이브 스폰 skip.");
					break;
				}

				Vector3 localSpawn = stage.EnemySpawnPoints != null && stage.EnemySpawnPoints.Length > 0
					? stage.EnemySpawnPoints[enemyIndex % stage.EnemySpawnPoints.Length]
					: Vector3.zero;

				GameObject enemyGameObject = pool.Spawn(stage.EnemyUnit.Prefab);
				if (spawnedUnits.Contains(enemyGameObject) == false)
					spawnedUnits.Add(enemyGameObject); // 풀이 이전 웨이브 시체를 재사용해 반환하면 같은 참조 — 중복추적 방지.
				enemyGameObject.transform.position = stageRoot.TransformPoint(localSpawn);

				// 트랩#4: 스폰 직후 한 프레임 양보.
				yield return null;

				// belt-and-braces: 대기 중 Dispose(예: 웨이브 도중 매치 이탈) 됐으면 즉시 중단.
				if (core == null || targeting == null || pool == null)
					yield break;

				UnitObject enemyUnitObject = enemyGameObject.GetComponent<UnitObject>();
				if (enemyUnitObject == null)
				{
					Debug.LogWarning($"{nameof(TowerDefenseMatch)}: {stage.EnemyUnit.Prefab.name} 에 UnitObject 컴포넌트 없음 — skip.");
					continue;
				}

				enemyUnitObject.Init(stage.EnemyUnit);
				enemyUnitObject.SkillHandler.AutoCastEnabled = false; // 트랩#1.

				ArenaCombatant enemyCombatant = enemyUnitObject.GetComponent<ArenaCombatant>();
				if (enemyCombatant == null)
					enemyCombatant = enemyUnitObject.gameObject.AddComponent<ArenaCombatant>();
				enemyCombatant.SetTeam(ATTACKER_TEAM, nextCombatantId++);

				ApplyReadability(enemyUnitObject, stage.EnemyTint, stage.EnemyScale);
				enemyGameObject.SetActive(true);

				foreach (UnitBrain brain in enemyUnitObject.GetComponents<UnitBrain>()) // 트랩#2.
					brain.enabled = false;

				TacticDriver enemyDriver = enemyUnitObject.GetComponent<TacticDriver>();
				if (enemyDriver == null)
					enemyDriver = enemyUnitObject.gameObject.AddComponent<TacticDriver>();
				enemyDriver.Initialize(stage.EnemyTactic, targeting, timeManager);
				drivers.Add(enemyDriver);

				targeting.Register(enemyCombatant);
				registeredCombatants.Add(enemyCombatant);
				waveEnemies.Add(enemyCombatant);
				spawnedCount++;
			}

			// 스폰이 실제 확인된 뒤에만 클리어 판정 활성 — 0마리 스폰인데 확인하면 코어가 aliveEnemies==0 을
			// 즉시 "격퇴"로 오인해 웨이브를 통째 스킵(false-clear 재도입) → 0이면 확인 자체를 보류하고 FastFail 로그.
			if (spawnedCount > 0)
				core.ConfirmWaveSpawned();
			else
				Debug.LogError($"{nameof(TowerDefenseMatch)}: 웨이브 적 0마리 스폰 — ConfirmWaveSpawned 보류(false-clear 차단). stage.EnemyUnit/EnemySpawnPoints 확인 필요.");
		}

		/// <summary> 살아있는 웨이브 적 수 — 죽었거나 풀 반환된(null) 엔트리는 조회 겸 정리(멱등). </summary>
		private int CountAliveEnemies()
		{
			int count = 0;
			for (int index = waveEnemies.Count - 1; index >= 0; index--)
			{
				ArenaCombatant combatant = waveEnemies[index];
				if (combatant == null)
				{
					waveEnemies.RemoveAt(index);
					continue;
				}
				if (combatant.IsAlive)
					count++;
			}
			return count;
		}

		private void Conclude(TowerDefenseOutcome outcome)
		{
			ticking = false;

			if (TimeManager.TryGetExistingInstance(out TimeManager existingTimeManager))
				existingTimeManager.RemoveCallback(Tick);

			// 종료 = 브레인(core)이 actuation 정지 권한 행사 — 전 드라이버 정지(좀비 틱 방지). ArenaMatch 와 동형.
			foreach (TacticDriver driver in drivers)
			{
				if (driver != null)
					driver.StopDriving();
			}

			if (matchEndedFired == false)
			{
				matchEndedFired = true;
				MatchEnded(outcome);
			}
		}

		/// <summary>
		/// (배치 증분 진입점) 건설 페이즈에 타워 배치 — 자원 부족 시 즉시 false(배치 거절, 상태 무변경).
		/// 유닛데이터/프리팹 유효성은 TrySpend *전* 검증(스펙#E — 자원 뗀 뒤 스폰 실패로 자원만 날리는 것 방지).
		/// 스폰 자체는 트랩#4 준수 위해 코루틴으로 지연되지만 자원 차감은 이 호출에서 동기 확정.
		/// </summary>
		public bool TryPlaceTower(Vector3 worldPosition)
		{
			if (core == null || pool == null || timeManager == null || targeting == null)
				return false;
			if (stage.TowerUnit == null || stage.TowerUnit.Prefab == null)
			{
				Debug.LogError($"{nameof(TowerDefenseMatch)}: stage.TowerUnit/Prefab 미할당 — 배치 불가(자원 미차감).");
				return false;
			}

			Vector3Int cellKey = ToCellKey(worldPosition);
			if (occupiedCells.Contains(cellKey))
				return false; // 셀 이미 점유(겹배치 차단) — 자원 무변경.

			if (core.TrySpend(stage.TowerCost) == false)
				return false;

			occupiedCells.Add(cellKey);
			StartCoroutine(SpawnDefensiveUnitRoutine(stage.TowerUnit, stage.TowerTactic, worldPosition, isHarvester: false));
			return true;
		}

		/// <summary>
		/// (배치 증분 진입점) 건설 페이즈에 채집건물 배치 — 반드시 미점유 자원 노드 반경 내에만 성립
		/// (개척 리스크 = 설계 긴장: 코어 바로 옆에 쌓아 무위험 수입을 얻는 것 차단). 노드 없으면 자원 무변경 false.
		/// 성공 시 core.AddHarvester() 로 다음 정산부터 수입 증가 + 스폰 위치를 노드 좌표로 스냅.
		/// </summary>
		public bool TryPlaceHarvester(Vector3 worldPosition)
		{
			if (core == null || pool == null || timeManager == null || targeting == null)
				return false;
			if (stage.HarvesterUnit == null || stage.HarvesterUnit.Prefab == null)
			{
				Debug.LogError($"{nameof(TowerDefenseMatch)}: stage.HarvesterUnit/Prefab 미할당 — 배치 불가(자원 미차감).");
				return false;
			}
			if (TryFindPlaceableNode(worldPosition, out int nodeIndex, out Vector3 nodeWorldPosition) == false)
				return false; // 반경 내 미점유 노드 없음 — 자원 무변경(스펙#C).

			Vector3Int cellKey = ToCellKey(nodeWorldPosition);
			if (occupiedCells.Contains(cellKey))
				return false; // 노드 셀에 이미 무언가 서 있음(겹배치 차단) — 자원 무변경.

			if (core.TrySpend(stage.HarvesterCost) == false)
				return false;

			claimedNodes.Add(nodeIndex); // TrySpend 성공 후에만 점유 확정(스펙 지시 — 실패 시 점유 안 남김).
			occupiedCells.Add(cellKey);
			StartCoroutine(SpawnDefensiveUnitRoutine(stage.HarvesterUnit, null, nodeWorldPosition, isHarvester: true));
			return true;
		}

		/// <summary>
		/// worldPosition 반경 NodeCaptureRadius 내 가장 가까운 *미점유* 자원 노드를 찾는다.
		/// 배치 UI 가 유효/무효 프리뷰를 보여줄 때도 이 메서드로 규칙 중복 없이 재사용(TryPlaceHarvester 와 동일 판정).
		/// </summary>
		public bool TryFindPlaceableNode(Vector3 worldPosition, out int nodeIndex, out Vector3 nodeWorldPosition)
		{
			nodeIndex = -1;
			nodeWorldPosition = Vector3.zero;

			if (stage == null || stageRoot == null || stage.ResourceNodePositions == null)
				return false;

			float captureRadiusSqr = stage.NodeCaptureRadius * stage.NodeCaptureRadius;
			int bestIndex = -1;
			float bestSqrDistance = float.MaxValue;

			for (int index = 0; index < stage.ResourceNodePositions.Length; index++)
			{
				if (claimedNodes.Contains(index))
					continue;

				Vector3 candidateWorldPosition = stageRoot.TransformPoint(stage.ResourceNodePositions[index]);
				float sqrDistance = (candidateWorldPosition - worldPosition).sqrMagnitude;
				if (sqrDistance > captureRadiusSqr)
					continue;
				if (sqrDistance < bestSqrDistance)
				{
					bestSqrDistance = sqrDistance;
					bestIndex = index;
				}
			}

			if (bestIndex < 0)
				return false;

			nodeIndex = bestIndex;
			nodeWorldPosition = stageRoot.TransformPoint(stage.ResourceNodePositions[bestIndex]);
			return true;
		}

		/// <summary>
		/// worldPosition 이 속한 셀이 이미 배치물로 점유됐는지 — 배치 UI 프리뷰가 유효/무효 색을
		/// 이 메서드로 판정(TryPlaceTower/TryPlaceHarvester 내부 점유 판정과 동일 규칙 재사용).
		/// </summary>
		public bool IsCellOccupied(Vector3 worldPosition)
		{
			return occupiedCells.Contains(ToCellKey(worldPosition));
		}

		// 셀 키 = FloorToInt(worldPosition), y 는 0 고정(층 무관 단일 격자 — 위로 쌓기 원천 차단).
		private static Vector3Int ToCellKey(Vector3 worldPosition)
		{
			Vector3Int cell = Vector3Int.FloorToInt(worldPosition);
			cell.y = 0;
			return cell;
		}

		private IEnumerator SpawnDefensiveUnitRoutine(Unit unitData, TacticProgram tactic, Vector3 worldPosition, bool isHarvester)
		{
			if (unitData == null || unitData.Prefab == null)
			{
				Debug.LogError($"{nameof(TowerDefenseMatch)}: 배치 유닛 데이터/Prefab 미할당 — 스폰 불가(자원은 이미 차감됨).");
				yield break;
			}

			GameObject unitGameObject = pool.Spawn(unitData.Prefab);
			if (spawnedUnits.Contains(unitGameObject) == false)
				spawnedUnits.Add(unitGameObject);
			unitGameObject.transform.position = worldPosition;

			// 트랩#4: 스폰 직후 한 프레임 양보.
			yield return null;

			// belt-and-braces: 대기 중 Dispose 됐으면 즉시 중단(자원은 이미 차감됐지만 좀비 spawn 은 차단).
			if (core == null || targeting == null || pool == null)
				yield break;

			UnitObject unitObject = unitGameObject.GetComponent<UnitObject>();
			if (unitObject == null)
			{
				Debug.LogWarning($"{nameof(TowerDefenseMatch)}: {unitData.Prefab.name} 에 UnitObject 컴포넌트 없음 — skip.");
				yield break;
			}

			unitObject.Init(unitData);
			unitObject.SkillHandler.AutoCastEnabled = false; // 트랩#1.

			ArenaCombatant combatant = unitObject.GetComponent<ArenaCombatant>();
			if (combatant == null)
				combatant = unitObject.gameObject.AddComponent<ArenaCombatant>();
			combatant.SetTeam(DEFENDER_TEAM, nextCombatantId++);

			ApplyReadability(unitObject,
				isHarvester ? stage.HarvesterTint : stage.TowerTint,
				isHarvester ? stage.HarvesterScale : stage.TowerScale);
			unitGameObject.SetActive(true);

			foreach (UnitBrain brain in unitObject.GetComponents<UnitBrain>()) // 트랩#2.
				brain.enabled = false;

			if (tactic != null)
			{
				TacticDriver driver = unitObject.GetComponent<TacticDriver>();
				if (driver == null)
					driver = unitObject.gameObject.AddComponent<TacticDriver>();
				driver.Initialize(tactic, targeting, timeManager);
				drivers.Add(driver);
			}

			targeting.Register(combatant);
			registeredCombatants.Add(combatant);

			if (isHarvester)
				core.AddHarvester(); // 채집건물 = 실제 가동(스폰 확정) 시점에만 수입 반영.
		}

		/// <summary>
		/// 매치 생명주기 정리 — 단일 경로 + *재진입 가능*(ArenaMatch.Dispose 와 동형): 틱 콜백 해제 +
		/// 드라이버 정지/클리어 + targeting unregister + 스폰 유닛 풀 반환 + 지면 파괴 + 진입 상태 리셋.
		/// 컬렉션 비움으로 멱등(Dispose→Destroy→OnDestroy 이중 호출 무해).
		/// StopAllCoroutines 를 최우선 호출 — 진행 중이던 스폰 코루틴(웨이브/배치)이 pool/targeting/core 필드
		/// null화 이후 재개돼 NRE 나는 것을 원천 차단(스펙#A belt). 코루틴 내부 yield 직후 null 가드는 braces.
		/// </summary>
		public void Dispose()
		{
			StopAllCoroutines();
			ticking = false;

			if (TimeManager.TryGetExistingInstance(out TimeManager existingTimeManager))
				existingTimeManager.RemoveCallback(Tick);

			foreach (TacticDriver driver in drivers)
			{
				if (driver != null)
					driver.StopDriving();
			}
			drivers.Clear();

			if (targeting != null)
			{
				foreach (ICombatant combatant in registeredCombatants)
					targeting.Unregister(combatant);
			}
			registeredCombatants.Clear();
			waveEnemies.Clear();
			claimedNodes.Clear();
			occupiedCells.Clear();

			if (ObjectPoolManager.TryGetExistingInstance(out ObjectPoolManager existingPool))
			{
				foreach (GameObject unit in spawnedUnits)
					ReleaseUnit(existingPool, unit);
			}
			spawnedUnits.Clear();

			if (stageRoot != null)
			{
				for (int childIndex = stageRoot.childCount - 1; childIndex >= 0; childIndex--)
					Destroy(stageRoot.GetChild(childIndex).gameObject);
			}

			// 재진입 — 다음 Begin() 이 새 매치를 돌릴 수 있게 진입 상태 리셋.
			core = null;
			coreCombatant = null;
			targeting = null;
			pool = null;
			timeManager = null;
			started = false;
		}

		private void OnDestroy()
		{
			Dispose();
		}
	}
}
