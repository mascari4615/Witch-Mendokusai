using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
// ★ 이 파일의 칸 좌표는 「판정 쪽」이다 (TASK-WM-214) — 도로망·전력·지구 수요는 전부 시뮬 타입이고,
//   타일맵에 칠할 때는 자동으로 엔진 좌표가 된다. 반대로 엔진에서 칸을 받아올 때만 캐스트한다.
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
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
	public partial class CityPaintManager : MonoBehaviour
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

		[Header("INC-5 전력 (발전소 placeholder — 실 prefab/스킨=마법진? deferred)")]
		[Tooltip("발전소 1기가 도로 따라 전력 보내는 홉 거리.")]
		[SerializeField] private int powerSourceRange = 6;
		[SerializeField] private Color powerSourceColor = new(1.00f, 0.95f, 0.30f);

		[Header("INC-6 진단 히트맵 (데이터뷰 오버레이 — 색 팔레트=HeatmapGradient 기본)")]
		[Tooltip("오버레이 타일 두께 — 존 타일보다 약간 위에 떠 데이터뷰가 가시.")]
		[SerializeField] private float overlayTileHeight = 0.14f;
		[Tooltip("욕망도 히트맵 — 전역 존 수요 기여 가중.")]
		[SerializeField] private float desirabilityDemandWeight = 1.0f;
		[Tooltip("욕망도 히트맵 — 도로 접근 기여 가중.")]
		[SerializeField] private float desirabilityRoadWeight = 1.0f;
		[Tooltip("욕망도 히트맵 — 전력 공급 기여 가중.")]
		[SerializeField] private float desirabilityPowerWeight = 1.0f;

		[Header("INC-8 game-feel — 건물 성장/쇠퇴 연출 (DOTween, WM-180)")]
		[Tooltip("건물 자동 성장 시 바닥→full 솟아오르는 시간(초).")]
		[SerializeField] private float buildingRiseDuration = 0.35f;
		[Tooltip("건물 쇠퇴 시 가라앉으며 사라지는 시간(초).")]
		[SerializeField] private float buildingSinkDuration = 0.25f;
		[Tooltip("성장 연출 ease — OutBack = 살짝 튀어오르는 팝.")]
		[SerializeField] private Ease buildingRiseEase = Ease.OutBack;
		[Tooltip("쇠퇴 연출 ease — InBack = 빨려들어가듯 가라앉음.")]
		[SerializeField] private Ease buildingSinkEase = Ease.InBack;

		private InputManager inputManager;
		private GameModeManager gameModeManager;
		private StageManager stageManager;
		// BuildManager 의 런타임 Grid 재사용 — 도시 페인트가 건물 배치와 동일 셀 좌표계. 사용자 Grid 연결 불요.
		private BuildManager buildManager;
		private WorldClock worldClock;

		[Inject]
		public void Construct(InputManager inputManager, GameModeManager gameModeManager, StageManager stageManager, BuildManager buildManager, WorldClock worldClock)
		{
			this.inputManager = inputManager;
			this.gameModeManager = gameModeManager;
			this.stageManager = stageManager;
			this.buildManager = buildManager;
			this.worldClock = worldClock;
		}
		private readonly RciDemandModel demandModel = new();
		private readonly CityGrowthSystem growthSystem = new();
		private readonly CitySimulationSystem simulationSystem = new();
		private readonly PowerGrid powerGrid = new();

		public ZoneType SelectedZoneType { get; set; } = ZoneType.Residential;

		private void Awake()
		{
			visualRoot = new GameObject("CityPaintVisuals").transform;
			visualRoot.SetParent(transform, false);

			// TASK-WM-208 — 예전엔 큐브를 하나 만들어 그 **기본 재질**을 견본으로 훔쳐 왔다. 그 기본 재질은
			// 옛 렌더러용이라 에디터에선 그럭저럭 보이지만 **빌드에서 도시 전체가 밋밋한 회색**이 된다
			// (모든 칸 재질이 이 견본의 복제라 한 곳에서 판 전체로 번졌다). 이제 프로젝트 셰이더에서 받는다.
			templateMaterial = CombatPrimitive.CreateMaterial();
		}

		private void Start()
		{
			gameModeManager.OnModeChanged += OnModeChanged;
			ApplyMode(gameModeManager.CurrentMode);

			// step6, 매일 수요 평가 + 자동 성장. WorldClock 은 Root 스코프에서 주입 (2026-09-05, .Instance 제거)
			worldClock.OnDayChanged += OnDayChanged;
		}

		private void OnDestroy()
		{
			if (gameModeManager != null)
				gameModeManager.OnModeChanged -= OnModeChanged;
			if (worldClock != null)
				worldClock.OnDayChanged -= OnDayChanged;

			// INC-8 — 진행 중 건물 성장/쇠퇴 연출 트윈 정리 (파괴된 transform 트윈 경고 방지).
			foreach (GameObject visual in buildingVisuals.Values)
				if (visual != null)
					visual.transform.DOKill();
		}

		private void OnModeChanged(GameMode mode) => ApplyMode(mode);

		private void ApplyMode(GameMode mode)
		{
			inputManager.UnregisterInputEvent(InputEventType.Click0, InputEventResponseType.Get, OnClickPaint);
			inputManager.UnregisterInputEvent(InputEventType.Click1, InputEventResponseType.Get, OnClickErase);

			if (mode == GameMode.Zone || mode == GameMode.Road || mode == GameMode.Power)
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
			// 엔진이 고른 칸을 판정 세계로 들인다 — 여기가 경계다.
			cell = grid.WorldToCell(groundPoint).ToSim();
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
			else if (gameModeManager.CurrentMode == GameMode.Power)
			{
				if (placement.CanPlaceZone(cell) == false)
					return; // 도로 위엔 발전소 X (도로 인접 셀에 두면 전력이 도로로 퍼짐)

				worldStage.PowerSourceRegistry.Add(cell, powerSourceRange);
				SetPowerSourceVisual(cell);
			}
		}

		private void OnClickErase()
		{
			if (TryGetTargetCell(out Vector3Int cell, out WorldStage worldStage) == false)
				return;

			worldStage.ZoneGrid.Clear(cell);
			worldStage.RoadGraph.RemoveRoad(cell);
			worldStage.PowerSourceRegistry.Remove(cell);
			ClearCellVisual(cell);
			ClearBuildingVisual(cell);
			ClearPowerSourceVisual(cell);
			ClearOverlayVisual(cell);
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

			// INC-5(Phase3) — 발전소 있으면 전력 게이트(전력 받는 셀만 성장), 없으면 Phase2 그대로(비파괴).
			HashSet<Vector3Int> energizedRoads = ComputeEnergizedRoads(worldStage);
			CityGrowthDecision decision = worldStage.PowerSourceRegistry.Sources.Count > 0
				? growthSystem.Decide(demand, query, growthThreshold, maxChangePerDayPerZone, powerGrid, energizedRoads)
				: growthSystem.Decide(demand, query, growthThreshold, maxChangePerDayPerZone);
			ApplyGrowth(worldStage, decision);

			// INC-5c — 성장 반영된 도시로 하루치 생산/소비 → CityEconomy 재고 갱신 (query 는 live = post-growth 카운트).
			RunEconomyTick(worldStage, query, day);

			// INC-7 — 주거↔직장 통근 시민 동기화 (registry = 진실, 시각 에이전트 spawn/despawn).
			SyncCitizens(worldStage, query);

			// INC-6 — 데이터뷰 켜져 있으면 변경된 도시로 히트맵 갱신 (no-news 룰: 도시 흐르면 진단도 따라감).
			if (overlayKind != OverlayKind.Off)
				RefreshOverlay();
		}

		// 자원 카탈로그 임시 id (ResourceSO 도입 전 — 스킨 deferred). 0=원자재, 1=재화.
		private const int RAW_RESOURCE = 0;
		private const int GOODS_RESOURCE = 1;

		// Unity 내장 레이어 2 = "Ignore Raycast". 엔진이 정한 번호라 디자인 조절값이 아니지만,
		// 숫자 2 만 박혀 있으면 다음 사람이 무슨 뜻인지 몰라 못 건드린다.
		private const int IGNORE_RAYCAST_LAYER = 2;

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

				Vector3 fromPos = buildManager.GetWorldPosition(fromCell).ToUnity();
				Vector3 toPos = buildManager.GetWorldPosition(toCell).ToUnity();
				Vector3 pos = Vector3.Lerp(fromPos, toPos, t);
				// TASK-WM-181 INC-1 — pos.y = 지면(GetWorldPosition 보간) → 시민도 지형 위로 떠 따라감(평탄 X).
				agent.Visual.transform.position = new Vector3(pos.x, pos.y + citizenHeight, pos.z);
			}
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
				AnimateBuildingSink(change.Cell); // 자동 쇠퇴 = 가라앉아 사라짐 (유저 erase 는 즉시 — ClearBuildingVisual 별도)
			}
		}

		// INC-5 — 발전소들 → 각자 인접도로 진입점 → 그 source range 로 flood → union = 전력 흐르는 도로 셀.
		// 발전소가 도로에 안 닿으면(인접도로 없음) 그 발전소는 전력 0 (dead) — TryRoadNeighbor false.
		private HashSet<Vector3Int> ComputeEnergizedRoads(WorldStage worldStage)
		{
			HashSet<Vector3Int> energized = new();
			RoadGraph roadGraph = worldStage.RoadGraph;

			foreach (KeyValuePair<Vector3Int, PowerSourceData> entry in worldStage.PowerSourceRegistry.Sources)
			{
				if (TryRoadNeighbor(roadGraph, entry.Key, out Vector3Int roadEntry))
				{
					energized.UnionWith(powerGrid.ComputeEnergizedRoads(roadGraph, new[] { roadEntry }, entry.Value.Range));
				}
			}

			return energized;
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

		[ContextMenu("Enter Power Mode (발전소)")]
		private void EnterPowerMode_Editor() => gameModeManager.SetMode(GameMode.Power);

		[ContextMenu("Exit to Default")]
		private void ExitMode_Editor() => gameModeManager.SetMode(GameMode.Default);

		// 시간 안 기다리고 즉시 하루 성장 테스트 (수동 트리거 — 자동화는 수동 짝 룰).
		[ContextMenu("DEBUG: Force One Day Growth")]
		private void ForceGrowth_Editor() => OnDayChanged(0);
#endif
	}
}
