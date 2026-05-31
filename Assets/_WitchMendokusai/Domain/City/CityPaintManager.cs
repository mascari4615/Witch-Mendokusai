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
		// 자동 성장한 건물 시각 큐브 (존 타일과 별개 레이어 — 같은 셀에 타일+건물 공존).
		private readonly Dictionary<Vector3Int, GameObject> buildingVisuals = new();
		private readonly Dictionary<Color, Material> materialCache = new();
		private Material templateMaterial;
		private Transform visualRoot;
		private readonly RciDemandModel demandModel = new();

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

			if (gameModeManager.CurrentMode == GameMode.Zone)
			{
				worldStage.ZoneGrid.Paint(cell, SelectedZoneType);
				SetCellVisual(cell, ZoneColor(SelectedZoneType));
			}
			else if (gameModeManager.CurrentMode == GameMode.Road)
			{
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

		// step6 — 매일 호출. 수요 평가 후 존타입별 성장/쇠퇴.
		// ★ 수요 입력 = "지은 건물 수"(occupancy) — 칠한 존 칸 수(capacity)가 아님. capacity 를 넣으면
		//   주거칸 ≫ 일자리칸인 자연스러운 도시에서 주거 gap 영구 음수 → 주거 영영 미성장(RciDemandModel 주석).
		private void OnDayChanged(int day)
		{
			if (stageManager.CurStage is WorldStage worldStage == false)
				return;

			RciDemandCoefficients coefficients = new(residentsPerJob, shopsPerResident, industryPerResident, immigrationBaseline, exportBaseline, demandGain);
			RciDemand demand = demandModel.Evaluate(
				CountBuiltByType(worldStage, ZoneType.Residential),
				CountBuiltByType(worldStage, ZoneType.Commercial),
				CountBuiltByType(worldStage, ZoneType.Industrial),
				coefficients);

			ApplyDemand(worldStage, ZoneType.Residential, demand.Residential);
			ApplyDemand(worldStage, ZoneType.Commercial, demand.Commercial);
			ApplyDemand(worldStage, ZoneType.Industrial, demand.Industrial);
		}

		// 현재 점유(자동 성장한 건물) 수 — 존타입별. RciDemandModel 의 occupancy 입력원(capacity = CountByType 와 구분).
		// buildingVisuals = 자동 성장 건물의 진실(GridData 미러). 각 셀의 존타입은 ZoneGrid 가 진실.
		private int CountBuiltByType(WorldStage worldStage, ZoneType zoneType)
		{
			ZoneGrid zoneGrid = worldStage.ZoneGrid;
			int count = 0;
			foreach (Vector3Int cell in buildingVisuals.Keys)
			{
				if (zoneGrid.GetZone(cell) == zoneType)
					count++;
			}

			return count;
		}

		private void ApplyDemand(WorldStage worldStage, ZoneType zoneType, float demand)
		{
			if (demand > growthThreshold)
				GrowZone(worldStage, zoneType);
			else if (demand < -growthThreshold)
				ShrinkZone(worldStage, zoneType);
		}

		// 성장 = 해당 존타입의 "빈 존셀(건물 0) + 도로 인접" 셀 중 cap 만큼 건물 세움.
		private void GrowZone(WorldStage worldStage, ZoneType zoneType)
		{
			ZoneGrid zoneGrid = worldStage.ZoneGrid;
			RoadGraph roadGraph = worldStage.RoadGraph;
			int grown = 0;

			foreach (KeyValuePair<Vector3Int, ZoneCellData> entry in zoneGrid.ZoneData)
			{
				if (grown >= maxChangePerDayPerZone)
					break;

				Vector3Int cell = entry.Key;
				if (entry.Value.Type != zoneType)
					continue;
				if (buildingVisuals.ContainsKey(cell))
					continue; // 이미 건물 있음
				if (roadGraph.IsRoadAdjacent(cell) == false)
					continue; // 도로 안 닿음 = 성장 불가 (최소 공간 규칙)

				worldStage.GridData.AddBuildingAt(cell, new BuildingInstanceData(0));
				SetBuildingVisual(cell, ZoneColor(zoneType));
				grown++;
			}
		}

		// 쇠퇴 = 해당 존타입 건물 1개 철거 (cap 만큼).
		private void ShrinkZone(WorldStage worldStage, ZoneType zoneType)
		{
			ZoneGrid zoneGrid = worldStage.ZoneGrid;
			List<Vector3Int> toRemove = new();

			foreach (KeyValuePair<Vector3Int, GameObject> entry in buildingVisuals)
			{
				if (toRemove.Count >= maxChangePerDayPerZone)
					break;
				if (zoneGrid.GetZone(entry.Key) == zoneType)
					toRemove.Add(entry.Key);
			}

			foreach (Vector3Int cell in toRemove)
			{
				worldStage.GridData.RemoveBuildingAt(cell);
				ClearBuildingVisual(cell);
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
