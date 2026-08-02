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

		// 격파 보상을 이미 지급한 마수(CombatantId) — 죽은 개체는 여러 틱 동안 목록에 남으므로 중복 지급 차단.
		// 오브젝트 풀이 같은 GameObject 를 되돌려주기 때문에 참조가 아니라 매치 고유 id 로 센다.
		private readonly HashSet<int> bountyPaidEnemyIds = new();

		// 가동 중인 채집 인형 위치 — 정산 때 각자 머리 위에 벌어들인 액수를 띄운다.
		// 숫자가 *어디서* 나오는지 안 보이면 채집 인형은 그냥 서 있는 장식으로 읽힌다.
		private readonly List<Transform> harvesterTransforms = new();
		// 이 채집 인형이 바깥 노드에 섰나 — 정수/자원 중 무엇을 내는지 결정.
		private readonly Dictionary<Vector3, bool> harvesterIsOuter = new();

		// 이번 매치의 판 — 절차 생성이면 layout 이 정본, 끄면 null 이고 스테이지 SO 의 고정 레이아웃을 쓴다.
		// 아래 active* 목록이 *둘을 하나로 합친 단일 출처* — 매치 본문은 어느 쪽인지 신경 쓰지 않는다.
		// 시야 — 내 건물이 밝힌 만큼만 보인다. 건물은 안 움직이므로 *지어질 때만* 다시 계산한다.
		private TowerDefenseVision vision;
		private TowerDefenseFogView fogView;
		private readonly List<TowerDefenseVision.Source> visionSources = new();
		private readonly List<TowerDefenseVision.Source> scaledVisionSources = new();

		// 내가 세운 벽. 암반(생성된 지형)과 합쳐 「통행 불가」 하나로 본다 —
		// 길찾기·표시·배치가 각자 다른 기준을 쓰면 화면과 규칙이 갈라진다.
		private readonly HashSet<Vector2Int> wallCells = new();
		private Transform laneRoot;

		private TowerDefenseMapLayout mapLayout;
		private TowerDefenseFlowField flowField;
		private ITacticNavigator flowNavigator;
		private readonly List<Vector3> activeSpawnPoints = new();
		private readonly List<Vector3> activeNodePositions = new();
		private readonly List<float> activeNodeIncomeMultipliers = new();
		// 노드 등급 — 바깥 노드는 정수를 낸다(안쪽은 자원). 「멀리 나가야 강해진다」의 근거.
		private readonly List<bool> activeNodeIsOuter = new();
		private Vector3 activeCorePosition;
		private float activeGroundWidth;
		private float activeGroundLength;

		// 이번(또는 다음) 웨이브의 마수 구성 — 원소 = EnemyArchetypes 인덱스. 결정론이라 화면 예고와 실제 스폰이 같다.
		private readonly List<int> waveComposition = new();

		// 격파 보상은 종류마다 다르다(단단한 놈일수록 크게) — 죽은 뒤엔 어떤 종류였는지 알 수 없으므로
		// 스폰 시점에 CombatantId → 보상액을 기록해 둔다.
		private readonly Dictionary<int, int> enemyBountyById = new();

		// 셀 점유(TASK-WM-194 증분3) — 타워/채집건물 배치는 한 셀에 하나만(겹배치 차단). 키 = FloorToInt 셀(y=0 고정,
		// 층 무관 단일 격자). claimedNodes(자원 노드 자체 점유)와 직교 — 이건 "그 좌표에 뭔가 이미 서 있나"만 본다.
		private readonly HashSet<Vector3Int> occupiedCells = new();

		public event Action<TowerDefenseOutcome> MatchEnded = delegate { };

		// 웨이브 자동 진행 여부 — 플레이 중 토글되므로 코어(진행 중)와 필드(다음 매치)를 함께 갱신한다.
		// 재시작해도 방금 고른 방식이 유지돼야 한다(설정을 매번 다시 고르게 만들지 않는다).
		private bool autoAdvanceWaves = true;
		private bool waveModeInitialized;

		public bool AutoAdvanceWaves
		{
			get => autoAdvanceWaves;
			set
			{
				autoAdvanceWaves = value;
				if (core != null)
					core.AutoAdvance = value;
			}
		}

		/// <summary> 다음 웨이브 호출(수동 진행 / 자동에서도 즉시 시작). 건설 국면이 아니면 false. </summary>
		public bool RequestNextWave() => core != null && core.RequestNextWave();

		/// <summary> 수동 진행에서 호출이 예약된 상태인지 — HUD 표시용. </summary>
		public bool IsNextWaveRequested => core != null && core.IsNextWaveRequested;

		/// <summary>
		/// 이번 웨이브 적 추적 목록(읽기 전용) — **진단용**. "다 잡은 것 같은데 안 넘어간다"는
		/// 곧 "코어가 세는 생존자와 화면에서 보이는 것이 다르다"는 뜻이라, 무엇이 살아 있다고
		/// 집계되는지를 좌표·체력까지 직접 볼 수 있어야 원인을 짚는다(추측 금지).
		/// </summary>
		public IReadOnlyList<ArenaCombatant> WaveEnemies => waveEnemies;

		/// <summary>
		/// 코어가 보는 생존 적 수 — HUD 표시 + 진단 대조용. 매 프레임 읽히므로 목록을 건드리지 않는
		/// **순수 집계**(정리는 코어 틱의 CountAliveEnemies 가 담당 — 표시가 상태를 바꾸면 안 된다).
		/// </summary>
		public int AliveEnemyCount
		{
			get
			{
				int count = 0;
				foreach (ArenaCombatant combatant in waveEnemies)
				{
					if (combatant != null && combatant.IsAlive)
						count++;
				}
				return count;
			}
		}

		/// <summary>
		/// 진행 중인 스테이지 데이터(읽기 전용) — 검증 하네스가 좌표·수치를 **정본에서 읽게** 한다.
		/// 하네스에 좌표를 박아두면 레이아웃을 옮기는 순간 검사가 조용히 무의미해진다(항상 거절만 확인).
		/// </summary>
		public TowerDefenseStageSO Stage => stage;

		/// <summary> 코어 참가자(진단용) — 적이 코어를 실제로 때리고 있는지 체력으로 확인한다. </summary>
		public ArenaCombatant CoreCombatant => coreCombatant;

		/// <summary> 매치에 등록된 전 참가자(진단용) — 수비 유닛 생존 여부 확인. </summary>
		public IReadOnlyList<ICombatant> RegisteredCombatants => registeredCombatants;

		public int Resource => core != null ? core.Resource : 0;
		public int WaveIndex => core != null ? core.WaveIndex : 0;
		public TowerDefensePhase Phase => core != null ? core.Phase : TowerDefensePhase.Prepare;
		public TowerDefenseOutcome Outcome => core != null ? core.Outcome : TowerDefenseOutcome.InProgress;
		public float PrepareRemaining => core != null ? core.PrepareRemaining : 0f;

		/// <summary> 다음 정산액 + 가동 채집 인형 수 — 「채집 인형이 뭐 하는 놈인지」를 화면이 말하는 근거 숫자. </summary>
		public int NextWaveIncome => core != null ? core.NextWaveIncome : 0;
		public int Essence => core != null ? core.Essence : 0;
		public int NextWaveEssence => core != null ? core.NextWaveEssence : 0;
		public int HarvesterCount => core != null ? core.HarvesterCount : 0;

		/// <summary> 프로그래매틱 시작(런처/모드 진입용) — stage·stageRoot 주입 후 Begin. </summary>
		public void Begin(TowerDefenseStageSO stageConfig, Transform root)
		{
			stage = stageConfig;
			stageRoot = root;

			// 진행 방식 기본값은 스테이지가 정하지만, 플레이어가 한 번 고르면 그 선택이 재시작을 넘어 유지된다.
			if (waveModeInitialized == false && stage != null)
			{
				autoAdvanceWaves = stage.AutoAdvanceWavesDefault;
				waveModeInitialized = true;
			}

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

			PrepareLayout(); // 판을 먼저 확정 — 지면·노드·스폰·길안내가 전부 여기서 파생된다.
			BuildGround();

			targeting = new TargetingSystem();
			core = new TowerDefenseCore(stage.Rules)
			{
				AutoAdvance = autoAdvanceWaves,
				FirstAutoWave = stage.ManualFirstWave ? 1 : 0,
			};
			nextCombatantId = 0;
			matchEndedFired = false;
			claimedNodes.Clear(); // 재진입 — 지난 매치의 노드 점유가 새 매치로 새는 것 방지.
			bountyPaidEnemyIds.Clear();
			enemyBountyById.Clear();
			harvesterTransforms.Clear();
			harvesterIsOuter.Clear();
			LabCount = 0;
			TrapsSpent = 0;
			speedStep = 1;
			lastRunningStep = 1;
			ApplySpeed();
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

		/// <summary>
		/// 이번 매치의 판 확정 — 절차 생성이면 생성기를 돌리고, 아니면 스테이지 SO 의 고정값을 그대로 담는다.
		/// 어느 쪽이든 결과는 같은 active* 목록이라 매치 본문에는 분기가 없다(분기를 여기저기 흩으면
		/// 언젠가 한 곳이 옛 경로를 보고 조용히 어긋난다).
		/// </summary>
		private void PrepareLayout()
		{
			activeSpawnPoints.Clear();
			activeNodePositions.Clear();
			activeNodeIncomeMultipliers.Clear();
			activeNodeIsOuter.Clear();
			mapLayout = null;
			flowField = null;
			flowNavigator = null;

			if (stage.UseProceduralMap == false)
			{
				activeCorePosition = stage.CorePosition;
				activeGroundWidth = stage.GroundWidth;
				activeGroundLength = stage.GroundLength;

				if (stage.EnemySpawnPoints != null)
					activeSpawnPoints.AddRange(stage.EnemySpawnPoints);
				if (stage.ResourceNodePositions != null)
				{
					foreach (Vector3 nodePosition in stage.ResourceNodePositions)
					{
						activeNodePositions.Add(nodePosition);
						activeNodeIncomeMultipliers.Add(1f);
						activeNodeIsOuter.Add(false);
					}
				}
				return;
			}

			TowerDefenseMapParameters parameters = stage.MapParameters;
			if (stage.RandomizeSeedEachMatch)
				parameters.Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);

			mapLayout = TowerDefenseMapGenerator.Generate(parameters);

			activeCorePosition = mapLayout.CorePosition;
			activeGroundWidth = mapLayout.GroundWidth;
			activeGroundLength = mapLayout.GroundLength;
			activeSpawnPoints.AddRange(mapLayout.EnemySpawnPoints);
			foreach (TowerDefenseResourceNodeSpot node in mapLayout.ResourceNodes)
			{
				activeNodePositions.Add(node.Position);
				activeNodeIncomeMultipliers.Add(node.IncomeMultiplier);
				activeNodeIsOuter.Add(node.Tier == TowerDefenseNodeTier.Outer);
			}

			// 길 안내판 — 암반이 생긴 순간 직선 이동은 벽에 박힌다(웨이브가 영원히 안 끝나는 그 사고).
			wallCells.Clear();
			flowField = new TowerDefenseFlowField(
				mapLayout.Width, mapLayout.Length, mapLayout.CoreCell, IsPathBlocked);
			flowNavigator = new TowerDefenseFlowNavigator(
				mapLayout, flowField, stageRoot, stage.GroundCellSize * 2f);

			vision = new TowerDefenseVision(mapLayout.Width, mapLayout.Length);
			visionSources.Clear();

			Debug.Log($"{nameof(TowerDefenseMatch)}: 판 생성 seed={mapLayout.Seed} "
				+ $"암반={mapLayout.ObstacleCells.Count}칸 노드={mapLayout.ResourceNodes.Count} 스폰={mapLayout.EnemySpawnPoints.Count}");
		}

		/// <summary> 통행 불가 판정 — 생성된 암반 + 내가 세운 벽. 길찾기·표시가 같은 함수를 본다. </summary>
		private bool IsPathBlocked(Vector2Int cell)
		{
			return mapLayout.IsBlocked(cell) || wallCells.Contains(cell);
		}

		/// <summary>
		/// 그 칸의 포탑을 한 단계 올린다 — 같은 종류일 때만. 값은 기본 비용 × 다음 단계.
		/// 최대 단계거나 다른 종류면 아무 일도 안 일어난다(자원 무변경).
		/// </summary>
		private bool TryUpgradeTowerAt(Vector3Int cellKey, int towerIndex)
		{
			TowerDefenseTowerArchetype archetype = TowerArchetypeAt(towerIndex);
			if (archetype == null)
				return false;

			foreach (GameObject unit in spawnedUnits)
			{
				if (unit == null || unit.activeInHierarchy == false)
					continue;
				if (ToCellKey(unit.transform.position) != cellKey)
					continue;

				TowerDefenseWeapon weapon = unit.GetComponent<TowerDefenseWeapon>();
				if (weapon == null || weapon.Cost != archetype.Cost)
					return false; // 다른 종류(또는 포탑이 아님) — 겹배치 차단 그대로.
				if (weapon.Level >= archetype.MaxLevel)
					return false;

				// 승급도 정수 — 「지금 더 짓기(자원)」 vs 「있는 걸 키우기(정수)」가 서로 다른 통장을 쓴다.
				int upgradeCost = Mathf.Max(1, Mathf.RoundToInt(stage.UpgradeEssenceCost * (weapon.Level + 1) * 0.5f));
				if (core.TrySpendEssence(upgradeCost) == false)
					return false;

				weapon.TryUpgrade();
				PopWorldText("Lv." + weapon.Level, unit.transform.position, TextType.Exp);
				RefreshTowerRing(unit, archetype, weapon.Level);
				return true;
			}

			return false;
		}

		// 승급하면 사거리가 늘므로 화면의 원도 같이 자라야 한다 — 안 그러면 원이 거짓말한다.
		private void RefreshTowerRing(GameObject unit, TowerDefenseTowerArchetype archetype, int level)
		{
			TowerDefenseRing ring = unit.GetComponentInChildren<TowerDefenseRing>();
			if (ring != null)
				ring.SetRadius(archetype.Range * (1f + (level - 1) * archetype.UpgradeGrowth));
		}

		/// <summary>
		/// 함정 깔기 — 밟으면 터진다. 길목과 직결되므로 벽(길 그리기)의 짝.
		/// 통행을 막지 않으므로 길 검사가 필요 없다(그래서 벽보다 훨씬 가볍다).
		/// </summary>
		public bool TryPlaceTrap(Vector3 worldPosition)
		{
			if (core == null || mapLayout == null || stageRoot == null)
				return false;

			Vector3Int cellKey = ToCellKey(worldPosition);
			if (occupiedCells.Contains(cellKey) || IsObstacleAt(worldPosition))
				return false;

			if (core.TrySpend(stage.TrapCost) == false)
				return false;

			occupiedCells.Add(cellKey);
			BuildTrapObject(worldPosition, cellKey);
			return true;
		}

		private void BuildTrapObject(Vector3 worldPosition, Vector3Int cellKey)
		{
			float cellSize = stage.GroundCellSize;
			GameObject trapObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
			trapObject.name = "Trap";
			Destroy(trapObject.GetComponent<Collider>()); // 밟는 판정은 거리로 한다 — 물리를 끼우면 마수가 걸린다.
			trapObject.transform.SetParent(stageRoot, false);
			trapObject.transform.position = worldPosition + new Vector3(0f, 0.05f, 0f);
			trapObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
			trapObject.transform.localScale = Vector3.one * cellSize * 0.85f;

			Renderer trapRenderer = trapObject.GetComponent<Renderer>();
			if (trapRenderer != null)
			{
				Material trapMaterial = new Material(trapRenderer.sharedMaterial);
				MakeTransparent(trapMaterial);
				Color trapColor = stage.TrapTint;
				trapColor.a = 0.75f;
				trapMaterial.color = trapColor;
				if (trapMaterial.HasProperty("_BaseColor"))
					trapMaterial.SetColor("_BaseColor", trapColor);
				trapRenderer.sharedMaterial = trapMaterial;
			}

			TowerDefenseTrap trap = trapObject.AddComponent<TowerDefenseTrap>();
			trap.Configure(waveEnemies, stage.TrapDamage, stage.TrapCharges, stage.TrapRadius,
				spent =>
				{
					// 다 쓴 함정은 자리를 비워준다 — 안 비우면 그 칸이 영영 죽는다.
					occupiedCells.Remove(cellKey);
					TrapsSpent++;
					if (spent != null)
						Destroy(spent.gameObject);
				});
		}

		/// <summary> 다 쓰고 사라진 함정 수 — 검증·통계용. </summary>
		public int TrapsSpent { get; private set; }

		/// <summary>
		/// 벽 세우기 — 마수의 길을 *내가 그린다*. 장르적으로 여기가 가장 큰 전환점이다:
		/// 「어디에 지을까」가 「길을 어떻게 낼까」로 승격된다.
		///
		/// ★ 단 하나의 불변식: **길을 완전히 막을 수는 없다.** 모든 출현 지점에서 코어까지 가는 길이
		///   남아야 한다. 안 그러면 마수가 벽 앞에 굳고 웨이브가 영원히 안 끝난다(이미 겪은 사고).
		///   그래서 *먼저 세워보고 길이 남는지 확인한 뒤* 확정한다 — 안 되면 원상복구하고 거절.
		/// </summary>
		public bool TryPlaceWall(Vector3 worldPosition)
		{
			if (core == null || mapLayout == null || stageRoot == null)
				return false;

			Vector3Int cellKey = ToCellKey(worldPosition);
			if (occupiedCells.Contains(cellKey))
				return false;

			Vector2Int cell = mapLayout.WorldToCell(stageRoot.InverseTransformPoint(worldPosition));
			if (mapLayout.IsInside(cell) == false || IsPathBlocked(cell))
				return false;

			wallCells.Add(cell);
			if (RebuildPathing() == false)
			{
				wallCells.Remove(cell); // 길이 끊긴다 — 없던 일로.
				RebuildPathing();
				Debug.Log($"{nameof(TowerDefenseMatch)}: 벽 거절 — 여길 막으면 마수가 코어까지 갈 길이 없다.");
				return false;
			}

			if (core.TrySpend(stage.WallCost) == false)
			{
				wallCells.Remove(cell);
				RebuildPathing();
				return false;
			}

			occupiedCells.Add(cellKey);
			BuildWallObject(cell);
			return true;
		}

		/// <summary>
		/// 길 다시 계산 + 표시 갱신. 모든 출현 지점에서 코어까지 갈 수 있으면 true.
		/// 흐름장이 이미 있어 재계산이 싸다 — 벽을 세울 때마다 전부 다시 그려도 부담이 없다.
		/// </summary>
		private bool RebuildPathing()
		{
			flowField = new TowerDefenseFlowField(
				mapLayout.Width, mapLayout.Length, mapLayout.CoreCell, IsPathBlocked);
			flowNavigator = new TowerDefenseFlowNavigator(
				mapLayout, flowField, stageRoot, stage.GroundCellSize * 2f);

			foreach (Vector3 spawnLocal in activeSpawnPoints)
			{
				if (flowField.IsReachable(mapLayout.WorldToCell(spawnLocal)) == false)
					return false;
			}

			// 이미 걷고 있는 마수도 새 길을 따라야 한다 — 안 그러면 벽 안쪽에 갇힌다.
			foreach (TacticDriver driver in drivers)
			{
				if (driver != null)
					driver.Navigator = flowNavigator;
			}

			BuildPathLanes();
			return true;
		}

		private void BuildWallObject(Vector2Int cell)
		{
			float cellSize = mapLayout.CellSize;
			GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
			wall.name = "Wall";
			wall.transform.SetParent(stageRoot, false);
			wall.transform.localPosition = mapLayout.CellToWorld(cell) + new Vector3(0f, cellSize * 0.35f, 0f);
			wall.transform.localScale = new Vector3(cellSize * 0.94f, cellSize * 0.7f, cellSize * 0.94f);

			Renderer wallRenderer = wall.GetComponent<Renderer>();
			if (wallRenderer == null)
				return;

			Color wallColor = stage.WallTint;
			Material wallMaterial = new Material(wallRenderer.sharedMaterial);
			wallMaterial.color = wallColor;
			if (wallMaterial.HasProperty("_BaseColor"))
				wallMaterial.SetColor("_BaseColor", wallColor);
			wallRenderer.sharedMaterial = wallMaterial;
		}

		/// <summary>
		/// 마수가 실제로 걸어올 길을 바닥에 깐다 — 「여기가 길목」이 한눈에 보여야 배치가 판단이 된다.
		///
		/// ★ 길 안내판(흐름장)을 그대로 따라가며 칠하므로 *표시와 실제 이동이 같은 출처*다.
		///   보기용으로 따로 그리면 언젠가 반드시 어긋나고, 그때 화면은 플레이어를 속인다.
		/// 여러 출현 지점의 길이 겹치는 칸일수록 진하게 — 겹치는 곳이 곧 최고의 포탑 자리다.
		/// </summary>
		private void BuildPathLanes()
		{
			if (mapLayout == null || flowField == null)
				return;

			// 벽을 세울 때마다 다시 그리므로 지난 표시를 먼저 치운다(안 치우면 옛 길이 겹쳐 남는다).
			if (laneRoot != null)
				Destroy(laneRoot.gameObject);
			laneRoot = new GameObject("PathLanes").transform;
			laneRoot.SetParent(stageRoot, false);

			Dictionary<Vector2Int, int> laneWeight = new();
			foreach (Vector3 spawnLocal in activeSpawnPoints)
			{
				Vector2Int cell = mapLayout.WorldToCell(spawnLocal);
				int guard = mapLayout.Width * mapLayout.Length;

				while (guard-- > 0 && cell != flowField.GoalCell)
				{
					laneWeight.TryGetValue(cell, out int weight);
					laneWeight[cell] = weight + 1;

					if (flowField.TryGetNextCell(cell, out Vector2Int next) == false)
						break;
					cell = next;
				}
			}

			float cellSize = mapLayout.CellSize;
			foreach ((Vector2Int cell, int weight) in laneWeight)
			{
				GameObject lane = GameObject.CreatePrimitive(PrimitiveType.Quad);
				lane.name = "PathLane";
				Destroy(lane.GetComponent<Collider>()); // 표시용 — 배치 레이캐스트를 가로채면 안 된다.
				lane.transform.SetParent(laneRoot, false);
				lane.transform.localPosition = mapLayout.CellToWorld(cell) + new Vector3(0f, 0.03f, 0f);
				lane.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
				lane.transform.localScale = Vector3.one * cellSize * 0.92f;

				Renderer laneRenderer = lane.GetComponent<Renderer>();
				if (laneRenderer == null)
					continue;

				// 겹칠수록 진하게(1갈래 = 옅게, 여러 갈래 = 뚜렷하게).
				// 어두운 바닥 위에서 확실히 튀는 밝기까지 올린다 — 길이 안 보이면 이 기능은 없는 것과 같다.
				float intensity = Mathf.Clamp01(0.6f + (weight - 1) * 0.2f);
				Color laneColor = new Color(1f, 0.74f, 0.28f, intensity);
				Material laneMaterial = new Material(laneRenderer.sharedMaterial);
				MakeTransparent(laneMaterial);
				laneMaterial.color = laneColor;
				if (laneMaterial.HasProperty("_BaseColor"))
					laneMaterial.SetColor("_BaseColor", laneColor);
				laneRenderer.sharedMaterial = laneMaterial;
				laneRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			}
		}

		/// <summary> URP Lit 재질을 반투명으로 — 불투명 그대로면 길 표시가 바닥을 덮어버린다. </summary>
		private static void MakeTransparent(Material material)
		{
			material.SetFloat("_Surface", 1f); // 1 = Transparent
			material.SetOverrideTag("RenderType", "Transparent");
			material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
			material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
			material.SetInt("_ZWrite", 0);
			material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
			material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
		}

		/// <summary>
		/// 암반 세우기 — 눈에 보이고(길목이 읽혀야 배치 판단이 생김) 실제로 막는다(콜라이더).
		/// 칸 하나당 상자 하나 = 셀 격자와 정확히 일치 → 「저 칸은 못 지나간다」가 화면과 규칙에서 같다.
		/// </summary>
		private void BuildObstacles()
		{
			if (mapLayout == null)
				return;

			float cell = mapLayout.CellSize;
			// 어두운 바닥 위 어두운 바위 = 안 보인다(라이브 스크린샷 실증 — 암반과 지면이 구분 안 됨).
			// 벽이라는 걸 알려면 바닥보다 확실히 밝고 따뜻해야 하고, 높이도 눈에 띄게 서 있어야 한다.
			Color rockColor = new Color(0.62f, 0.55f, 0.47f, 1f);
			Material rockMaterial = null;

			foreach (Vector2Int obstacleCell in mapLayout.ObstacleCells)
			{
				GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
				rock.name = "Rock";
				rock.transform.SetParent(stageRoot, false);
				// 높은 벽은 부감 시점에서 뒤쪽 바닥을 가려 길 표시를 통째로 먹는다(스크린샷 실증).
				// 낮은 능선이면 「막힌 칸」은 그대로 읽히면서 길이 보인다.
				rock.transform.localPosition = mapLayout.CellToWorld(obstacleCell) + new Vector3(0f, cell * 0.3f, 0f);
				rock.transform.localScale = new Vector3(cell, cell * 0.6f, cell);

				// ★ 충돌 상자를 칸보다 살짝 작게 — 길찾기는 1칸 통로를 정상 경로로 주는데, 몸통이 칸을 꽉 채우면
				//   그 통로에서 물리적으로 낀다(라이브: 마수 1기가 40초 가까이 도착 못 함). 보이는 크기는 그대로.
				BoxCollider rockCollider = rock.GetComponent<BoxCollider>();
				if (rockCollider != null)
					rockCollider.size = new Vector3(0.82f, 1f, 0.82f);

				Renderer rockRenderer = rock.GetComponent<Renderer>();
				if (rockRenderer == null)
					continue;

				// 재질 1장을 전부가 공유 — 칸마다 새 재질을 만들면 수백 장이 된다.
				if (rockMaterial == null)
				{
					rockMaterial = new Material(rockRenderer.sharedMaterial);
					rockMaterial.color = rockColor;
					if (rockMaterial.HasProperty("_BaseColor"))
						rockMaterial.SetColor("_BaseColor", rockColor);
				}
				rockRenderer.sharedMaterial = rockMaterial;
			}
		}

		/// <summary> 지면(바닥) 런타임 생성 — RectangleArenaMap.Build 와 동형(Plane 스케일, SO 수치 그대로). </summary>
		private void BuildGround()
		{
			GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
			ground.name = "Ground";
			ground.transform.SetParent(stageRoot, false);
			ground.transform.localPosition = Vector3.zero;
			// Plane = 10x10 유닛 @ scale 1 → GroundWidth/GroundLength 에 맞춰 스케일.
			ground.transform.localScale = new Vector3(activeGroundWidth / 10f, 1f, activeGroundLength / 10f);

			ApplyGroundCheckerboard(ground);
			BuildObstacles();
			BuildPathLanes();
			if (vision != null)
			{
				fogView = TowerDefenseFogView.Create(
					stageRoot, mapLayout.Width, mapLayout.Length, activeGroundWidth, activeGroundLength, 0.9f);
				RefreshVision();
			}
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
			Vector2 tiling = new Vector2(activeGroundWidth / cell, activeGroundLength / cell);

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
		/// <summary>
		/// 종류별 체력·속도 적용 — 기반 유닛 스탯에 배수를 씌운다. 새 유닛 에셋 없이 「단단한 놈/빠른 놈」이
		/// 성립하는 지점. HP_MAX_STAT(기반)까지 같이 올려야 이후 스탯 재계산이 원래 값으로 되돌리지 않는다.
		/// 리스는 ApplyReadability 가 이미 잡아뒀다(같은 스폰 경로) — 반납 시 원본 스탯으로 복원된다.
		/// </summary>
		private static void ApplyArchetypeStats(UnitObject unitObject, TowerDefenseEnemyArchetype archetype)
		{
			if (unitObject == null || archetype == null)
				return;

			if (Mathf.Approximately(archetype.HealthMultiplier, 1f) == false)
			{
				int scaledMax = Mathf.Max(1, Mathf.RoundToInt(unitObject.UnitStat[UnitStatType.HP_MAX] * archetype.HealthMultiplier));
				unitObject.UnitStat[UnitStatType.HP_MAX_STAT] = scaledMax;
				unitObject.UnitStat[UnitStatType.HP_MAX] = scaledMax;
				unitObject.UnitStat[UnitStatType.HP_CUR] = scaledMax;
			}

			if (Mathf.Approximately(archetype.SpeedMultiplier, 1f) == false)
			{
				int scaledSpeed = Mathf.Max(1, Mathf.RoundToInt(unitObject.UnitStat[UnitStatType.MOVEMENT_SPEED] * archetype.SpeedMultiplier));
				unitObject.UnitStat[UnitStatType.MOVEMENT_SPEED] = scaledSpeed;
			}
		}

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
			foreach (Vector3 localPosition in activeNodePositions)
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
			foreach (Vector3 localPosition in activeSpawnPoints)
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
			coreGameObject.transform.position = stageRoot.TransformPoint(activeCorePosition);

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
			AddVisionSource(coreGameObject.transform.position, stage.CoreVisionRadius);
		}

		private void Tick()
		{
			if (ticking == false || core == null)
				return;

			CullEscapedEnemies(); // 무대 밖 개체가 웨이브를 영원히 붙잡지 못하게 — 집계 *전에* 정리.
			CullLeakedEnemies();  // 목표에 닿은 마수는 사라지고 목숨이 준다(유출제).
			ApplyEnemyVisibility(); // 안 보이는 마수는 화면에서도 지운다(규칙과 그림이 같아야 한다).
			PayKillBounties();    // 격파 즉시 보상 — 웨이브 정산만 있으면 교전 중엔 아무 보상도 안 온다.

			bool coreAlive = coreCombatant != null && coreCombatant.IsAlive;
			int aliveEnemies = CountAliveEnemies();

			TowerDefenseSignal signal = core.Tick(TimeManager.TICK, aliveEnemies, coreAlive);
			switch (signal)
			{
				case TowerDefenseSignal.WaveStarted:
					RefreshVision(); // 어스름 진입/이탈이 시야에 즉시 반영돼야 한다.
					StartCoroutine(SpawnWaveRoutine());
					break;
				case TowerDefenseSignal.Victory:
					Conclude(TowerDefenseOutcome.Victory);
					break;
				case TowerDefenseSignal.Defeat:
					Conclude(TowerDefenseOutcome.Defeat);
					break;
				case TowerDefenseSignal.WaveCleared:
					ShowIncomeBreakdown();
					break;
				// None = 규칙 상 상태전이 없음 — 셸 actuation 0.
				case TowerDefenseSignal.None:
				default:
					break;
			}
		}

		/// <summary> WaveStarted 신호 처리 — SO 스폰 지점에 분산 스폰 후 ConfirmWaveSpawned (false-clear 차단 계약). </summary>
		private IEnumerator SpawnWaveRoutine()
		{
			waveEnemies.Clear(); // 이전 웨이브 잔여(이미 죽어 카운트 0인 엔트리) 누적 방지 — 이번 웨이브 것만 추적.

			ComposeWave(core.WaveIndex, waveComposition); // 예고와 같은 함수 = 화면이 말한 대로 나온다.

			TowerDefenseWaveEventKind waveEvent = WaveEventAt(core.WaveIndex);
			int enemyCount = ScaledEnemyCount(core.WaveIndex);
			int spawnedCount = 0; // 실제로 UnitObject 확보 + 등록까지 끝난 수 — 이게 0 이면 ConfirmWaveSpawned 자체를 보류.

			for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
			{
				if (stage.EnemyUnit == null || stage.EnemyUnit.Prefab == null)
				{
					Debug.LogWarning($"{nameof(TowerDefenseMatch)}: stage.EnemyUnit/Prefab 미할당 — 웨이브 스폰 skip.");
					break;
				}

				Vector3 localSpawn = activeSpawnPoints.Count > 0
					? activeSpawnPoints[enemyIndex % activeSpawnPoints.Count] + SpawnSpreadOffset(enemyIndex)
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

				TowerDefenseEnemyArchetype archetype = enemyIndex < waveComposition.Count
					? EnemyArchetypeAt(waveComposition[enemyIndex])
					: null;

				ApplyReadability(enemyUnitObject,
					archetype != null ? archetype.Tint : stage.EnemyTint,
					stage.EnemyScale * (archetype != null ? archetype.ScaleMultiplier : 1f));
				enemyBountyById[enemyCombatant.CombatantId] = archetype != null ? archetype.Bounty : core.BountyPerKill;
				enemyGameObject.SetActive(true);

				// ★ 스탯 배수는 *켠 다음 프레임*에 씌운다. UnitObject.Start 가 UnitData 로 스탯을 통째 다시
				//   세팅하므로(재-Init 규약), 켜기 전에 올려둔 체력은 첫 프레임에 조용히 원래대로 돌아간다
				//   (라이브 실증: 덩치·보상은 갈리는데 체력만 전부 같았다).
				yield return null;
				if (core == null || targeting == null || pool == null)
					yield break;
				ApplyArchetypeStats(enemyUnitObject, archetype);
				ApplyWaveEventStats(enemyUnitObject, waveEvent);

				foreach (UnitBrain brain in enemyUnitObject.GetComponents<UnitBrain>()) // 트랩#2.
					brain.enabled = false;

				TacticDriver enemyDriver = enemyUnitObject.GetComponent<TacticDriver>();
				if (enemyDriver == null)
					enemyDriver = enemyUnitObject.gameObject.AddComponent<TacticDriver>();
				enemyDriver.Initialize(stage.EnemyTactic, targeting, timeManager);
				enemyDriver.Navigator = flowNavigator; // 지형이 있으면 돌아가고, 없으면(null) 직선 그대로.
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

		/// <summary>
		/// 같은 출현 지점에 나오는 마수들을 서로 벌린다.
		///
		/// ★ 겹쳐 스폰하면 물리가 파고듦을 해소하려고 서로를 튕겨내 **맵 밖으로 날려버린다**
		///   (실측: 살아있는 마수 2기가 (1236, -2906, 2015) 로 날아가 웨이브가 영원히 안 끝났다).
		///   출현 지점 수보다 마수가 많아지는 후반 웨이브에서 반드시 발생하므로 스폰 단계에서 막는다.
		/// 같은 지점을 쓰는 몇 번째인지로 좌우 지그재그 — 결정적(같은 웨이브 → 같은 배치).
		/// </summary>
		private Vector3 SpawnSpreadOffset(int enemyIndex)
		{
			int pointCount = activeSpawnPoints.Count;
			int repeat = enemyIndex / pointCount;          // 이 지점을 몇 번째로 쓰는가
			int lane = (repeat + 1) / 2;                   // 0,1,1,2,2,...
			float side = repeat % 2 == 0 ? 1f : -1f;       // 좌우 번갈아
			float spread = stage.EnemySpawnSpread;

			// z 도 조금 밀어 완전히 같은 줄에 서지 않게(앞뒤로도 벌림).
			return new Vector3(lane * spread * side, 0f, repeat * spread * 0.35f);
		}

		/// <summary>
		/// 무대를 벗어난 적 정리 — 지면 아래로 떨어졌거나 개척지 밖으로 날아간 개체는 죽은 것으로 친다.
		///
		/// ★ 이게 없으면 *어떤* 물리 사고든 곧바로 「웨이브가 영원히 안 끝남」이 된다(코어는 생존 적을
		///   세는데 그 적은 화면 밖에 있어 플레이어가 손쓸 방법이 없다). 원인을 하나 막는 것과 별개로,
		///   무대 밖 개체가 진행을 막지 못하게 하는 안전망이 진행 규칙 쪽에 있어야 한다.
		/// </summary>
		private void CullEscapedEnemies()
		{
			if (stage == null || stageRoot == null)
				return;

			float halfWidth = activeGroundWidth * 0.5f + stage.StageBoundsMargin;
			float halfLength = activeGroundLength * 0.5f + stage.StageBoundsMargin;

			for (int index = waveEnemies.Count - 1; index >= 0; index--)
			{
				ArenaCombatant enemy = waveEnemies[index];
				if (enemy == null || enemy.IsAlive == false)
					continue;

				Vector3 local = stageRoot.InverseTransformPoint(enemy.Position);
				bool escaped = local.y < stage.StageFloorDepth
					|| Mathf.Abs(local.x) > halfWidth
					|| Mathf.Abs(local.z) > halfLength;
				if (escaped == false)
					continue;

				Debug.LogWarning($"{nameof(TowerDefenseMatch)}: 마수가 무대를 이탈 — 제거로 처리 local={local}. "
					+ "(스폰 겹침·물리 튕김 흔적이면 EnemySpawnSpread 확인)");

				targeting.Unregister(enemy);
				registeredCombatants.Remove(enemy);
				waveEnemies.RemoveAt(index);

				TacticDriver driver = enemy.GetComponent<TacticDriver>();
				if (driver != null)
					driver.StopDriving();

				ReleaseUnit(pool, enemy.gameObject);
				spawnedUnits.Remove(enemy.gameObject);
			}
		}

		/// <summary>
		/// 포탑 사거리 = 전술의 표적 탐색 반경. 별도 수치를 두면 화면의 원과 실제 사거리가 갈라진다
		/// (원이 거짓말하는 순간 배치 판단 전체가 무의미해진다) — 그래서 전술 정본에서 읽는다.
		/// </summary>
		public float TowerRange(int towerIndex = 0)
		{
			TowerDefenseTowerArchetype archetype = TowerArchetypeAt(towerIndex);
			if (archetype != null)
				return archetype.Range;

			if (stage == null || stage.TowerTactic.Rules == null)
				return 0f;

			float best = 0f;
			foreach (TacticRule rule in stage.TowerTactic.Rules)
			{
				if (rule.Target.MaxRange > best)
					best = rule.Target.MaxRange;
			}
			return best;
		}

		/// <summary> 첫 파도를 사람이 부르길 기다리는 중인가 — 화면이 「시계가 돈다」고 거짓말하지 않게. </summary>
		public bool IsWaitingForFirstCall =>
			core != null
			&& core.Phase == TowerDefensePhase.Prepare
			&& core.WaveIndex < core.FirstAutoWave
			&& core.IsNextWaveRequested == false;

		/// <summary> 이번 판의 자원 노드 위치(무대 로컬) — 절차 생성이면 매 판 다르다. </summary>
		public IReadOnlyList<Vector3> ActiveResourceNodePositions => activeNodePositions;

		/// <summary> 그 자리가 지금 보이는가 — 안 보이면 포탑도 못 쏘고 마수도 안 그려진다. </summary>
		public bool IsVisibleAt(Vector3 worldPosition)
		{
			if (vision == null || mapLayout == null || stageRoot == null)
				return true; // 시야 없는 판(고정 레이아웃) = 전부 보임.

			return vision.IsVisible(mapLayout.WorldToCell(stageRoot.InverseTransformPoint(worldPosition)));
		}

		/// <summary> 한 번이라도 밝혔던 자리인가 — 기억한 지형·노드는 계속 보여준다. </summary>
		public bool IsExploredAt(Vector3 worldPosition)
		{
			if (vision == null || mapLayout == null || stageRoot == null)
				return true;

			return vision.IsExplored(mapLayout.WorldToCell(stageRoot.InverseTransformPoint(worldPosition)));
		}

		/// <summary> 시야원 하나 추가 + 즉시 반영 — 건물을 세운 그 순간 밝아져야 「넓혔다」가 읽힌다. </summary>
		private void AddVisionSource(Vector3 worldPosition, float radius)
		{
			if (vision == null || mapLayout == null || stageRoot == null || radius <= 0f)
				return;

			visionSources.Add(new TowerDefenseVision.Source(
				mapLayout.WorldToCell(stageRoot.InverseTransformPoint(worldPosition)), radius));
			RefreshVision();
		}

		private void RefreshVision()
		{
			if (vision == null)
				return;

			// 어스름 파도면 모든 시야가 함께 좁아진다 — 「보이는 만큼만 쏜다」가 아프게 걸린다.
			float visionScale = CurrentVisionScale();
			if (Mathf.Approximately(visionScale, 1f))
			{
				vision.Recompute(visionSources);
			}
			else
			{
				scaledVisionSources.Clear();
				foreach (TowerDefenseVision.Source source in visionSources)
					scaledVisionSources.Add(new TowerDefenseVision.Source(source.Cell, source.Radius * visionScale));
				vision.Recompute(scaledVisionSources);
			}
			if (fogView != null)
				fogView.Apply(vision);
		}

		/// <summary> index 번 노드의 벌이 배수 — 화면 표시와 실제 수입이 같은 값을 읽는다. </summary>
		public float NodeIncomeMultiplierAt(int index)
		{
			return index >= 0 && index < activeNodeIncomeMultipliers.Count ? activeNodeIncomeMultipliers[index] : 1f;
		}

		/// <summary> 무대 루트 — 화면 표시가 로컬 좌표를 월드로 옮길 때 쓴다. </summary>
		public Transform StageRoot => stageRoot;

		/// <summary> 이번 판의 마수 출현 지점(무대 로컬). </summary>
		public IReadOnlyList<Vector3> ActiveEnemySpawnPoints => activeSpawnPoints;

		/// <summary> 이번 판의 씨앗 — 같은 값이면 같은 판이 나온다(재현·신고용). 고정 판이면 0. </summary>
		public int MapSeed => mapLayout != null ? mapLayout.Seed : 0;

		/// <summary> 이번 판의 암반 칸 수 — 0 이면 지형 없는 빈 판. </summary>
		public int ObstacleCount => mapLayout != null ? mapLayout.ObstacleCells.Count : 0;

		// ── 시간 조작 ────────────────────────────────────────────────────────────
		// ★ 왜 필요한가: 판이 커지고(44칸) 화면이 말하는 정보가 늘었는데(예고·사거리·시야·길) 정작
		//   *볼 시간*이 없으면 그 정보는 없는 것과 같다. 멈추고 보는 것은 편의가 아니라 전술의 일부다.
		private static readonly float[] SpeedSteps = { 0f, 1f, 2f, 3f };
		private int speedStep = 1;

		/// <summary> 지금 시간 배속(0 = 멈춤). </summary>
		public float SpeedScale => SpeedSteps[Mathf.Clamp(speedStep, 0, SpeedSteps.Length - 1)];

		/// <summary> 멈춤 ↔ 직전 배속 토글. 멈춘 채로 배치·관찰할 수 있어야 정보가 쓸모를 갖는다. </summary>
		public void TogglePause()
		{
			speedStep = speedStep == 0 ? lastRunningStep : 0;
			ApplySpeed();
		}

		/// <summary> 배속 한 단계 올림(끝에서 처음으로 순환). 멈춤 상태는 건너뛴다. </summary>
		public void CycleSpeed()
		{
			speedStep = speedStep >= SpeedSteps.Length - 1 ? 1 : speedStep + 1;
			lastRunningStep = speedStep;
			ApplySpeed();
		}

		private int lastRunningStep = 1;

		private void ApplySpeed()
		{
			// 개척 안에서는 이 모드가 곧 게임 전부라 전역 시간을 그대로 쓴다 —
			// 매치 전용 시계를 따로 두면 물리·이펙트가 따로 놀아 화면이 갈라진다.
			Time.timeScale = SpeedScale;
		}

		/// <summary> 모드를 나가거나 매치가 끝나면 반드시 원래 속도로 — 안 되돌리면 본편이 멈춘 채 남는다. </summary>
		public void RestoreTimeScale()
		{
			Time.timeScale = 1f;
		}

		/// <summary> 세운 연구 인형 수 — 늘어날수록 모든 포탑이 강해진다. </summary>
		public int LabCount { get; private set; }

		/// <summary>
		/// 지금의 포탑 피해 배수. 포탑이 매 발사 때 *읽어가므로* 나중에 세운 연구 인형이
		/// 이미 서 있던 포탑에도 즉시 반영된다(세운 뒤에야 효과가 오면 강화가 아니라 벌칙이다).
		/// </summary>
		public float TowerDamageMultiplier => 1f + LabCount * (stage != null ? stage.LabDamageBonus : 0f);

		/// <summary>
		/// 지금까지 내가 쓴 수단의 누적 — 세워둔 포탑들이 각자 센 것을 모은다.
		/// 「무엇을 많이 썼나」가 곧 마수가 무엇에 익숙해지는가다.
		/// </summary>
		public TowerDefenseAdaptationState Adaptation
		{
			get
			{
				if (stage == null)
					return default;

				int slowUses = 0;
				int splashHits = 0;
				int pierceHits = 0;
				foreach (GameObject unit in spawnedUnits)
				{
					if (unit == null)
						continue;
					TowerDefenseWeapon weapon = unit.GetComponent<TowerDefenseWeapon>();
					if (weapon == null)
						continue;
					slowUses += weapon.SlowApplied;
					splashHits += weapon.SplashHits;
					pierceHits += weapon.PierceHits;
				}

				return TowerDefenseAdaptation.From(slowUses, splashHits, pierceHits, stage.AdaptationSensitivity);
			}
		}

		/// <summary> waveIndex 파의 성격 — 예고와 스폰이 같은 함수를 본다. </summary>
		public TowerDefenseWaveEventKind WaveEventAt(int waveIndex)
		{
			return stage != null
				? TowerDefenseWaveEvent.For(waveIndex, stage.WaveEventEvery)
				: TowerDefenseWaveEventKind.None;
		}

		/// <summary> 성격까지 반영한 그 파도의 마수 수(떼거리는 배로, 정예는 절반). </summary>
		public int ScaledEnemyCount(int waveIndex)
		{
			if (stage == null)
				return 0;

			float scaled = stage.Rules.EnemiesInWave(waveIndex)
				* TowerDefenseWaveEvent.CountScale(WaveEventAt(waveIndex));
			return Mathf.Max(1, Mathf.RoundToInt(scaled));
		}

		/// <summary> 지금 파도의 시야 배수 — 어스름이면 좁아진다. </summary>
		private float CurrentVisionScale()
		{
			return TowerDefenseWaveEvent.VisionScale(WaveEventAt(core != null ? core.WaveIndex : 0));
		}

		// 파도 성격을 마수 스탯에 얹는다 — 종류(archetype) 배수 *위에* 곱해지므로 둘이 겹쳐 쌓인다.
		private static void ApplyWaveEventStats(UnitObject unitObject, TowerDefenseWaveEventKind kind)
		{
			if (unitObject == null || kind == TowerDefenseWaveEventKind.None)
				return;

			float healthScale = TowerDefenseWaveEvent.HealthScale(kind);
			if (Mathf.Approximately(healthScale, 1f) == false)
			{
				int scaledMax = Mathf.Max(1, Mathf.RoundToInt(unitObject.UnitStat[UnitStatType.HP_MAX] * healthScale));
				unitObject.UnitStat[UnitStatType.HP_MAX_STAT] = scaledMax;
				unitObject.UnitStat[UnitStatType.HP_MAX] = scaledMax;
				unitObject.UnitStat[UnitStatType.HP_CUR] = scaledMax;
			}

			float speedScale = TowerDefenseWaveEvent.SpeedScale(kind);
			if (Mathf.Approximately(speedScale, 1f) == false)
			{
				int scaledSpeed = Mathf.Max(1, Mathf.RoundToInt(unitObject.UnitStat[UnitStatType.MOVEMENT_SPEED] * speedScale));
				unitObject.UnitStat[UnitStatType.MOVEMENT_SPEED] = scaledSpeed;
			}
		}

		/// <summary> 등록된 포탑 종류 수(0 이면 기존 단일 포탑). </summary>
		public int TowerArchetypeCount => stage != null && stage.TowerArchetypes != null ? stage.TowerArchetypes.Length : 0;

		/// <summary> index 번 포탑 종류(범위 밖이면 null). </summary>
		public TowerDefenseTowerArchetype TowerArchetypeAt(int index)
		{
			if (index < 0 || index >= TowerArchetypeCount)
				return null;
			return stage.TowerArchetypes[index];
		}

		/// <summary> index 번 포탑의 건설 비용 — 종류가 없으면 스테이지 기본값. </summary>
		public int TowerCostAt(int index)
		{
			TowerDefenseTowerArchetype archetype = TowerArchetypeAt(index);
			return archetype != null ? archetype.Cost : stage.TowerCost;
		}

		/// <summary> 등록된 마수 종류 수(0 이면 기반 유닛 한 종류로 동작). </summary>
		public int EnemyArchetypeCount => stage != null && stage.EnemyArchetypes != null ? stage.EnemyArchetypes.Length : 0;

		/// <summary> index 번 마수 종류(범위 밖이면 null). HUD 범례·예고가 이름·색을 읽는다. </summary>
		public TowerDefenseEnemyArchetype EnemyArchetypeAt(int index)
		{
			if (index < 0 || index >= EnemyArchetypeCount)
				return null;
			return stage.EnemyArchetypes[index];
		}

		/// <summary>
		/// waveIndex 파의 구성을 계산해 result 에 담는다 — *예고*와 *실제 스폰*이 같은 함수를 쓰므로
		/// 화면이 거짓말할 수 없다(예고용 별도 계산을 두면 언젠가 반드시 어긋난다).
		/// </summary>
		public void ComposeWave(int waveIndex, List<int> result)
		{
			result.Clear();
			if (stage == null || core == null)
				return;

			int enemyCount = stage.Rules.EnemiesInWave(waveIndex);
			int archetypeCount = EnemyArchetypeCount;
			if (archetypeCount <= 0)
			{
				for (int index = 0; index < enemyCount; index++)
					result.Add(0);
				return;
			}

			int[] unlockWaves = new int[archetypeCount];
			int[] weights = new int[archetypeCount];
			for (int index = 0; index < archetypeCount; index++)
			{
				TowerDefenseEnemyArchetype archetype = stage.EnemyArchetypes[index];
				unlockWaves[index] = archetype != null ? archetype.UnlockWave : 0;
				weights[index] = archetype != null ? archetype.Weight : 0;
			}

			TowerDefenseWaveComposer.Compose(unlockWaves, weights, waveIndex, enemyCount, result);
		}

		/// <summary>
		/// 목표에 닿은 마수 처리 — 유출(leak). 그 마수는 *사라지고* 목숨이 하나 준다.
		///
		/// ★ 코어를 갉는 방식과 다른 점: 「아직 얼마 남았나」가 아니라 「한 마리라도 새면 아프다」가 된다.
		///   길목 하나가 뚫리는 순간의 무게가 여기서 정해진다. 새 놈이 코어에 눌어붙어 화면에서
		///   사라지던 옛 문제도 같이 없어진다(닿는 즉시 치우므로).
		/// </summary>
		private void CullLeakedEnemies()
		{
			if (core == null || core.UsesLives == false || coreCombatant == null)
				return;

			float leakRadius = stage.LeakRadius;
			float leakRadiusSqr = leakRadius * leakRadius;

			for (int index = waveEnemies.Count - 1; index >= 0; index--)
			{
				ArenaCombatant enemy = waveEnemies[index];
				if (enemy == null || enemy.IsAlive == false)
					continue;
				if ((enemy.Position - coreCombatant.Position).sqrMagnitude > leakRadiusSqr)
					continue;

				PopWorldText("-1", enemy.Position, TextType.Warning);
				core.RegisterLeak();

				targeting.Unregister(enemy);
				registeredCombatants.Remove(enemy);
				waveEnemies.RemoveAt(index);

				TacticDriver driver = enemy.GetComponent<TacticDriver>();
				if (driver != null)
					driver.StopDriving();

				ReleaseUnit(pool, enemy.gameObject);
				spawnedUnits.Remove(enemy.gameObject);
			}
		}

		/// <summary> 남은 목숨(유출제 아니면 0). </summary>
		public int Lives => core != null ? core.Lives : 0;

		/// <summary> 이 판이 유출제인가 — 화면이 목숨을 보여줄지 결정한다. </summary>
		public bool UsesLives => core != null && core.UsesLives;

		/// <summary>
		/// 시야 밖 마수는 안 그린다 — 포탑이 못 쏘는데 화면에는 보이면, 「왜 안 쏘지」가 버그로 읽힌다.
		/// 규칙(못 쏨)과 그림(안 보임)이 같은 사실을 말해야 한다.
		/// </summary>
		private void ApplyEnemyVisibility()
		{
			if (vision == null)
				return;

			foreach (ArenaCombatant enemy in waveEnemies)
			{
				if (enemy == null || enemy.UnitObject == null)
					continue;

				bool seen = IsVisibleAt(enemy.Position);
				foreach (Renderer enemyRenderer in enemy.UnitObject.GetComponentsInChildren<Renderer>(true))
				{
					if (enemyRenderer.enabled != seen)
						enemyRenderer.enabled = seen;
				}
			}
		}

		/// <summary>
		/// 격파 보상 지급 — 마수가 죽은 것을 처음 본 틱에 1회. 「잡는 맛」이 이 경로 하나에 달려 있다:
		/// 웨이브 정산만 있으면 교전 20초 동안 화면에서 아무 일도 안 일어나고, 잘 맞췄는지도 알 수 없다.
		/// 이탈(무대 밖) 제거는 목록에서 먼저 빠지므로 보상 대상이 아니다 — 사고에 상을 주지 않는다.
		/// </summary>
		private void PayKillBounties()
		{
			if (core == null)
				return;

			foreach (ArenaCombatant enemy in waveEnemies)
			{
				if (enemy == null || enemy.IsAlive)
					continue;
				if (bountyPaidEnemyIds.Add(enemy.CombatantId) == false)
					continue;

				int bounty = enemyBountyById.TryGetValue(enemy.CombatantId, out int recorded)
					? recorded
					: core.BountyPerKill;
				if (bounty <= 0)
					continue;

				core.AddResource(bounty);
				PopWorldText("+" + bounty, enemy.Position, TextType.Exp);

				// 죽은 자리에 잔해 — 많이 죽인 곳이 저절로 늪이 되어 다음 무리가 느려진다.
				TowerDefenseDebris.Spawn(stageRoot, enemy.Position, waveEnemies,
					stage.DebrisSeconds, stage.DebrisSlowFactor, stage.GroundCellSize * 0.8f, stage.DebrisTint);
			}
		}

		/// <summary>
		/// 웨이브 정산 내역을 *번 자리에* 띄운다 — 코어에 기본 수입, 채집 인형 각자 머리 위에 자기 몫.
		/// 총액만 HUD 숫자로 올리면 「채집 인형이 무슨 역할인지」가 영원히 안 읽힌다(사용자 실증).
		/// </summary>
		private void ShowIncomeBreakdown()
		{
			if (core == null || stage == null)
				return;

			if (coreCombatant != null && stage.Rules.BaseWaveIncome > 0)
				PopWorldText("+" + stage.Rules.BaseWaveIncome, coreCombatant.Position, TextType.Heal);

			if (stage.Rules.IncomePerHarvester <= 0)
				return;

			for (int index = harvesterTransforms.Count - 1; index >= 0; index--)
			{
				Transform harvester = harvesterTransforms[index];
				if (harvester == null)
				{
					harvesterTransforms.RemoveAt(index);
					continue;
				}
				PopWorldText("+" + stage.Rules.IncomePerHarvester, harvester.position, TextType.Heal);
			}
		}

		/// <summary> 월드 좌표 위 뜨는 글자 — UI 매니저가 아직 없으면(부팅 전/헤드리스) 조용히 넘어간다. </summary>
		private static void PopWorldText(string message, Vector3 worldPosition, TextType textType)
		{
			if (UIManager.TryGetExistingInstance(out UIManager uiManager) == false)
				return;

			uiManager.PopText(message, textType, worldPosition);
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
		public bool TryPlaceTower(Vector3 worldPosition, int towerIndex = 0)
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
			{
				// ★ 같은 자리에 같은 종류를 다시 지으면 = 승급. 별도 선택 UI 없이 「한 번 더 짓는다」는
				//   손동작 그대로라 배울 게 없고, 세로 깊이(같은 포탑을 키운다)가 생긴다.
				return TryUpgradeTowerAt(cellKey, towerIndex);
			}

			if (core.TrySpend(TowerCostAt(towerIndex)) == false)
				return false;

			occupiedCells.Add(cellKey);
			// 종류가 정의돼 있으면 개척 전용 무기로, 없으면 기존 전술 경로로(하위 호환).
			TowerDefenseTowerArchetype archetype = TowerArchetypeAt(towerIndex);
			StartCoroutine(SpawnDefensiveUnitRoutine(
				stage.TowerUnit,
				archetype != null ? null : stage.TowerTactic,
				worldPosition,
				isHarvester: false,
				incomeMultiplier: 1f,
				towerArchetype: archetype));
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
			float incomeMultiplier = nodeIndex < activeNodeIncomeMultipliers.Count ? activeNodeIncomeMultipliers[nodeIndex] : 1f;
			bool outerNode = nodeIndex < activeNodeIsOuter.Count && activeNodeIsOuter[nodeIndex];
			harvesterIsOuter[nodeWorldPosition] = outerNode;
			StartCoroutine(SpawnDefensiveUnitRoutine(stage.HarvesterUnit, null, nodeWorldPosition, isHarvester: true, incomeMultiplier));
			return true;
		}

		/// <summary>
		/// 그 칸에 세운 것을 판다(환불). 「실수가 되돌려지는가」 — 이게 없으면 배치가 실험이 아니라 도박이다.
		/// 코어는 못 판다(그건 자해다). 판 자리는 다시 비워져 새로 지을 수 있다.
		/// </summary>
		public bool TrySell(Vector3 worldPosition, float refundRatio)
		{
			if (core == null || pool == null)
				return false;

			Vector3Int cellKey = ToCellKey(worldPosition);
			if (occupiedCells.Contains(cellKey) == false)
				return false;

			GameObject sold = null;
			foreach (GameObject unit in spawnedUnits)
			{
				if (unit == null || unit.activeInHierarchy == false)
					continue;
				if (ToCellKey(unit.transform.position) != cellKey)
					continue;
				if (coreCombatant != null && unit == coreCombatant.gameObject)
					return false; // 코어는 못 판다.
				sold = unit;
				break;
			}

			if (sold == null)
				return false;

			int refund = Mathf.Max(0, Mathf.RoundToInt(SoldValue(sold) * refundRatio));
			core.AddResource(refund);
			PopWorldText("+" + refund, sold.transform.position, TextType.Heal);

			ReleaseSoldUnit(sold);
			occupiedCells.Remove(cellKey);
			return true;
		}

		// 판 값 = 그 자리에 무엇이 서 있었나. 채집이면 노드 점유도 함께 푼다(다시 잡을 수 있어야 한다).
		private int SoldValue(GameObject sold)
		{
			for (int index = harvesterTransforms.Count - 1; index >= 0; index--)
			{
				if (harvesterTransforms[index] == null || harvesterTransforms[index] != sold.transform)
					continue;

				harvesterTransforms.RemoveAt(index);
				int nodeIndex = ReleaseNodeAt(sold.transform.position);
				float multiplier = NodeIncomeMultiplierAt(nodeIndex);
				if (nodeIndex >= 0 && nodeIndex < activeNodeIsOuter.Count && activeNodeIsOuter[nodeIndex])
					core.RemoveEssenceHarvester(multiplier);
				else
					core.RemoveHarvester(multiplier);
				return stage.HarvesterCost;
			}

			TowerDefenseWeapon weapon = sold.GetComponent<TowerDefenseWeapon>();
			if (weapon != null)
				return weapon.Cost;

			// 무기가 없는 방어 건물 = 연구 인형.
			LabCount = Mathf.Max(0, LabCount - 1);
			return stage.LabCost;
		}

		// 판 채집 인형이 잡고 있던 노드를 놓아준다(못 놓으면 그 노드는 영영 못 쓴다).
		private int ReleaseNodeAt(Vector3 worldPosition)
		{
			for (int index = 0; index < activeNodePositions.Count; index++)
			{
				if (claimedNodes.Contains(index) == false)
					continue;
				Vector3 nodeWorld = stageRoot.TransformPoint(activeNodePositions[index]);
				if ((nodeWorld - worldPosition).sqrMagnitude > 1f)
					continue;

				claimedNodes.Remove(index);
				return index;
			}
			return -1;
		}

		private void ReleaseSoldUnit(GameObject sold)
		{
			ArenaCombatant combatant = sold.GetComponent<ArenaCombatant>();
			if (combatant != null && targeting != null)
			{
				targeting.Unregister(combatant);
				registeredCombatants.Remove(combatant);
			}

			TacticDriver driver = sold.GetComponent<TacticDriver>();
			if (driver != null)
				driver.StopDriving();

			ReleaseUnit(pool, sold);
			spawnedUnits.Remove(sold);
		}

		/// <summary>
		/// 연구 인형 배치 — 아무 빈 칸에나 선다(노드 결합 X). 지어진 순간부터 *모든* 포탑이 강해진다.
		/// 자원을 지금 방어에 쓸지 다음 파도를 위해 강화에 쓸지 — 판 안의 새 선택이 여기서 생긴다.
		/// </summary>
		public bool TryPlaceLab(Vector3 worldPosition)
		{
			if (core == null || pool == null || timeManager == null || targeting == null)
				return false;
			if (stage.HarvesterUnit == null || stage.HarvesterUnit.Prefab == null)
			{
				Debug.LogError($"{nameof(TowerDefenseMatch)}: 연구 인형이 쓸 프리팹 미할당 — 배치 불가(자원 미차감).");
				return false;
			}

			Vector3Int cellKey = ToCellKey(worldPosition);
			if (occupiedCells.Contains(cellKey))
				return false;
			if (IsObstacleAt(worldPosition))
				return false;

			// 연구는 정수로만 — 강화의 통로를 바깥 노드(개척)에 묶는다.
			if (core.TrySpendEssence(stage.LabEssenceCost) == false)
				return false;

			occupiedCells.Add(cellKey);
			LabCount++;
			StartCoroutine(SpawnDefensiveUnitRoutine(
				stage.HarvesterUnit, null, worldPosition, isHarvester: false, incomeMultiplier: 1f, towerArchetype: null, isLab: true));
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

			if (stage == null || stageRoot == null)
				return false;

			float captureRadiusSqr = stage.NodeCaptureRadius * stage.NodeCaptureRadius;
			int bestIndex = -1;
			float bestSqrDistance = float.MaxValue;

			for (int index = 0; index < activeNodePositions.Count; index++)
			{
				if (claimedNodes.Contains(index))
					continue;

				Vector3 candidateWorldPosition = stageRoot.TransformPoint(activeNodePositions[index]);
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
			nodeWorldPosition = stageRoot.TransformPoint(activeNodePositions[bestIndex]);
			return true;
		}

		/// <summary>
		/// worldPosition 이 속한 셀이 이미 배치물로 점유됐는지 — 배치 UI 프리뷰가 유효/무효 색을
		/// 이 메서드로 판정(TryPlaceTower/TryPlaceHarvester 내부 점유 판정과 동일 규칙 재사용).
		/// </summary>
		public bool IsCellOccupied(Vector3 worldPosition)
		{
			if (occupiedCells.Contains(ToCellKey(worldPosition)))
				return true;

			// 암반 위에는 못 짓는다 — 화면에 바위가 보이는데 그 위에 세워지면 규칙과 그림이 어긋난다.
			return IsObstacleAt(worldPosition);
		}

		/// <summary> 그 자리가 암반인지(무대 로컬 환산 후 판정). 고정 판이면 항상 false. </summary>
		public bool IsObstacleAt(Vector3 worldPosition)
		{
			if (mapLayout == null || stageRoot == null)
				return false;

			return mapLayout.IsBlocked(stageRoot.InverseTransformPoint(worldPosition));
		}

		// 셀 키 = FloorToInt(worldPosition), y 는 0 고정(층 무관 단일 격자 — 위로 쌓기 원천 차단).
		private static Vector3Int ToCellKey(Vector3 worldPosition)
		{
			Vector3Int cell = Vector3Int.FloorToInt(worldPosition);
			cell.y = 0;
			return cell;
		}

		private IEnumerator SpawnDefensiveUnitRoutine(Unit unitData, TacticProgram tactic, Vector3 worldPosition, bool isHarvester, float incomeMultiplier = 1f, TowerDefenseTowerArchetype towerArchetype = null, bool isLab = false)
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
				isLab ? stage.LabTint
					: isHarvester ? stage.HarvesterTint
					: (towerArchetype != null ? towerArchetype.Tint : stage.TowerTint),
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

			// 세워둔 포탑의 사거리를 옅게 늘 보여준다 — 「어디가 비었나」는 기존 커버리지가 보여야 알 수 있다.
			if (isHarvester == false && isLab == false)
			{
				if (towerArchetype != null)
				{
					TowerDefenseWeapon weapon = unitObject.GetComponent<TowerDefenseWeapon>();
					if (weapon == null)
						weapon = unitObject.gameObject.AddComponent<TowerDefenseWeapon>();
					weapon.Configure(towerArchetype, targeting, combatant, waveEnemies, IsVisibleAt, () => TowerDamageMultiplier, () => Adaptation);
				}

				float towerRange = towerArchetype != null ? towerArchetype.Range : TowerRange();
				if (towerRange > 0f)
				{
					Color ringColor = towerArchetype != null ? towerArchetype.Tint : new Color(0.45f, 0.72f, 1f, 1f);
					ringColor.a = 0.30f;
					TowerDefenseRing ring = TowerDefenseRing.Create(
						unitGameObject.transform, "RangeRing", ringColor, 0.08f, 0.05f);
					ring.SetRadius(towerRange);
				}
			}

			AddVisionSource(worldPosition,
				isLab ? stage.LabVisionRadius
					: isHarvester ? stage.HarvesterVisionRadius
					: (towerArchetype != null ? towerArchetype.VisionRadius : stage.CoreVisionRadius));

			if (isHarvester)
			{
				if (harvesterIsOuter.TryGetValue(worldPosition, out bool outer) && outer)
					core.AddEssenceHarvester(incomeMultiplier);
				else
					core.AddHarvester(incomeMultiplier); // 채집건물 = 실제 가동(스폰 확정) 시점에만 수입 반영.
				harvesterTransforms.Add(unitGameObject.transform);
			}
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
