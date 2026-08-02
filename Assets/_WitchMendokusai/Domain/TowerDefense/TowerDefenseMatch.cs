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
		// ★ 열쇠는 반드시 *그 인형 자신*이다. 좌표를 열쇠로 쓰면 유닛이 바닥에 앉으며 소수점이 미세하게
		//   달라지는 순간 조회가 통째로 빗나가 **바깥 노드가 영원히 안쪽으로 취급된다** = 정수 수입 0
		//   (사용자 실증: "전초지기랑 연구소 설치 안됨" — 정수로만 사는 것들이 통째로 잠겨 있었다).
		private readonly Dictionary<Transform, bool> harvesterIsOuter = new();
		// 보급 — 코어에서 내 건물을 징검다리로 이어지는 사슬. 끊기면 그 너머의 채집은 수입이 0.
		private readonly List<Vector3> supplyBuildings = new();
		private readonly List<Transform> supplyTransforms = new();
		private readonly HashSet<int> suppliedBuildings = new();
		// 전초기지 — 마수가 향하는 *또 하나의 목표*이자 보급의 새 원점.
		private readonly List<Transform> outposts = new();
		private readonly List<Vector3> supplySeeds = new();
		private readonly List<Vector2Int> pathGoals = new();

		// 파도 사이 드래프트 — 고른 것이 쌓이는 곳(boons) + 지금 화면에 걸려 답을 기다리는 카드들(pendingDraft).
		// 카드가 걸려 있는 동안 진행이 멈춘다 = 「강제 선택」의 실체.
		private readonly TowerDefenseBoonState boons = new();
		private readonly List<TowerDefenseBoon> pendingDraft = new();

		// 영웅 인형 — 유일하게 *움직이는* 내 편. 포탑과 같은 전투 표를 쓰되 자리를 내가 옮긴다.
		private Transform heroTransform;
		private ArenaCombatant heroCombatant;
		private Vector3 heroTargetPosition;
		private bool heroActive;

		// 이름 붙은 인형들 — 화면이 이름표를 띄우는 데 필요한 최소 정보.
		private readonly List<TowerDefenseDollLabel> dollLabels = new();
		private int nextDollOrdinal;

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
		// 수입 가중치를 보급이 정하게 되면서 core 의 누적 카운트는 늘 0 이 됐다 — 실제 목록이 진실.
		public int HarvesterCount => harvesterTransforms.Count;

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
			supplyTransforms.Clear();
			supplyBuildings.Clear();
			outposts.Clear();
			suppliedBuildings.Clear();
			DisconnectedHarvesters = 0;
			LabCount = 0;
			TrapsSpent = 0;
			speedStep = 1;
			lastRunningStep = 1;
			ApplySpeed();
			occupiedCells.Clear(); // 재진입 — 지난 매치의 셀 점유가 새 매치로 새는 것 방지.

			// 새 판 = 새 선택·새 이름·새 영웅. 하나라도 남으면 "새 판"이 아니다.
			boons.Reset();
			pendingDraft.Clear();
			dollLabels.Clear();
			nextDollOrdinal = 0;
			heroActive = false;
			heroTransform = null;
			heroCombatant = null;
			heroVisionSourceIndex = -1;
			heroVisionCell = new Vector2Int(int.MinValue, int.MinValue);
			enemyMaxStopDistance = 0f;
			enemyStillness.Clear();

			yield return SpawnCoreRoutine();
			if (coreCombatant == null)
			{
				// 코어 스폰 자체가 실패 — 이미 로그됨. 진입 상태만 리셋(started 가드 해제).
				started = false;
				yield break;
			}

			yield return SpawnHeroRoutine(); // 영웅 미설정 스테이지면 즉시 빠져나온다(기존 판과 동일).

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

				// 이름표에도 단계가 붙는다 — 같은 아이가 자란 것이지 새 물건이 생긴 것이 아니다.
				TowerDefenseDollLabel label = FindDollLabel(unit.transform);
				if (label != null)
					label.Level = weapon.Level;

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
			pathGoals.Clear();
			pathGoals.Add(mapLayout.CoreCell);
			foreach (Transform outpost in outposts)
			{
				if (outpost != null)
					pathGoals.Add(mapLayout.WorldToCell(stageRoot.InverseTransformPoint(outpost.position)));
			}

			flowField = new TowerDefenseFlowField(
				mapLayout.Width, mapLayout.Length, pathGoals, IsPathBlocked);
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

			// 벽도 보급 중계다 — 길을 그리는 것과 보급선을 잇는 것이 같은 행위가 되어,
			// 「어디에 벽을 세울까」가 방어선과 살림살이 양쪽을 동시에 결정한다.
			supplyTransforms.Add(wall.transform);
			RefreshSupply();
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
			// ★ 진짜 체스판으로 (사용자 지시: "체스판처럼 게임 프로토타입에서 많이 보이는 텍스쳐").
			//   격자 *선*만 그으면 칸이 다 같은 색이라 「몇 칸 떨어졌나」가 안 읽힌다. 밝은 칸/어두운 칸이
			//   번갈아 나오면 거리가 눈으로 세어진다 — 프로토타입 바닥이 늘 체스판인 이유가 그거다.
			//   텍스처 한 장 = 2×2 칸(체크 한 주기).
			const int CELL_PIXELS = 32;
			const int LINE_PIXELS = 2;
			const int TEXTURE_PIXELS = CELL_PIXELS * 2;
			Texture2D checker = new Texture2D(TEXTURE_PIXELS, TEXTURE_PIXELS, TextureFormat.RGBA32, mipChain: true)
			{
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Repeat,
			};
			Color lightCell = new Color(0.34f, 0.38f, 0.44f, 1f);
			Color darkCell = new Color(0.22f, 0.25f, 0.30f, 1f);
			Color gridLine = new Color(0.55f, 0.62f, 0.72f, 1f);
			for (int y = 0; y < TEXTURE_PIXELS; y++)
			{
				for (int x = 0; x < TEXTURE_PIXELS; x++)
				{
					bool oddCell = (x / CELL_PIXELS + y / CELL_PIXELS) % 2 == 1;
					int inCellX = x % CELL_PIXELS;
					int inCellY = y % CELL_PIXELS;
					bool onEdge = inCellX < LINE_PIXELS || inCellY < LINE_PIXELS;
					checker.SetPixel(x, y, onEdge ? gridLine : (oddCell ? darkCell : lightCell));
				}
			}
			checker.Apply();

			// 텍스처 한 장 = 2칸이므로 타일 수 = 전체 길이 / (칸크기 × 2).
			float cell = stage.GroundCellSize > 0f ? stage.GroundCellSize : 1f;
			Vector2 tiling = new Vector2(activeGroundWidth / (cell * 2f), activeGroundLength / (cell * 2f));

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

			// ★ 몸집을 키우면 *충돌 몸통도 같이 커진다* — 그러면 단단한 마수(1.35배)는 암반과 벽 사이
			//   좁은 틈에 끼어 나오지 못한다(사용자 실증: "단단한 마수 아까부터 껴서 못 움직인다").
			//   이동은 콜라이더를 쓸어서 미끄러지는 방식이라, 몸통이 한 칸보다 크면 길이 있어도 못 지난다.
			//   보이는 크기는 그대로 두고 *충돌 몸통만* 원래 굵기로 되돌린다 — 「단단함」은 체력이 말한다.
			CapsuleCollider capsule = unitObject.GetComponent<CapsuleCollider>();
			if (capsule != null && scale > 1f)
			{
				capsule.radius /= scale;
				capsule.height /= scale;
			}
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

			// 보급이 여기서 출발해 어디까지 닿는지 — 안 보이면 「왜 안 이어지지」를 짐작으로 풀어야 한다.
			ShowSupplyReachRing(coreGameObject.transform);
		}

		private void Tick()
		{
			if (ticking == false || core == null)
				return;

			TickHero(); // 영웅은 카드가 걸려 있어도 자리를 잡을 수 있다 — 멈춘 것은 *파도*지 내 손이 아니다.

			// 카드가 걸린 동안은 진행 규칙 자체가 멈춘다 — 고르는 사이에 파도가 오면 선택이 아니라 벌칙이 된다.
			if (IsDraftPending)
				return;

			CullEscapedEnemies(); // 무대 밖 개체가 웨이브를 영원히 붙잡지 못하게 — 집계 *전에* 정리.
			CullLeakedEnemies();  // 목표에 닿은 마수는 사라지고 목숨이 준다(유출제).
			UnstickEnemies();     // 굳은 마수를 풀어준다 — 한 마리가 굳으면 웨이브가 영영 안 끝난다.
			ApplyEnemyVisibility(); // 안 보이는 마수는 화면에서도 지운다(규칙과 그림이 같아야 한다).
			RefreshSupply();        // 방어 건물이 부서지면 그 순간 사슬이 끊긴다.
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
					HealDefenders(); // 버틴 인형은 숨을 돌린다 — 안 그러면 한 번 긁힌 인형은 팔 때까지 계속 약하다.
					OfferDraft(); // 넘긴 직후 = 다음 파도를 준비하기 *전*. 여기가 선택이 가장 무거운 자리다.
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

				// ★ 분산(SpawnSpreadOffset)이 마수를 암반 위/뒤에 떨구면 그 마리는 「갈 수 없는 자리」에서 시작해
				//   그대로 굳는다 — 한 마리만 굳어도 웨이브가 영영 안 끝난다(사용자 실증: "멈춰서 안올때가 있음").
				//   출현 지점 자체는 길이 보장돼 있으므로(RebuildPathing 검사) 벌어진 자리만 되돌린다.
				localSpawn = SnapSpawnToReachable(localSpawn);

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
				enemyDriver.StopsToAttack = false;     // 걸으면서 쏜다 — 전진이 멈추면 판이 안 끝난다.
				// 마수가 코어 둘레에 「고리」로 서는 거리 — 유출 반경이 이보다 작으면 바깥 고리는 영영 안 닿는다.
				enemyMaxStopDistance = Mathf.Max(enemyMaxStopDistance, enemyDriver.MaxStopDistance);
				drivers.Add(enemyDriver);

				targeting.Register(enemyCombatant);
				registeredCombatants.Add(enemyCombatant);
				waveEnemies.Add(enemyCombatant);
				spawnedCount++;

				// ★ 한 지점에 한꺼번에 쏟으면 마수들이 서로의 몸에 끼어 그 자리에서 못 나온다
				//   (라이브 실측: 출현 줄에서 세 마리가 나란히 4초씩 정지). 좌우로 벌리는 것만으로는
				//   마릿수가 늘면 결국 겹친다 — *시간*으로 흘려보내야 구조적으로 안 겹친다.
				//   덤으로 「파도가 밀려온다」는 감각이 생긴다(장르 표준의 trickle spawn).
				if (stage.EnemySpawnInterval > 0f)
					yield return new WaitForSeconds(stage.EnemySpawnInterval);
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
		/// 벌어진 출현 자리가 길 위인지 확인하고, 아니면 가장 가까운 갈 수 있는 칸으로 되돌린다.
		/// 고정 판(흐름장 없음)에서는 아무것도 안 한다 — 그쪽은 애초에 암반이 없다.
		/// </summary>
		private Vector3 SnapSpawnToReachable(Vector3 localSpawn)
		{
			if (mapLayout == null || flowField == null)
				return localSpawn;

			Vector2Int cell = mapLayout.WorldToCell(localSpawn);
			if (flowField.IsReachable(cell))
				return localSpawn;

			if (TrySnapToReachable(cell, out Vector2Int freeCell) == false)
				return localSpawn;

			return mapLayout.CellToWorld(freeCell);
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

		// ── 파도 사이 드래프트 ────────────────────────────────────────────────────
		// ★ 왜 필요한가: 자원이 쌓이면 살 수 있는 걸 사는 구조는 고민이 아니라 *대기*다. 파도를 넘길 때마다
		//   세 장 중 하나를 반드시 고르게 하면, 포기한 두 장이 이번 판의 성격이 된다(Slot Theory 계열).
		// ★ 왜 판이 멈추나: 고르는 동안 마수가 오면 그건 선택이 아니라 벌칙이다. 카드가 걸린 동안 진행 규칙
		//   자체를 멈춘다(시간 배속을 건드리지 않는다 — 그건 사람이 쥔 손잡이라 여기서 뺏으면 안 된다).

		/// <summary> 새 카드가 걸렸다 — 화면이 구독해 띄운다. </summary>
		public event Action DraftOffered = delegate { };

		/// <summary> 지금 답을 기다리는 카드들(없으면 빈 목록). </summary>
		public IReadOnlyList<TowerDefenseBoon> PendingDraft => pendingDraft;

		/// <summary> 고를 것이 걸려 있는가 — 걸린 동안 파도가 오지 않는다. </summary>
		public bool IsDraftPending => pendingDraft.Count > 0;

		/// <summary> 지금까지 고른 것 한 줄 요약(없으면 빈 문자열). </summary>
		public string BoonSummary => boons.Describe();

		/// <summary> 지금까지 고른 장수. </summary>
		public int BoonCount => boons.TakenCount;

		private void OfferDraft()
		{
			if (stage == null || core == null)
				return;

			TowerDefenseDraftRules rules = stage.DraftRules;
			if (rules.IsEnabled == false)
				return;

			// 같은 판·같은 파도면 같은 세 장 — 「다시 뽑기」로 흔들 수 있으면 선택의 무게가 사라진다.
			TowerDefenseDraft.Offer(core.WaveIndex, MapSeed, rules, pendingDraft);
			if (pendingDraft.Count == 0)
				return;

			DraftOffered();
		}

		/// <summary>
		/// 카드 한 장 선택 — 지속 효과는 쌓이고, 즉시 효과(목숨·정수·자원)는 그 자리에서 들어온다.
		/// 고른 순간 판이 다시 흐른다.
		/// </summary>
		public bool ChooseBoon(int index)
		{
			if (core == null || index < 0 || index >= pendingDraft.Count)
				return false;

			TowerDefenseBoon boon = pendingDraft[index];
			boons.Take(boon);

			switch (boon.Kind)
			{
				case TowerDefenseBoonKind.Life:
					core.AddLives(Mathf.RoundToInt(boon.Magnitude));
					break;
				case TowerDefenseBoonKind.Essence:
					core.AddEssence(Mathf.RoundToInt(boon.Magnitude));
					break;
				case TowerDefenseBoonKind.Windfall:
					core.AddResource(Mathf.RoundToInt(boon.Magnitude));
					break;
				// 지속 효과는 boons 에 쌓인 것이 전부 — 아래에서 코어에 반영한다.
				default:
					break;
			}

			core.IncomeMultiplier = boons.IncomeMultiplier;
			pendingDraft.Clear();

			if (coreCombatant != null)
				PopWorldText("「" + boon.DisplayName + "」", coreCombatant.Position, TextType.Heal);
			Debug.Log($"{nameof(TowerDefenseMatch)}: 드래프트 선택 — {boon.DisplayName} ({boon.Note})");
			return true;
		}

		// ── 영웅 인형 ─────────────────────────────────────────────────────────────
		// ★ 왜 필요한가: 지금 개척은 「전부 미리 배치하고 지켜본다」라 교전 중에 사람이 할 일이 0 이다.
		//   움직이는 내 편이 하나 있으면 「부족한 곳을 내가 뛰어가 메운다」가 생긴다(Kingdom Rush 의 영웅).
		//   WM 은 본편에 이미 조종하는 인형이 있으니 **한 명만 데려간다**가 세계관 정합이다.
		// ★ 왜 포탑과 같은 표를 쓰나: 전투 수치를 따로 두면 두 곳이 갈라진다. 다른 점은 단 하나 — 움직인다.

		/// <summary> 영웅이 판에 있는가. </summary>
		public bool HasHero => heroActive && heroTransform != null;

		/// <summary> 영웅 현재 위치(없으면 코어 자리). </summary>
		public Vector3 HeroPosition => heroTransform != null ? heroTransform.position : activeCorePosition;

		/// <summary> 영웅을 그 자리로 보낸다 — 걸어간다(순간이동 X, 늦는 것 자체가 판단의 대가다). </summary>
		public bool CommandHero(Vector3 worldPosition)
		{
			if (HasHero == false)
				return false;

			heroTargetPosition = new Vector3(worldPosition.x, heroTransform.position.y, worldPosition.z);
			return true;
		}

		private IEnumerator SpawnHeroRoutine()
		{
			if (stage.HeroUnit == null || stage.HeroUnit.Prefab == null)
				yield break; // 영웅 미설정 스테이지 — 기존 판과 완전히 동일하게 진행.

			GameObject heroGameObject = pool.Spawn(stage.HeroUnit.Prefab);
			if (spawnedUnits.Contains(heroGameObject) == false)
				spawnedUnits.Add(heroGameObject);

			Vector3 spawnPosition = stageRoot.TransformPoint(activeCorePosition) + new Vector3(stage.GroundCellSize * 1.5f, 0f, 0f);
			heroGameObject.transform.position = spawnPosition;

			yield return null;
			if (core == null || targeting == null || pool == null)
				yield break;

			UnitObject heroUnitObject = heroGameObject.GetComponent<UnitObject>();
			if (heroUnitObject == null)
			{
				Debug.LogWarning($"{nameof(TowerDefenseMatch)}: {stage.HeroUnit.Prefab.name} 에 UnitObject 없음 — 영웅 없이 진행.");
				yield break;
			}

			heroUnitObject.Init(stage.HeroUnit);
			heroUnitObject.SkillHandler.AutoCastEnabled = false;

			heroCombatant = heroUnitObject.GetComponent<ArenaCombatant>();
			if (heroCombatant == null)
				heroCombatant = heroUnitObject.gameObject.AddComponent<ArenaCombatant>();
			heroCombatant.SetTeam(DEFENDER_TEAM, nextCombatantId++);

			ApplyReadability(heroUnitObject, stage.HeroTint, stage.HeroScale);

			heroGameObject.SetActive(true);

			// ★ 영웅의 자리는 *사람이 정한다*. 강체를 그대로 두면 내가 옮긴 좌표를 물리가 매 프레임 되돌린다
			//   (라이브 실측: 옮긴 다음 틱에 뒤로 밀려 제자리 — 명령해도 안 움직이는 것처럼 보였다).
			// ★ 켠 *다음 프레임*에 씌운다 — 스탯 배수와 같은 이유로, 켜기 전에 바꾼 값은 UnitObject.Start 의
			//   재-Init 규약에 조용히 덮인다(이 파일에서 이미 한 번 겪은 트랩).
			//   대여 계약(TowerDefenseUnitLease)이 반납 때 원래 값으로 되돌리므로 다음 대여(마수 등)에 안 샌다.
			yield return null;
			if (core == null || targeting == null || pool == null)
				yield break;

			Rigidbody heroBody = heroGameObject.GetComponent<Rigidbody>();
			if (heroBody != null)
			{
				heroBody.isKinematic = true;
				heroBody.useGravity = false;
			}

			// 본편 이동 시스템(NavMeshAgent/UnitMovement)은 개척에서 통째로 끈다 — 개척 지면은 런타임 생성이라
			// NavMesh 자체가 없고, 길찾기는 흐름장이 이미 한다. 켜두면 에이전트가 좌표를 도로 잡아당긴다(실측).
			UnityEngine.AI.NavMeshAgent heroAgent = heroGameObject.GetComponent<UnityEngine.AI.NavMeshAgent>();
			if (heroAgent != null)
				heroAgent.enabled = false;
			UnitMovement heroMovement = heroGameObject.GetComponent<UnitMovement>();
			if (heroMovement != null)
				heroMovement.enabled = false;

			foreach (UnitBrain brain in heroUnitObject.GetComponents<UnitBrain>())
				brain.enabled = false;

			if (stage.HeroArchetype != null)
			{
				TowerDefenseWeapon heroWeapon = heroUnitObject.GetComponent<TowerDefenseWeapon>();
				if (heroWeapon == null)
					heroWeapon = heroUnitObject.gameObject.AddComponent<TowerDefenseWeapon>();
				heroWeapon.Configure(stage.HeroArchetype, targeting, heroCombatant, waveEnemies,
					IsVisibleAt, () => TowerDamageMultiplier, () => Adaptation);
			}

			targeting.Register(heroCombatant);
			registeredCombatants.Add(heroCombatant);

			heroTransform = heroGameObject.transform;
			heroTargetPosition = heroTransform.position;
			heroActive = true;

			// 영웅에게도 이름이 있어야 「데려간 아이」가 된다 — 이름 없는 영웅은 커서다.
			RegisterDoll(heroTransform, stage.HeroTint);
			RefreshHeroVision();
		}

		/// <summary>
		/// 영웅 이동 + 움직이는 시야. 건물 시야는 지어질 때 한 번만 계산하면 되지만 영웅은 매 틱 자리가 바뀌므로
		/// **칸이 바뀐 순간에만** 다시 계산한다(매 틱 전면 재계산은 44칸 판에서 그냥 낭비다).
		/// </summary>
		private void TickHero()
		{
			if (HasHero == false)
				return;

			if (heroCombatant != null && heroCombatant.IsAlive == false)
			{
				heroActive = false; // 쓰러진 영웅은 그 판에선 끝 — 「한 명만 데려간다」의 무게.
				Debug.Log($"{nameof(TowerDefenseMatch)}: 영웅 쓰러짐 — 이번 판은 여기까지.");
				return;
			}

			Vector3 current = heroTransform.position;
			Vector3 delta = heroTargetPosition - current;
			delta.y = 0f;

			float step = stage.HeroMoveSpeed * TimeManager.TICK;
			if (delta.sqrMagnitude > step * step)
			{
				heroTransform.position = current + delta.normalized * step;
				RefreshHeroVision();
			}
		}

		private Vector2Int heroVisionCell = new Vector2Int(int.MinValue, int.MinValue);
		private int heroVisionSourceIndex = -1;

		private void RefreshHeroVision()
		{
			if (vision == null || mapLayout == null || stageRoot == null || heroTransform == null || stage.HeroVisionRadius <= 0f)
				return;

			Vector2Int cell = mapLayout.WorldToCell(stageRoot.InverseTransformPoint(heroTransform.position));
			if (cell == heroVisionCell)
				return;

			heroVisionCell = cell;
			TowerDefenseVision.Source source = new(cell, stage.HeroVisionRadius);

			// 영웅의 시야원은 *하나*다 — 지나간 자리마다 원을 남기면 판이 통째로 밝아진다(밝힌 자리는
			// Explored 로 남으므로 「가봤다」는 기록은 그대로 유지된다).
			if (heroVisionSourceIndex >= 0 && heroVisionSourceIndex < visionSources.Count)
				visionSources[heroVisionSourceIndex] = source;
			else
			{
				heroVisionSourceIndex = visionSources.Count;
				visionSources.Add(source);
			}

			RefreshVision();
		}

		// ── 이름 붙은 인형 ────────────────────────────────────────────────────────
		// ★ 왜 필요한가: 「광역 포탑」은 물건이고, 물건은 팔 때 아깝지 않다. 이름이 붙는 순간 같은 유닛이
		//   아이가 되어 잃는 것에 무게가 생긴다. 개척은 마녀가 인형을 데리고 나가는 이야기다.

		/// <summary> 화면에 띄울 이름표들 — 사라진 앵커는 조회 겸 정리(멱등). </summary>
		public IReadOnlyList<TowerDefenseDollLabel> DollLabels
		{
			get
			{
				for (int index = dollLabels.Count - 1; index >= 0; index--)
				{
					if (dollLabels[index].IsAlive == false)
						dollLabels.RemoveAt(index);
				}
				return dollLabels;
			}
		}

		/// <summary> 세워진 인형에게 이름을 준다 + 한 마디 시킨다. 같은 판·같은 순서면 같은 이름. </summary>
		private void RegisterDoll(Transform anchor, Color tint)
		{
			if (anchor == null)
				return;

			int ordinal = nextDollOrdinal++;
			string name = TowerDefenseNames.For(MapSeed, ordinal);
			dollLabels.Add(new TowerDefenseDollLabel(anchor, name, tint));
			PopWorldText("「" + name + "」 " + TowerDefenseNames.Greeting(MapSeed, ordinal), anchor.position, TextType.Heal);
		}

		/// <summary>
		/// 보급 원점(코어·전초기지)에 사거리 원 — 「사슬이 여기서 출발해 이만큼 닿는다」.
		/// 이 원이 없으면 채집을 어디에 세워야 이어지는지가 순수한 시행착오가 된다.
		/// </summary>
		private void ShowSupplyReachRing(Transform origin)
		{
			if (origin == null || stage == null || stage.SupplyReach <= 0f)
				return;

			Color ringColor = stage.HarvesterTint;
			ringColor.a = 0.18f;
			TowerDefenseRing ring = TowerDefenseRing.Create(origin, "SupplyReachRing", ringColor, 0.06f, 0.03f);
			ring.SetRadius(stage.SupplyReach);
		}

		/// <summary>
		/// 그 유닛이 무엇인지 사람 말로(툴팁). 화면에 서 있는 것이 「무엇이고 얼마나 버티는지」를 물어볼
		/// 수단이 없으면, 색과 크기만으로 짐작해야 한다(사용자 요청: 유닛 툴팁).
		/// 모르는 대상이면 빈 문자열 — 아무거나 지어내지 않는다.
		/// </summary>
		public string DescribeUnit(ArenaCombatant combatant)
		{
			if (combatant == null || combatant.UnitObject == null)
				return string.Empty;

			Transform unit = combatant.transform;
			int currentHp = combatant.UnitObject.UnitStat[UnitStatType.HP_CUR];
			int maxHp = combatant.UnitObject.UnitStat[UnitStatType.HP_MAX];

			// 마수 — 지금 얼마나 남았고 잡으면 얼마인지.
			if (combatant.TeamId == ATTACKER_TEAM)
			{
				string bounty = enemyBountyById.TryGetValue(combatant.CombatantId, out int reward)
					? "  ·  잡으면 +" + Mathf.RoundToInt(reward * boons.BountyMultiplier)
					: string.Empty;
				return "마수\n체력 " + currentHp + " / " + maxHp + bounty;
			}

			TowerDefenseDollLabel label = FindDollLabel(unit);
			string name = label != null ? label.Name : "인형";

			// 포탑 — 무기가 붙어 있으면 그 수치가 정본이다(화면과 규칙이 같은 곳을 읽는다).
			TowerDefenseWeapon weapon = unit.GetComponent<TowerDefenseWeapon>();
			if (weapon != null)
			{
				bool isHero = HasHero && heroTransform == unit;
				return (isHero ? name + " (영웅)" : name + (label != null && label.Level > 1 ? " ★" + label.Level : ""))
					+ "\n체력 " + currentHp + " / " + maxHp
					+ "\n사거리 " + weapon.Range.ToString("0.#") + "  ·  피해 " + weapon.CurrentDamage
					+ (isHero ? "\n핫바에서 「영웅 이동」을 고르고 찍으면 그리 간다" : "\n같은 자리에 같은 종류를 또 지으면 승급");
			}

			// 채집 인형 — 무엇을 얼마나 캐고, 이어져 있는지.
			if (harvesterTransforms.Contains(unit))
			{
				bool outer = harvesterIsOuter.TryGetValue(unit, out bool isOuter) && isOuter;
				bool connected = label == null || label.Disconnected == false;
				return name + " (채집 인형)"
					+ "\n체력 " + currentHp + " / " + maxHp
					+ "\n" + (outer ? "정수" : "자원") + " ×" + HarvesterMultiplierOf(unit).ToString("0.0")
					+ "\n" + (connected ? "보급 이어짐" : "⚠ 보급 끊김 — 한 푼도 안 들어온다");
			}

			if (coreCombatant == combatant)
				return "코어\n체력 " + currentHp + " / " + maxHp + "\n여기까지 새면 목숨이 준다";

			return name + "\n체력 " + currentHp + " / " + maxHp;
		}

		/// <summary> 그 자리 인형의 이름표(없으면 null) — 승급 단계 표시 갱신에 쓴다. </summary>
		private TowerDefenseDollLabel FindDollLabel(Transform anchor)
		{
			foreach (TowerDefenseDollLabel label in dollLabels)
			{
				if (label.Anchor == anchor)
					return label;
			}
			return null;
		}

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
		// 연구(판 안 건물)와 드래프트(파도 사이 선택)는 서로 다른 층이라 곱해진다 — 둘 다 쌓은 판이
		// 눈에 띄게 세지는 것이 「이 판은 화력으로 갔다」의 실체다.
		public float TowerDamageMultiplier =>
			(1f + LabCount * (stage != null ? stage.LabDamageBonus : 0f)) * boons.DamageMultiplier;

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
		/// <summary>
		/// 실제로 「샜다」로 치는 반경 — 설정값과 *마수가 멈춰 서는 거리* 중 큰 쪽.
		///
		/// ★ 왜 이게 필요한가 (사용자 실증: "몬스터가 멈춰서 안올때가 있음", 라이브 재현 170초):
		///   유출제에서 마수는 코어에 「닿으면」 사라진다. 그런데 마수는 코어를 *때리는 무기*를 갖고 있어서
		///   자기 사거리에 들어오는 순간 **거기서 멈춰 선다**. 그 사거리가 유출 반경보다 크면 마수는
		///   영원히 닿지 않고, 살아있는 마수가 0이 안 되니 **웨이브가 영영 안 끝난다**.
		///   「닿았다」의 기준을 마수가 실제로 멈추는 거리에서 뽑으면 두 숫자가 갈라질 수 없다.
		/// </summary>
		private float EffectiveLeakRadius
		{
			get
			{
				float stopDistance = 0f;
				if (stage != null && stage.EnemyTactic.Rules != null)
				{
					foreach (TacticRule rule in stage.EnemyTactic.Rules)
					{
						if (rule.Target.MaxRange > stopDistance)
							stopDistance = rule.Target.MaxRange;
					}
				}

				// 마수가 실제로 멈추는 자리는 둘 중 더 먼 쪽이다: 「사거리에 들어와서」 또는 「고리로 둘러싸서」.
				// 둘 다 덮지 않으면 바깥에 선 마수가 영영 안 닿아 파도가 끝나지 않는다(실측 2회).
				stopDistance = Mathf.Max(stopDistance, enemyMaxStopDistance);
				return Mathf.Max(stage.LeakRadius, stopDistance + stage.LeakRangeMargin);
			}
		}

		// 이번 매치 마수들이 목표에서 멈춰 서는 최대 거리 — 스폰 때 드라이버가 알려준 값.
		private float enemyMaxStopDistance;

		private void CullLeakedEnemies()
		{
			if (core == null || core.UsesLives == false || coreCombatant == null)
				return;

			float leakRadius = EffectiveLeakRadius;
			float leakRadiusSqr = leakRadius * leakRadius;

			for (int index = waveEnemies.Count - 1; index >= 0; index--)
			{
				ArenaCombatant enemy = waveEnemies[index];
				if (enemy == null || enemy.IsAlive == false)
					continue;
				if (IsAtAnyGoal(enemy.Position, leakRadiusSqr) == false)
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

		/// <summary>
		/// 보급 다시 계산 + 수입 반영. 건물이 서거나 사라질 때마다, 그리고 매 틱 부른다.
		/// 끊긴 채집은 수입이 0 — 「넓히면 번다」가 「넓히면 지킬 것이 는다」로 바뀌는 지점.
		/// </summary>
		private void RefreshSupply()
		{
			if (core == null || coreCombatant == null || stage == null)
				return;

			for (int index = supplyTransforms.Count - 1; index >= 0; index--)
			{
				if (supplyTransforms[index] == null)
					supplyTransforms.RemoveAt(index);
			}

			supplyBuildings.Clear();
			foreach (Transform building in supplyTransforms)
				supplyBuildings.Add(building.position);

			supplySeeds.Clear();
			supplySeeds.Add(coreCombatant.Position);
			foreach (Transform outpost in outposts)
			{
				if (outpost != null)
					supplySeeds.Add(outpost.position);
			}

			TowerDefenseSupply.Compute(supplySeeds, supplyBuildings, stage.SupplyReach, suppliedBuildings);

			float resourceWeight = 0f;
			float essenceWeight = 0f;
			DisconnectedHarvesters = 0;

			for (int index = 0; index < supplyTransforms.Count; index++)
			{
				Transform building = supplyTransforms[index];
				if (harvesterTransforms.Contains(building) == false)
					continue;

				bool connected = suppliedBuildings.Contains(index);

				// 끊긴 사실을 그 인형 머리 위에 붙인다 — 수입이 왜 안 오는지가 숫자가 아니라 *자리*로 보여야 한다.
				TowerDefenseDollLabel label = FindDollLabel(building);
				if (label != null)
					label.Disconnected = connected == false;

				if (connected == false)
				{
					DisconnectedHarvesters++;
					continue;
				}

				float multiplier = HarvesterMultiplierOf(building);
				if (harvesterIsOuter.TryGetValue(building, out bool outer) && outer)
					essenceWeight += multiplier;
				else
					resourceWeight += multiplier;
			}

			core.SetHarvesterWeights(resourceWeight, essenceWeight);

			OuterHarvesters = 0;
			SuppliedOuterHarvesters = 0;
			for (int index = 0; index < supplyTransforms.Count; index++)
			{
				Transform building = supplyTransforms[index];
				if (harvesterIsOuter.TryGetValue(building, out bool outer) == false || outer == false)
					continue;

				OuterHarvesters++;
				if (suppliedBuildings.Contains(index))
					SuppliedOuterHarvesters++;
			}
		}

		/// <summary> 보급이 끊긴 채집 인형 수 — 화면이 「왜 수입이 줄었나」를 말해줘야 한다. </summary>
		public int DisconnectedHarvesters { get; private set; }

		/// <summary> 코어까지 이어진 건물 수 — 검증·진단용. </summary>
		public int SuppliedBuildings => suppliedBuildings.Count;

		/// <summary> 보급 사슬 후보 건물 수 — 「사슬이 비었나 / 안 닿나」를 가르는 진단값. </summary>
		public int SupplyBuildingCount => supplyTransforms.Count;

		/// <summary>
		/// 바깥 노드에 선 채집 수 / 그중 보급이 이어진 수.
		/// ★ 정수가 0일 때 원인이 셋 중 어느 것인지 갈라준다: ① 바깥에 안 세웠다 ② 세웠는데 안 이어졌다
		///   ③ 둘 다 됐는데 안 들어온다(진짜 결함). 이 구분이 없으면 「바깥 노드인데 정수가 안 나온다」 같은
		///   *거짓 실패*가 계속 찍힌다(실측: 실제로는 바깥에 세운 적이 없었다).
		/// </summary>
		public int OuterHarvesters { get; private set; }
		public int SuppliedOuterHarvesters { get; private set; }

		private float HarvesterMultiplierOf(Transform harvester)
		{
			for (int index = 0; index < activeNodePositions.Count; index++)
			{
				Vector3 nodeWorld = stageRoot.TransformPoint(activeNodePositions[index]);
				if ((nodeWorld - harvester.position).sqrMagnitude <= 1f)
					return NodeIncomeMultiplierAt(index);
			}
			return 1f;
		}

		// 유출 지점은 코어만이 아니다 — 전초기지도 지켜야 할 곳이다(넓힌 만큼 늘어난다).
		private bool IsAtAnyGoal(Vector3 position, float radiusSqr)
		{
			if ((position - coreCombatant.Position).sqrMagnitude <= radiusSqr)
				return true;

			foreach (Transform outpost in outposts)
			{
				if (outpost != null && (position - outpost.position).sqrMagnitude <= radiusSqr)
					return true;
			}
			return false;
		}

		/// <summary>
		/// 전초기지 세우기 — 정수로만. 세우는 순간 ① 마수가 향하는 목표가 하나 늘고
		/// ② 보급의 새 원점이 생기고 ③ 시야가 넓어진다. 「넓히면 벌지만 지킬 곳이 는다」가 한 건물에 들어있다.
		/// </summary>
		public bool TryPlaceOutpost(Vector3 worldPosition)
		{
			if (core == null || mapLayout == null || stageRoot == null)
				return false;

			Vector3Int cellKey = ToCellKey(worldPosition);
			if (occupiedCells.Contains(cellKey))
				return Reject("여긴 이미 찼다", worldPosition);
			if (IsObstacleAt(worldPosition))
				return Reject("암반 위엔 못 짓는다", worldPosition);

			if (core.TrySpendEssence(stage.OutpostEssenceCost) == false)
				return Reject($"정수 부족 {core.Essence}/{stage.OutpostEssenceCost}", worldPosition);

			occupiedCells.Add(cellKey);

			GameObject outpostObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
			outpostObject.name = "Outpost";
			outpostObject.transform.SetParent(stageRoot, false);
			outpostObject.transform.position = worldPosition + new Vector3(0f, 0.6f, 0f);
			outpostObject.transform.localScale = new Vector3(1.1f, 1.2f, 1.1f);

			Renderer outpostRenderer = outpostObject.GetComponent<Renderer>();
			if (outpostRenderer != null)
			{
				Material material = new Material(outpostRenderer.sharedMaterial);
				material.color = stage.OutpostTint;
				if (material.HasProperty("_BaseColor"))
					material.SetColor("_BaseColor", stage.OutpostTint);
				outpostRenderer.sharedMaterial = material;
			}

			outposts.Add(outpostObject.transform);
			supplyTransforms.Add(outpostObject.transform);
			ShowSupplyReachRing(outpostObject.transform); // 새 원점이므로 새 사거리 원.
			AddVisionSource(worldPosition, stage.OutpostVisionRadius);
			RebuildPathing(); // 목표가 늘었으므로 마수의 길이 통째로 바뀐다.
			RefreshSupply();
			return true;
		}

		/// <summary> 세운 전초기지 수 — 지킬 곳의 개수. </summary>
		public int OutpostCount => outposts.Count;

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

		// 굳은 마수 감시 — CombatantId → (마지막 자리, 그 자리에 머문 시간).
		private readonly Dictionary<int, (Vector3 Position, float Seconds)> enemyStillness = new();

		/// <summary>
		/// 굳은 마수를 풀어준다(사용자 실증: "몬스터가 멈춰서 안올때가 있음").
		///
		/// ★ 왜 이게 치명적인가: 웨이브 종료 조건이 「살아있는 마수 0」이라 **한 마리만 굳어도 판이 영영 안 끝난다**.
		///   무대 밖 이탈(CullEscapedEnemies)은 이미 막고 있지만, *무대 안에서 제자리에 붙는* 경우는 안 잡혔다.
		/// ★ 왜 굳는가: 스폰 분산이 마수를 암반 칸 위/뒤에 떨궈 흐름장이 「거기서는 갈 수 없다」고 답하면
		///   안내가 끊기고, 그 자리에서 직선으로 벽을 밀며 영원히 버틴다.
		/// ★ 그래서 두 겹으로 막는다: ① 스폰 자체를 갈 수 있는 칸으로 스냅(SnapToReachable) ② 그래도 굳으면
		///   가장 가까운 갈 수 있는 칸으로 옮겨준다. 옮긴 사실은 로그로 남긴다 — 조용히 순간이동시키면
		///   다음에 같은 원인이 생겨도 아무도 모른다.
		/// </summary>
		private void UnstickEnemies()
		{
			if (mapLayout == null || flowField == null || stageRoot == null)
				return;

			float threshold = stage.StuckRelocateSeconds;
			if (threshold <= 0f)
				return;

			float moveEpsilonSqr = stage.StuckMoveEpsilon * stage.StuckMoveEpsilon;

			foreach (ArenaCombatant enemy in waveEnemies)
			{
				if (enemy == null || enemy.IsAlive == false)
					continue;

				Vector3 position = enemy.Position;
				if (enemyStillness.TryGetValue(enemy.CombatantId, out (Vector3 Position, float Seconds) tracked) == false)
				{
					enemyStillness[enemy.CombatantId] = (position, 0f);
					continue;
				}

				if ((position - tracked.Position).sqrMagnitude > moveEpsilonSqr)
				{
					enemyStillness[enemy.CombatantId] = (position, 0f);
					continue;
				}

				float stillSeconds = tracked.Seconds + TimeManager.TICK;
				if (stillSeconds < threshold)
				{
					enemyStillness[enemy.CombatantId] = (tracked.Position, stillSeconds);
					continue;
				}

				Vector2Int cell = mapLayout.WorldToCell(stageRoot.InverseTransformPoint(position));
				bool blocked = IsPathBlocked(cell);
				bool reachable = flowField.IsReachable(cell);

				if (TrySnapToReachable(cell, out Vector2Int freeCell))
				{
					enemy.transform.position = stageRoot.TransformPoint(mapLayout.CellToWorld(freeCell));
					Debug.LogWarning($"{nameof(TowerDefenseMatch)}: 굳은 마수를 옮김 — cell={cell} blocked={blocked} "
						+ $"reachable={reachable} → {freeCell} ({stillSeconds:F1}s 정지)");
				}
				else if (reachable && flowField.TryGetNextCell(cell, out Vector2Int nextCell))
				{
					// 길은 멀쩡한데 안 움직인다 = 서로 밀치다 끼었다(스폰 지점에 몰릴 때 실제로 난다).
					// 길을 다시 그려줄 게 아니라 *다음 칸으로 한 발 밀어준다* — 원인을 숨기지 않게 로그는 남긴다.
					enemy.transform.position = stageRoot.TransformPoint(mapLayout.CellToWorld(nextCell));
					Debug.LogWarning($"{nameof(TowerDefenseMatch)}: 길 위에서 끼인 마수를 한 칸 밀어줌 — {cell} → {nextCell} "
						+ $"({stillSeconds:F1}s 정지)");
				}
				else
				{
					Debug.LogWarning($"{nameof(TowerDefenseMatch)}: 굳은 마수 주변에 갈 수 있는 칸이 없음 — cell={cell} "
						+ $"blocked={blocked} ({stillSeconds:F1}s 정지)");
				}

				enemyStillness[enemy.CombatantId] = (enemy.Position, 0f);
			}
		}

		/// <summary> 그 칸에서 가장 가까운 「갈 수 있는」 칸 — 나선으로 넓혀 찾는다(없으면 false). </summary>
		private bool TrySnapToReachable(Vector2Int cell, out Vector2Int result)
		{
			result = cell;
			if (flowField.IsReachable(cell))
				return false; // 이미 갈 수 있는 자리 — 굳은 원인이 길이 아니다(옮겨도 소용 없다).

			for (int radius = 1; radius <= stage.StuckSearchRadius; radius++)
			{
				for (int offsetX = -radius; offsetX <= radius; offsetX++)
				{
					for (int offsetY = -radius; offsetY <= radius; offsetY++)
					{
						if (Mathf.Abs(offsetX) != radius && Mathf.Abs(offsetY) != radius)
							continue; // 테두리만 — 안쪽은 이전 반경에서 이미 봤다.

						Vector2Int candidate = new(cell.x + offsetX, cell.y + offsetY);
						if (flowField.IsReachable(candidate) == false)
							continue;

						result = candidate;
						return true;
					}
				}
			}

			return false;
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
				bounty = Mathf.RoundToInt(bounty * boons.BountyMultiplier); // 드래프트로 고른 「사냥의 값」.
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

		/// <summary>
		/// 마지막으로 배치가 거절된 이유 — 화면·로그가 같은 문장을 쓴다.
		///
		/// ★ 왜 필요한가 (사용자 실증: "전초지기랑 연구소 설치 안됨"): 거절이 전부 조용한 false 였다.
		///   정수가 없어서인지, 이미 뭐가 서 있어서인지, 암반 위라서인지 화면이 한 마디도 안 하면
		///   플레이어에게는 「그 칸이 고장났다」로 보인다. 못 짓는 것보다 *이유를 모르는 것*이 더 나쁘다.
		/// </summary>
		public string LastRejectReason { get; private set; } = string.Empty;

		// 거절 = 이유를 그 자리에 띄우고 false. 모든 거절 경로가 이 하나를 지난다(조용한 false 금지).
		private bool Reject(string reason, Vector3 worldPosition)
		{
			LastRejectReason = reason;
			PopWorldText(reason, worldPosition, TextType.Warning);
			// 로그에도 남긴다 — 화면 글자는 흘러가고, 「왜 안 지어졌나」는 나중에 되짚어야 할 때가 온다.
			Debug.Log($"{nameof(TowerDefenseMatch)}: 배치 거절 — {reason} @ {worldPosition}");
			return false;
		}

		/// <summary>
		/// 파도를 넘길 때마다 내 편(코어·인형·영웅)을 최대 체력의 일정 비율만큼 회복시킨다(사용자 요청).
		///
		/// ★ 왜 필요한가: 지금은 한 번 긁힌 인형이 판이 끝날 때까지 그 체력으로 산다. 그러면 「버텼다」의
		///   보상이 없고, 앞줄에 세운 인형은 필연적으로 죽으니 앞에 세우는 선택 자체가 손해가 된다.
		///   파도 사이 회복이 있으면 「이번엔 버틸 수 있나」가 매 파도의 계산이 된다.
		/// ★ 완전 회복이 아닌 이유: 그러면 피해가 아무 의미가 없어져 방어선의 소모전이 사라진다.
		/// </summary>
		private void HealDefenders()
		{
			if (stage == null || stage.DefenderHealPerWave <= 0f)
				return;

			foreach (ICombatant combatant in registeredCombatants)
			{
				if (combatant is not ArenaCombatant defender || defender.IsAlive == false)
					continue;
				if (defender.TeamId != DEFENDER_TEAM || defender.UnitObject == null)
					continue;

				UnitHealth health = defender.UnitObject.GetComponent<UnitHealth>();
				if (health == null)
					continue;

				int maxHp = defender.UnitObject.UnitStat[UnitStatType.HP_MAX];
				int currentHp = defender.UnitObject.UnitStat[UnitStatType.HP_CUR];
				if (currentHp >= maxHp)
					continue;

				int healAmount = Mathf.Max(1, Mathf.RoundToInt(maxHp * stage.DefenderHealPerWave));
				health.ReceiveHeal(healAmount);
				PopWorldText("+" + Mathf.Min(healAmount, maxHp - currentHp), defender.Position, TextType.Heal);
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

			if (IsObstacleAt(worldPosition))
				return Reject("암반 위엔 못 짓는다", worldPosition);
			if (core.TrySpend(TowerCostAt(towerIndex)) == false)
				return Reject($"자원 부족 {core.Resource}/{TowerCostAt(towerIndex)}", worldPosition);

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
				return Reject("자원 노드 위에만 선다", worldPosition); // 자원 무변경(스펙#C).

			Vector3Int cellKey = ToCellKey(nodeWorldPosition);
			if (occupiedCells.Contains(cellKey))
				return Reject("이 노드는 이미 잡혔다", nodeWorldPosition);

			if (core.TrySpend(stage.HarvesterCost) == false)
				return Reject($"자원 부족 {core.Resource}/{stage.HarvesterCost}", nodeWorldPosition);

			claimedNodes.Add(nodeIndex); // TrySpend 성공 후에만 점유 확정(스펙 지시 — 실패 시 점유 안 남김).
			occupiedCells.Add(cellKey);
			float incomeMultiplier = nodeIndex < activeNodeIncomeMultipliers.Count ? activeNodeIncomeMultipliers[nodeIndex] : 1f;
			bool outerNode = nodeIndex < activeNodeIsOuter.Count && activeNodeIsOuter[nodeIndex];
			// 등급은 인형이 실제로 생긴 뒤 *그 인형에* 붙인다(스폰이 코루틴이라 지금은 아직 없다).
			StartCoroutine(SpawnDefensiveUnitRoutine(stage.HarvesterUnit, null, nodeWorldPosition, isHarvester: true, incomeMultiplier,
				towerArchetype: null, isLab: false, isOuterNode: outerNode));
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
				ReleaseNodeAt(sold.transform.position);
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

			supplyTransforms.Remove(sold.transform);
			ReleaseUnit(pool, sold);
			spawnedUnits.Remove(sold);
			RefreshSupply(); // 사슬 중간이 사라지면 그 너머가 통째로 끊긴다.
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
				return Reject("여긴 이미 찼다", worldPosition);
			if (IsObstacleAt(worldPosition))
				return Reject("암반 위엔 못 짓는다", worldPosition);

			// 연구는 정수로만 — 강화의 통로를 바깥 노드(개척)에 묶는다.
			if (core.TrySpendEssence(stage.LabEssenceCost) == false)
				return Reject($"정수 부족 {core.Essence}/{stage.LabEssenceCost}", worldPosition);

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

		private IEnumerator SpawnDefensiveUnitRoutine(Unit unitData, TacticProgram tactic, Vector3 worldPosition, bool isHarvester, float incomeMultiplier = 1f, TowerDefenseTowerArchetype towerArchetype = null, bool isLab = false, bool isOuterNode = false)
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

			// 세운 인형에게 이름 — 벽·함정은 물건이지만 인형은 아이다(이 경로로 오는 것은 전부 인형).
			RegisterDoll(unitGameObject.transform,
				isLab ? stage.LabTint
					: isHarvester ? stage.HarvesterTint
					: (towerArchetype != null ? towerArchetype.Tint : stage.TowerTint));

			// 모든 내 건물이 보급 사슬의 징검다리 — 포탑을 늘어놓는 것이 곧 보급선을 잇는 일이 된다.
			supplyTransforms.Add(unitGameObject.transform);

			if (isHarvester)
			{
				harvesterTransforms.Add(unitGameObject.transform);
				harvesterIsOuter[unitGameObject.transform] = isOuterNode;
			}

			RefreshSupply(); // 수입은 「지을 때 더한다」가 아니라 「지금 몇 개가 이어져 있나」로 정해진다.
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
