using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// SimCity Phase 1 step5+6 — 존/도로 페인트 (가시화) + 시간 흐르면 건물 자동 성장.
	//
	// step5: GameMode.Zone/Road 진입 시 Click0=페인트 / Click1=해제 → WorldStage.ZoneGrid / RoadGraph
	// (substrate step1-4) 에 쓰고 셀마다 색 큐브 spawn. 좌표계 = BuildManager 의 런타임 Grid 재사용
	// (건물 배치와 동일 셀). 클릭 셀 = 카메라 ray ∩ 도시 평면(grid Y) — perspective 카메라 정합.
	//
	// step6: WorldClock.OnDayChanged 구독 → 매일 (ZoneGrid 카운트 → RciDemandModel 수요) 평가 →
	// 수요>임계 인 존타입의 "빈 존셀(존 칠해졌으나 건물 0) + 도로 인접" 셀을 cap 만큼 건물로 승격(시각
	// 건물 큐브 spawn). 수요<-임계 면 해당 존타입 건물 1개 쇠퇴(철거). 공간 신호 정교화(셀별
	// desirability)·전용 Building SO 매핑은 Phase 2 deferred(TASK-WM-164 기록) — 여기선 MVP 단순화.
	//
	// 모드 진입 = [ContextMenu] 수동 트리거 (정식 단축키는 후속 — slot A InputManager enum 무접촉).
	public class CityPaintManager : MonoBehaviour
	{
		[Header("Tile Visual")]
		[Tooltip("페인트 셀 큐브 한 변 비율 (1 = 셀 꽉 참).")]
		[SerializeField] private float cellTileScale = 0.9f;
		[Tooltip("존/도로 타일 두께 (납작한 판).")]
		[SerializeField] private float cellTileHeight = 0.1f;
		[Tooltip("자동 성장한 건물 큐브 높이.")]
		[SerializeField] private float buildingHeight = 1.0f;

		[SerializeField] private Color residentialColor = new(0.40f, 0.85f, 0.40f);
		[SerializeField] private Color commercialColor = new(0.40f, 0.60f, 1.00f);
		[SerializeField] private Color industrialColor = new(1.00f, 0.85f, 0.30f);
		[SerializeField] private Color roadColor = new(0.35f, 0.35f, 0.35f);

		[Header("RCI Demand 계수 (수치 노출 — RciDemandSO 도입 전 코드 기본값)")]
		[SerializeField] private float residentsPerJob = 1.0f;
		[SerializeField] private float shopsPerResident = 0.3f;
		[SerializeField] private float industryPerResident = 0.2f;
		[Tooltip("외부 이주 기반 주거 수요 — 일자리 없어도 새 도시로 사람 유입(빈 도시 주거 부트스트랩, 산업 exportBaseline 의 주거 대응).")]
		[SerializeField] private float immigrationBaseline = 5.0f;
		[SerializeField] private float exportBaseline = 5.0f;
		[SerializeField] private float demandGain = 0.1f;
		[Tooltip("수요가 이 값 넘으면 성장, -이 값 밑이면 쇠퇴.")]
		[SerializeField] private float growthThreshold = 0.2f;
		[Tooltip("하루에 한 존타입당 성장/쇠퇴할 최대 셀 수 (폭증 방지).")]
		[SerializeField] private int maxChangePerDayPerZone = 2;

		[Header("INC-5c 경제 틱 (임시 코드 레시피 — ResourceSO/ProductionSO·스킨 deferred)")]
		[Tooltip("산업 1동이 하루 채취하는 원자재(무입력 = 외부 수출).")]
		[SerializeField] private float industrialRawOutput = 2f;
		[Tooltip("상업 1동이 하루 소비하는 원자재.")]
		[SerializeField] private float commercialRawInput = 1f;
		[Tooltip("상업 1동이 하루 생산하는 재화.")]
		[SerializeField] private float commercialGoodsOutput = 1f;
		[Tooltip("상업 1동 만가동에 필요한 노동력.")]
		[SerializeField] private float commercialLaborPerBuilding = 2f;
		[Tooltip("주거 1동이 하루 소비하는 재화.")]
		[SerializeField] private float residentialGoodsConsumption = 0.5f;
		[Tooltip("주거 1동이 공급하는 노동력.")]
		[SerializeField] private float workersPerResidence = 4f;

		[Header("INC-7 통근 시민 (placeholder 큐브 — 실 prefab/sprite/표시명=스킨 deferred)")]
		[Tooltip("시민 placeholder 큐브 한 변.")]
		[SerializeField] private float citizenSize = 0.35f;
		[Tooltip("시민이 지면 위로 떠 있는 높이(중심).")]
		[SerializeField] private float citizenHeight = 0.4f;
		[Tooltip("시민 이동 속도 (셀/초).")]
		[SerializeField] private float citizenSpeed = 2.0f;
		[SerializeField] private Color citizenColor = new(0.95f, 0.85f, 0.55f);

		private InputManager inputManager;
		private GameModeManager gameModeManager;
		private StageManager stageManager;
		// BuildManager 의 런타임 Grid 재사용 — 도시 페인트가 건물 배치와 동일 셀 좌표계. 사용자 Grid 연결 불요.
		private BuildManager buildManager;

		[Inject]
		public void Construct(InputManager inputManager, GameModeManager gameModeManager, StageManager stageManager, BuildManager buildManager)
		{
			this.inputManager = inputManager;
			this.gameModeManager = gameModeManager;
			this.stageManager = stageManager;
			this.buildManager = buildManager;
		}

		// 셀 → 시각 큐브 (ZoneGrid/RoadGraph 가 데이터 진실, 이건 그 투영 = 렌더 캐시).
		private readonly Dictionary<Vector3Int, GameObject> cellVisuals = new();
		// 자동 성장한 건물 시각 큐브 — **projection only (렌더 캐시)**. 집계/성장/쇠퇴 판정의 진실은
		// GridData(CityCellQuery 경유). (구조 리뷰: 시각 캐시를 진실로 쓰면 save/load 후 갈라짐.)
		private readonly Dictionary<Vector3Int, GameObject> buildingVisuals = new();
		private readonly Dictionary<Color, Material> materialCache = new();
		private Material templateMaterial;
		private Transform visualRoot;
		private readonly RciDemandModel demandModel = new();
		private readonly CityGrowthSystem growthSystem = new();
		private readonly CitySimulationSystem simulationSystem = new();

		// INC-7 — 통근 시민 placeholder 에이전트. key = 집 셀(1 주거 = 1 시민). 진실 = CitizenRegistry,
		// 이건 그 시각 투영(렌더+이동). CitizenRegistry 가 비면 여기도 빔.
		private sealed class CitizenAgent
		{
			public CommutePathFollower Follower;
			public GameObject Visual;
		}
		private readonly Dictionary<Vector3Int, CitizenAgent> citizenAgents = new();

		// 4-이웃 오프셋 (존/직장 셀의 인접 도로 찾기 — RoadGraph NEIGHBOR_OFFSETS 와 동일 평면).
		private static readonly Vector3Int[] FOUR_NEIGHBORS =
		{
			new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0),
		};

		public ZoneType SelectedZoneType { get; set; } = ZoneType.Residential;

		private void Awake()
		{
			visualRoot = new GameObject("CityPaintVisuals").transform;
			visualRoot.SetParent(transform, false);

			GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
			templateMaterial = probe.GetComponent<Renderer>().sharedMaterial;
			Destroy(probe);
		}

		private void Start()
		{
			gameModeManager.OnModeChanged += OnModeChanged;
			ApplyMode(gameModeManager.CurrentMode);

			// step6 — 매일 수요 평가 + 자동 성장. WorldClock(MonoBehaviour singleton)은 Bootstrap 후 존재.
			if (WorldClock.Instance != null)
				WorldClock.Instance.OnDayChanged += OnDayChanged;
		}

		private void OnDestroy()
		{
			if (gameModeManager != null)
				gameModeManager.OnModeChanged -= OnModeChanged;
			if (WorldClock.Instance != null)
				WorldClock.Instance.OnDayChanged -= OnDayChanged;
		}

		private void OnModeChanged(GameMode mode) => ApplyMode(mode);

		private void ApplyMode(GameMode mode)
		{
			inputManager.UnregisterInputEvent(InputEventType.Click0, InputEventResponseType.Get, OnClickPaint);
			inputManager.UnregisterInputEvent(InputEventType.Click1, InputEventResponseType.Get, OnClickErase);

			if (mode == GameMode.Zone || mode == GameMode.Road)
			{
				inputManager.RegisterInputEvent(InputEventType.Click0, InputEventResponseType.Get, OnClickPaint);
				inputManager.RegisterInputEvent(InputEventType.Click1, InputEventResponseType.Get, OnClickErase);
			}
		}

		private bool TryGetTargetCell(out Vector3Int cell, out WorldStage worldStage)
		{
			cell = default;
			worldStage = null;

			if (inputManager.IsPointerOverUI())
				return false;

			if (stageManager.CurStage is WorldStage stage == false)
				return false;

			worldStage = stage;

			Camera camera = Camera.main;
			if (camera == null)
				return false;

			Grid grid = buildManager.Grid;
			Ray ray = camera.ScreenPointToRay(inputManager.MouseScreenPosition);
			Plane groundPlane = new(Vector3.up, grid.transform.position);
			if (groundPlane.Raycast(ray, out float enter) == false)
				return false;

			Vector3 groundPoint = ray.GetPoint(enter);
			cell = grid.WorldToCell(groundPoint);
			return true;
		}

		private void OnClickPaint()
		{
			if (TryGetTargetCell(out Vector3Int cell, out WorldStage worldStage) == false)
				return;

			// 셀 점유 규칙 중앙 판정 (road XOR zone/building) — 충돌 페인트 차단.
			CityPlacementService placement = new(worldStage.GridData, worldStage.ZoneGrid, worldStage.RoadGraph);

			if (gameModeManager.CurrentMode == GameMode.Zone)
			{
				if (placement.CanPlaceZone(cell) == false)
					return; // 도로 셀엔 존 X

				worldStage.ZoneGrid.Paint(cell, SelectedZoneType);
				SetCellVisual(cell, ZoneColor(SelectedZoneType));
			}
			else if (gameModeManager.CurrentMode == GameMode.Road)
			{
				if (placement.CanPlaceRoad(cell) == false)
					return; // 존/건물 셀엔 도로 X

				worldStage.RoadGraph.AddRoad(cell);
				SetCellVisual(cell, roadColor);
			}
		}

		private void OnClickErase()
		{
			if (TryGetTargetCell(out Vector3Int cell, out WorldStage worldStage) == false)
				return;

			worldStage.ZoneGrid.Clear(cell);
			worldStage.RoadGraph.RemoveRoad(cell);
			ClearCellVisual(cell);
			ClearBuildingVisual(cell);
		}

		// step6 — 매일 호출. 수요 평가 → 성장/쇠퇴 결정(CityGrowthSystem 순수) → 적용(GridData + 시각).
		// ★ 수요 입력·성장 판정 = GridData(진실, CityCellQuery 경유) — buildingVisuals(시각 캐시) 아님.
		//   (구조 리뷰 2026-05-31: 시각 캐시를 진실로 쓰면 save/load 후 갈라짐. 집계/판정은 데이터 소스로 통일.
		//    결정(순수 CityGrowthSystem)과 적용(여기 MonoBehaviour)도 분리 — EditMode 검증 가능.)
		private void OnDayChanged(int day)
		{
			if (stageManager.CurStage is WorldStage worldStage == false)
				return;

			CityCellQuery query = new(worldStage.GridData, worldStage.ZoneGrid, worldStage.RoadGraph);
			RciDemandCoefficients coefficients = new(residentsPerJob, shopsPerResident, industryPerResident, immigrationBaseline, exportBaseline, demandGain);
			RciDemand demand = demandModel.Evaluate(
				query.CountBuildingsByZone(ZoneType.Residential),
				query.CountBuildingsByZone(ZoneType.Commercial),
				query.CountBuildingsByZone(ZoneType.Industrial),
				coefficients);

			CityGrowthDecision decision = growthSystem.Decide(demand, query, growthThreshold, maxChangePerDayPerZone);
			ApplyGrowth(worldStage, decision);

			// INC-5c — 성장 반영된 도시로 하루치 생산/소비 → CityEconomy 재고 갱신 (query 는 live = post-growth 카운트).
			RunEconomyTick(worldStage, query, day);

			// INC-7 — 주거↔직장 통근 시민 동기화 (registry = 진실, 시각 에이전트 spawn/despawn).
			SyncCitizens(worldStage, query);
		}

		// 자원 카탈로그 임시 id (ResourceSO 도입 전 — 스킨 deferred). 0=원자재, 1=재화.
		private const int RAW_RESOURCE = 0;
		private const int GOODS_RESOURCE = 1;

		// INC-5c — 하루치 경제 흐름: 산업(원자재 채취) → 상업(원자재+노동→재화) → 주거(재화 소비). 공급망 순서.
		private void RunEconomyTick(WorldStage worldStage, CityCellQuery query, int day)
		{
			int residential = query.CountBuildingsByZone(ZoneType.Residential);
			int commercial = query.CountBuildingsByZone(ZoneType.Commercial);
			int industrial = query.CountBuildingsByZone(ZoneType.Industrial);

			ResourceId raw = new(RAW_RESOURCE);
			ResourceId goods = new(GOODS_RESOURCE);

			List<ProductionOrder> orders = new()
			{
				// 산업: 무입력 → 원자재 (채취/외부 수출, 부트스트랩).
				new ProductionOrder(new ProductionRecipe(
					new List<ResourceFlow>(),
					new List<ResourceFlow> { new(raw, industrialRawOutput) },
					0f), industrial),
				// 상업: 원자재 + 노동 → 재화.
				new ProductionOrder(new ProductionRecipe(
					new List<ResourceFlow> { new(raw, commercialRawInput) },
					new List<ResourceFlow> { new(goods, commercialGoodsOutput) },
					commercialLaborPerBuilding), commercial),
				// 주거: 재화 소비 (출력 없음) — 노동력 공급원.
				new ProductionOrder(new ProductionRecipe(
					new List<ResourceFlow> { new(goods, residentialGoodsConsumption) },
					new List<ResourceFlow>(),
					0f), residential),
			};

			float availableLabor = residential * workersPerResidence;
			simulationSystem.RunDay(worldStage.CityEconomy, orders, availableLabor);

			// no-news=bad-news: 매일 1줄 (0건이어도) — 경제 흐름 가시화.
			Debug.Log($"[City/Econ day{day}] R{residential} C{commercial} I{industrial} labor{availableLabor:F0} | RAW {worldStage.CityEconomy.GetStock(raw):F1} GOODS {worldStage.CityEconomy.GetStock(goods):F1}");
		}

		// INC-7 — 매 프레임 시민 이동(placeholder 큐브를 통근 경로 위로 왕복). 도시 살아있는 체감의 가시 레이어.
		private void Update()
		{
			if (citizenAgents.Count == 0)
				return;

			float step = citizenSpeed * Time.deltaTime;
			foreach (CitizenAgent agent in citizenAgents.Values)
			{
				agent.Follower.Advance(step);
				agent.Follower.CurrentSegment(out Vector3Int fromCell, out Vector3Int toCell, out float t);

				Vector3 fromPos = buildManager.GetWorldPosition(fromCell);
				Vector3 toPos = buildManager.GetWorldPosition(toCell);
				Vector3 pos = Vector3.Lerp(fromPos, toPos, t);
				agent.Visual.transform.position = new Vector3(pos.x, citizenHeight, pos.z);
			}
		}

		// INC-7 — registry(진실) ↔ 도시 상태 동기화 + 시각 에이전트 spawn/despawn. (1 주거 = 1 시민.)
		private void SyncCitizens(WorldStage worldStage, CityCellQuery query)
		{
			CitizenRegistry registry = worldStage.CitizenRegistry;

			// 1) 집 건물 사라진 시민 제거.
			HashSet<Vector3Int> residentialHomes = new(query.BuiltCells(ZoneType.Residential));
			registry.Citizens.RemoveAll(citizen => residentialHomes.Contains(citizen.HomeCell) == false);

			// 2) 시민 없는 주거에 도달 가능한 직장 배정해 추가.
			HashSet<Vector3Int> homed = new();
			foreach (CitizenSaveData citizen in registry.Citizens)
				homed.Add(citizen.HomeCell);

			foreach (Vector3Int home in residentialHomes)
			{
				if (homed.Contains(home))
					continue;
				if (TryAssignWork(worldStage, query, home, out Vector3Int work))
					registry.Add(new CitizenSaveData(home, work, CitizenState.GoingToWork));
			}

			// 3) 시각 에이전트 = registry 투영(spawn/despawn).
			SyncCitizenVisuals(worldStage, registry);
		}

		private void SyncCitizenVisuals(WorldStage worldStage, CitizenRegistry registry)
		{
			HashSet<Vector3Int> registryHomes = new();
			foreach (CitizenSaveData citizen in registry.Citizens)
				registryHomes.Add(citizen.HomeCell);

			List<Vector3Int> stale = new();
			foreach (KeyValuePair<Vector3Int, CitizenAgent> entry in citizenAgents)
				if (registryHomes.Contains(entry.Key) == false)
					stale.Add(entry.Key);
			foreach (Vector3Int home in stale)
			{
				Destroy(citizenAgents[home].Visual);
				citizenAgents.Remove(home);
			}

			foreach (CitizenSaveData citizen in registry.Citizens)
			{
				if (citizenAgents.ContainsKey(citizen.HomeCell))
					continue;
				if (TryBuildCommutePath(worldStage, citizen.HomeCell, citizen.WorkCell, out List<Vector3Int> path) == false)
					continue;

				citizenAgents[citizen.HomeCell] = new CitizenAgent
				{
					Follower = new CommutePathFollower(path),
					Visual = CreateCitizenCube(),
				};
			}
		}

		// 집에서 도로로 도달 가능한 직장(상업/산업 건물) 찾기 — 첫 reachable.
		private bool TryAssignWork(WorldStage worldStage, CityCellQuery query, Vector3Int home, out Vector3Int work)
		{
			RoadGraph roadGraph = worldStage.RoadGraph;
			if (TryRoadNeighbor(roadGraph, home, out Vector3Int homeRoad) == false)
			{
				work = default;
				return false;
			}

			foreach (Vector3Int candidate in JobCells(query))
			{
				if (TryRoadNeighbor(roadGraph, candidate, out Vector3Int workRoad) == false)
					continue;
				if (roadGraph.FindPath(homeRoad, workRoad).Count > 0)
				{
					work = candidate;
					return true;
				}
			}

			work = default;
			return false;
		}

		private static IEnumerable<Vector3Int> JobCells(CityCellQuery query)
		{
			foreach (Vector3Int cell in query.BuiltCells(ZoneType.Commercial))
				yield return cell;
			foreach (Vector3Int cell in query.BuiltCells(ZoneType.Industrial))
				yield return cell;
		}

		// 집→(집인접도로)→…도로…→(직장인접도로)→직장 셀 시퀀스. 도달 불가면 false.
		private bool TryBuildCommutePath(WorldStage worldStage, Vector3Int home, Vector3Int work, out List<Vector3Int> path)
		{
			RoadGraph roadGraph = worldStage.RoadGraph;
			path = null;

			if (TryRoadNeighbor(roadGraph, home, out Vector3Int homeRoad) == false)
				return false;
			if (TryRoadNeighbor(roadGraph, work, out Vector3Int workRoad) == false)
				return false;

			List<Vector3Int> roadPath = roadGraph.FindPath(homeRoad, workRoad);
			if (roadPath.Count == 0)
				return false;

			path = new List<Vector3Int> { home };
			path.AddRange(roadPath);
			path.Add(work);
			return true;
		}

		// 셀의 4-이웃 중 첫 도로 셀 (존/직장 셀 → 인접 도로 진입점).
		private static bool TryRoadNeighbor(RoadGraph roadGraph, Vector3Int cell, out Vector3Int road)
		{
			for (int i = 0; i < FOUR_NEIGHBORS.Length; i++)
			{
				Vector3Int neighbor = cell + FOUR_NEIGHBORS[i];
				if (roadGraph.HasRoad(neighbor))
				{
					road = neighbor;
					return true;
				}
			}

			road = default;
			return false;
		}

		// 시민 placeholder 큐브 (실 prefab/스킨 deferred).
		private GameObject CreateCitizenCube()
		{
			GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
			cube.transform.SetParent(visualRoot, false);
			cube.name = "Citizen";
			cube.layer = 2; // Ignore Raycast

			Collider cubeCollider = cube.GetComponent<Collider>();
			if (cubeCollider != null)
				Destroy(cubeCollider);

			cube.transform.localScale = Vector3.one * citizenSize;
			cube.GetComponent<Renderer>().sharedMaterial = GetMaterial(citizenColor);
			return cube;
		}

		// 성장 결정 적용 — GridData(진실) mutate + 시각 큐브 projection(캐시). 시각은 데이터의 투영일 뿐.
		private void ApplyGrowth(WorldStage worldStage, CityGrowthDecision decision)
		{
			foreach (GrowthChange change in decision.Grow)
			{
				worldStage.GridData.AddBuildingAt(change.Cell, new BuildingInstanceData(0));
				SetBuildingVisual(change.Cell, ZoneColor(change.ZoneType));
			}

			foreach (GrowthChange change in decision.Shrink)
			{
				worldStage.GridData.RemoveBuildingAt(change.Cell);
				ClearBuildingVisual(change.Cell);
			}
		}

		private Color ZoneColor(ZoneType type)
		{
			switch (type)
			{
				case ZoneType.Residential: return residentialColor;
				case ZoneType.Commercial: return commercialColor;
				case ZoneType.Industrial: return industrialColor;
				default: return Color.gray;
			}
		}

		// 존/도로 타일 (납작한 판).
		private void SetCellVisual(Vector3Int cell, Color color)
		{
			if (cellVisuals.TryGetValue(cell, out GameObject visual) == false)
			{
				visual = CreateCellCube(cell, cellTileHeight);
				cellVisuals[cell] = visual;
			}

			visual.GetComponent<Renderer>().sharedMaterial = GetMaterial(color);
		}

		// 자동 성장 건물 (높은 큐브) — 존 타일 위에.
		private void SetBuildingVisual(Vector3Int cell, Color color)
		{
			if (buildingVisuals.TryGetValue(cell, out GameObject visual) == false)
			{
				visual = CreateCellCube(cell, buildingHeight);
				visual.name = $"Bldg_{cell.x}_{cell.y}";
				buildingVisuals[cell] = visual;
			}

			// 건물은 존 색을 어둡게 (타일과 구분).
			visual.GetComponent<Renderer>().sharedMaterial = GetMaterial(color * 0.6f);
		}

		// 셀 좌표에 큐브 1개 생성 (height = Y 크기·바닥에서 띄움). Grid 회전 상속.
		private GameObject CreateCellCube(Vector3Int cell, float height)
		{
			GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
			cube.transform.SetParent(visualRoot, false);
			cube.name = $"Cell_{cell.x}_{cell.y}";

			cube.layer = 2; // Ignore Raycast — 클릭 평면판정 무방해
			Collider cubeCollider = cube.GetComponent<Collider>();
			if (cubeCollider != null)
				Destroy(cubeCollider);

			Vector3 buildPos = buildManager.GetWorldPosition(cell);
			cube.transform.position = new Vector3(buildPos.x, height * 0.5f, buildPos.z);
			cube.transform.rotation = buildManager.Grid.transform.rotation; // 다이아몬드 칸 정합
			Vector3 cellSize = buildManager.Grid.cellSize;
			cube.transform.localScale = new Vector3(cellSize.x * cellTileScale, height, cellSize.y * cellTileScale);
			return cube;
		}

		private void ClearCellVisual(Vector3Int cell)
		{
			if (cellVisuals.TryGetValue(cell, out GameObject visual))
			{
				cellVisuals.Remove(cell);
				Destroy(visual);
			}
		}

		private void ClearBuildingVisual(Vector3Int cell)
		{
			if (buildingVisuals.TryGetValue(cell, out GameObject visual))
			{
				buildingVisuals.Remove(cell);
				Destroy(visual);
			}
		}

		private Material GetMaterial(Color color)
		{
			if (materialCache.TryGetValue(color, out Material material))
				return material;

			Material created = new(templateMaterial);
			created.color = color;
			if (created.HasProperty("_BaseColor"))
				created.SetColor("_BaseColor", color);

			materialCache[color] = created;
			return created;
		}

#if UNITY_EDITOR
		[ContextMenu("Enter Zone Mode (Residential)")]
		private void EnterZoneResidential_Editor()
		{
			SelectedZoneType = ZoneType.Residential;
			gameModeManager.SetMode(GameMode.Zone);
		}

		[ContextMenu("Enter Zone Mode (Commercial)")]
		private void EnterZoneCommercial_Editor()
		{
			SelectedZoneType = ZoneType.Commercial;
			gameModeManager.SetMode(GameMode.Zone);
		}

		[ContextMenu("Enter Zone Mode (Industrial)")]
		private void EnterZoneIndustrial_Editor()
		{
			SelectedZoneType = ZoneType.Industrial;
			gameModeManager.SetMode(GameMode.Zone);
		}

		[ContextMenu("Enter Road Mode")]
		private void EnterRoadMode_Editor() => gameModeManager.SetMode(GameMode.Road);

		[ContextMenu("Exit to Default")]
		private void ExitMode_Editor() => gameModeManager.SetMode(GameMode.Default);

		// 시간 안 기다리고 즉시 하루 성장 테스트 (수동 트리거 — 자동화는 수동 짝 룰).
		[ContextMenu("DEBUG: Force One Day Growth")]
		private void ForceGrowth_Editor() => OnDayChanged(0);
#endif
	}
}
