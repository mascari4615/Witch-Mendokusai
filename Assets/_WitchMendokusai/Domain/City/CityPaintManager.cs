using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// SimCity Phase 1 step5 — 존/도로 페인트 (화면 가시화 tracer).
	//
	// GameMode.Zone/Road 진입 시 Click0=페인트 / Click1=해제. 데이터 진실 = WorldStage.ZoneGrid /
	// RoadGraph (substrate step1-4), 이 매니저는 거기에 쓰고 셀마다 색 큐브를 *코드로* spawn 해 보이게
	// 한다(프리팹 0 = tracer; 정식 타일 비주얼은 후속). 좌표계는 BuildManager 의 런타임 Grid 를 재사용 —
	// 건물 배치와 *정확히 같은 셀 좌표계* (자체 Grid 연결 시 prefab/런타임 인스턴스 불일치로 위치 어긋남).
	//
	// 모드 진입 = [ContextMenu] 수동 트리거 (입력 시스템·slot A InputManager enum 무접촉 — 정식 단축키
	// 는 후속 step. 수동 트리거 = 「모든 자동화는 수동 트리거 전제」 정합).
	public class CityPaintManager : MonoBehaviour
	{
		[Tooltip("페인트 셀 큐브 한 변 비율 (1 = 셀 꽉 참).")]
		[SerializeField] private float cellTileScale = 0.9f;
		[Tooltip("타일 두께 (납작한 판).")]
		[SerializeField] private float cellTileHeight = 0.1f;

		[SerializeField] private Color residentialColor = new(0.40f, 0.85f, 0.40f);
		[SerializeField] private Color commercialColor = new(0.40f, 0.60f, 1.00f);
		[SerializeField] private Color industrialColor = new(1.00f, 0.85f, 0.30f);
		[SerializeField] private Color roadColor = new(0.35f, 0.35f, 0.35f);

		private InputManager inputManager;
		private GameModeManager gameModeManager;
		private StageManager stageManager;
		// BuildManager 의 런타임 Grid 재사용 — 도시 페인트가 건물 배치와 *정확히 같은 셀 좌표계* 를 쓰게
		// 보장(자체 Grid SerializeField 연결 시 prefab/런타임 인스턴스 불일치로 위치 어긋남). known-good
		// 재사용 = 좌표 정합 > City→Building 결합 회피. 사용자 Grid 연결 불요(BuildManager 가 이미 보유).
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
		private readonly Dictionary<Color, Material> materialCache = new();
		private Material templateMaterial;
		private Transform visualRoot;

		public ZoneType SelectedZoneType { get; set; } = ZoneType.Residential;

		private void Awake()
		{
			visualRoot = new GameObject("CityPaintVisuals").transform;
			visualRoot.SetParent(transform, false);

			// 머티리얼 템플릿 1회 확보 — Shader.Find 회피(파이프라인 의존 X), primitive 기본 머티리얼을
			// 복제해 색만 바꿔 씀.
			GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
			templateMaterial = probe.GetComponent<Renderer>().sharedMaterial;
			Destroy(probe);
		}

		private void Start()
		{
			gameModeManager.OnModeChanged += OnModeChanged;
			ApplyMode(gameModeManager.CurrentMode);
		}

		private void OnDestroy()
		{
			if (gameModeManager != null)
				gameModeManager.OnModeChanged -= OnModeChanged;
		}

		private void OnModeChanged(GameMode mode) => ApplyMode(mode);

		private void ApplyMode(GameMode mode)
		{
			// 모드 진입/이탈마다 멱등 재배선 (BuildManager.ApplyMode 패턴).
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
			// BuildManager 의 Grid = 건물 배치와 동일 좌표계 (런타임 stage prefab 내 Grid, stage 오프셋 반영).
			cell = buildManager.Grid.WorldToCell(inputManager.MouseWorldPosition);
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

		private void SetCellVisual(Vector3Int cell, Color color)
		{
			if (cellVisuals.TryGetValue(cell, out GameObject visual) == false)
			{
				visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
				visual.name = $"Cell_{cell.x}_{cell.y}";
				visual.transform.SetParent(visualRoot, false);

				// 콜라이더 제거 + Ignore Raycast 레이어(2) — Destroy 는 프레임 끝 지연이라 드래그 중
				// 직전 프레임 큐브를 MouseWorldPosition raycast 가 맞혀 셀이 점점 어긋나는 드리프트를
				// 차단(즉시 레이어 격리가 근본, Destroy 타이밍 비의존).
				visual.layer = 2; // Builtin "Ignore Raycast"
				Collider cubeCollider = visual.GetComponent<Collider>();
				if (cubeCollider != null)
					Destroy(cubeCollider);

				// XZ = BuildManager.GetWorldPosition(cell) = 건물이 놓이는 바로 그 월드 좌표(검증된 known-good,
				// stage 오프셋·셀 중심 포함). Y 만 타일용 납작 높이로 덮어씀. 클릭 셀 ↔ 타일 위치 정합 =
				// BuildManager 와 동일하므로 어긋남 0.
				Vector3 buildPos = buildManager.GetWorldPosition(cell);
				Vector3 worldPos = new(buildPos.x, cellTileHeight * 0.5f, buildPos.z);
				visual.transform.position = worldPos;
				// X/Z 는 셀 크기만큼(인접 타일 seamless), Y 는 얇은 판.
				Vector3 cellSize = buildManager.Grid.cellSize;
				visual.transform.localScale = new Vector3(cellSize.x * cellTileScale, cellTileHeight, cellSize.y * cellTileScale);
				cellVisuals[cell] = visual;
			}

			visual.GetComponent<Renderer>().sharedMaterial = GetMaterial(color);
		}

		private void ClearCellVisual(Vector3Int cell)
		{
			if (cellVisuals.TryGetValue(cell, out GameObject visual))
			{
				cellVisuals.Remove(cell);
				Destroy(visual);
			}
		}

		private Material GetMaterial(Color color)
		{
			if (materialCache.TryGetValue(color, out Material material))
				return material;

			Material created = new(templateMaterial);
			created.color = color;
			// URP Lit 은 _BaseColor 사용 — 빌트인(_Color, .color)·URP 둘 다 set (파이프라인 무관 색 적용).
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
#endif
	}
}
