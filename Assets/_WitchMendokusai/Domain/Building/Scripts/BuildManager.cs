using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace WitchMendokusai
{
	// TODO: 이미 있는 곳에 배치하려고 하는 경우 Text 알림
	public class BuildManager : MonoBehaviour
	{
		public static BuildManager Instance { get; private set; }

		public static bool TryGetExistingInstance(out BuildManager mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		private const string MARKER_ENABLED = "ENABLED";
		private const string MARKER_RESET_TRIGGER = "RESET";

		private InputManager inputManager;
		private GameModeManager gameModeManager;
		private CameraManager cameraManager;
		private StageManager stageManager;
		private ObjectPoolManager objectPoolManager;

		[Inject]
		public void Construct(InputManager inputManager, GameModeManager gameModeManager, CameraManager cameraManager, StageManager stageManager, ObjectPoolManager objectPoolManager)
		{
			this.inputManager = inputManager;
			this.gameModeManager = gameModeManager;
			this.cameraManager = cameraManager;
			this.stageManager = stageManager;
			this.objectPoolManager = objectPoolManager;
		}

		[SerializeField] private Grid grid;
		// TASK-WM-164 — City 페인트가 동일 Grid 좌표계 재사용(런타임 stage prefab 내 Grid). 읽기 전용 노출.
		public Grid Grid => grid;
		[SerializeField] private Transform gridParent;
		[SerializeField] private GameObject gridVisualization;
		[SerializeField] private Animator marker;
		[SerializeField] private Building defaultBuilding;
		[field: SerializeField] public GameObject BuildingObjectPrefab { get; private set; } = null;
		public Dictionary<Vector3Int, BuildingObject> BuildingObjectsByPos { get; } = new();

		private Building selectedBuilding = null;
		// TASK-WM-181 INC-2 — gridPosition = 배치 셀(맞은 면 바깥 인접, 마크식) / removeGridPosition = 제거 셀(맞은 셀).
		private Vector3Int gridPosition = Vector3Int.zero;
		private Vector3Int removeGridPosition = Vector3Int.zero;
		private float lastClickTime = 0f;
		private const float CLICK_COOLDOWN = 0.1f; // 클릭 간 최소 시간 간격 (초)

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
			Init();
		}

		private void Init()
		{
			selectedBuilding = defaultBuilding;
			StageManager.OnStageChanged += OnStageChanged;
		}

		private void Start()
		{
			gameModeManager.OnModeChanged += OnGameModeChanged;
			ApplyMode(gameModeManager.CurrentMode);
		}

		private void OnDestroy()
		{
			StageManager.OnStageChanged -= OnStageChanged;
			if (gameModeManager != null)
				gameModeManager.OnModeChanged -= OnGameModeChanged;
			if (Instance == this)
				Instance = null;
		}

		private void OnGameModeChanged(GameMode mode) => ApplyMode(mode);

		private void ApplyMode(GameMode mode)
		{
			bool isBuildMode = mode == GameMode.Build;

			if (isBuildMode)
			{
				inputManager.RegisterInputEvent(InputEventType.Click0, InputEventResponseType.Get, ClickCell);
				inputManager.RegisterInputEvent(InputEventType.Click1, InputEventResponseType.Get, TryRemoveCell);
			}
			else
			{
				inputManager.UnregisterInputEvent(InputEventType.Click0, InputEventResponseType.Get, ClickCell);
				inputManager.UnregisterInputEvent(InputEventType.Click1, InputEventResponseType.Get, TryRemoveCell);
				cameraManager.SetContentCameraMode(ContentCameraMode.Normal);
			}

			gridVisualization.SetActive(isBuildMode);
			marker.SetBool(MARKER_ENABLED, isBuildMode);
		}

		private void Update()
		{
			if (gameModeManager.IsBuildMode == false)
				return;

			UpdateCellPos();

			Vector3 worldPos = BuildCellToWorld(gridPosition);
			if (marker.transform.position != worldPos)
			{
				if (marker.GetBool(MARKER_ENABLED) == true)
				{
					marker.transform.position = worldPos;
					marker.SetTrigger(MARKER_RESET_TRIGGER);
				}
			}
		}

		public Vector3 GetWorldPosition(Vector3Int gridPosition)
		{
			Vector3 worldPos = grid.GetCellCenterWorld(gridPosition);
			// TASK-WM-181 INC-2 — 셀 z(=world Y 레벨, swizzle XZY)가 0이면 평지/도시 → 지면 샘플(GroundProbe).
			// z≠0 = 마크식 면-인접으로 높이 박힌 3D 셀 → 셀 자체 Y(GetCellCenterWorld) 그대로 (블록 위/옆 정합).
			if (gridPosition.z == 0)
				worldPos.y = GroundProbe.SampleSurfaceY(worldPos.x, worldPos.z, worldPos.y);
			return worldPos;
		}

		public Vector3 GetWorldPosition(Vector3Int gridPosition, Vector2Int size)
		{
			Vector3 pivotPos = grid.GetCellCenterWorld(gridPosition);
			Vector3 endPos = grid.GetCellCenterWorld(gridPosition + new Vector3Int(-size.x + 1, size.y - 1, 0));
			Vector3 worldPos = Vector3.Lerp(pivotPos, endPos, 0.5f);
			// TASK-WM-181 INC-2 — z=0(평지/도시) = 지면 샘플 / z≠0(면-인접 3D) = 셀 자체 Y.
			if (gridPosition.z == 0)
				worldPos.y = GroundProbe.SampleSurfaceY(worldPos.x, worldPos.z, worldPos.y);
			return worldPos;
		}

		// TASK-WM-181 INC-2 — 빌더 셀(월드 정수, 복셀 동형) → 월드 위치. 복셀 블록과 동일 lattice:
		// 셀 [n,n+1) 의 XZ 중심 + 바닥 Y(cell.y). GroundProbe 불요(cell.y 가 hit 에서 이미 높이 박음).
		private Vector3 BuildCellToWorld(Vector3Int cell)
		{
			return new Vector3(cell.x + 0.5f, cell.y, cell.z + 0.5f);
		}

		// 다중 셀 건물 footprint 중심 (GetBuildingCoords 동형 — pivot 기준 -X, +Z 확장).
		private Vector3 BuildCellToWorld(Vector3Int pivot, Vector2 size)
		{
			float centerX = pivot.x + 0.5f - (size.x - 1) * 0.5f;
			float centerZ = pivot.z + 0.5f + (size.y - 1) * 0.5f;
			return new Vector3(centerX, pivot.y, centerZ);
		}

		private void UpdateCellPos()
		{
			// TASK-WM-181 INC-2 — 마크식 면-인접: 배치 = 맞은 면 *바깥* 인접 셀(hit+normal), 제거 = 맞은 셀(hit-normal).
			// 그리드 swizzle=XZY 라 셀이 3D (cell.z=world Y 레벨) → 블록 위/옆 어디든 자연 정합 (VoxelInteraction 동형).
			Vector3 hitPoint = inputManager.MouseWorldPosition;
			Vector3 hitNormal = inputManager.MouseWorldNormal;
			// 복셀 VoxelInteraction 과 동일 월드-정수 판정 (그리드 lattice/swizzle/스테이지오프셋 경유 X →
			// 같은 클릭에 복셀 블록과 빌더 건물이 같은 셀에 떨어짐 = 진짜 통합).
			gridPosition = Vector3Int.FloorToInt(hitPoint + hitNormal * 0.5f);
			removeGridPosition = Vector3Int.FloorToInt(hitPoint - hitNormal * 0.5f);
		}

		private void ClickCell()
		{
			if (inputManager.IsPointerOverUI())
				return;

			if (stageManager.CurStage is WorldStage worldStage == false)
				return;

			List<Vector3Int> coords = GetBuildingCoords(gridPosition, selectedBuilding.Size);
			foreach (Vector3Int coord in coords)
			{
				if (BuildingObjectsByPos.ContainsKey(coord))
				{
					// Debug.LogWarning("Already has object at " + coord);
					return;
				}
			}

			if (Time.time - lastClickTime < CLICK_COOLDOWN)
			{
				// Debug.LogWarning("Clicking too fast!");
				return;
			}
			lastClickTime = Time.time;

			worldStage.GridData.AddBuildingAt(gridPosition, new BuildingInstanceData(selectedBuilding.ID));
			SpawnBuildingObject(gridPosition, worldStage.GridData.BuildingData[gridPosition]);

			// buildingState.OnAction(gridPosition);
		}

		private void TryRemoveCell()
		{
			if (inputManager.IsPointerOverUI())
				return;

			if (stageManager.CurStage is WorldStage worldStage == false)
				return;

			if (BuildingObjectsByPos.TryGetValue(removeGridPosition, out BuildingObject buildingObject) == false)
				return;

			if (Time.time - lastClickTime < CLICK_COOLDOWN)
			{
				// Debug.LogWarning("Clicking too fast!");
				return;
			}
			lastClickTime = Time.time;

			Vector3Int pivot = buildingObject.Pivot;
			worldStage.GridData.RemoveBuildingAt(removeGridPosition);
			DespawnBuildingObject(pivot);
		}

		public void SelectBuilding(Building building)
		{
			selectedBuilding = building;
		}

		private void OnStageChanged(Stage stage, StageObject stageObject)
		{
			if (stage is WorldStage worldStage)
			{
				Debug.Log($"{nameof(OnStageChanged)} {grid} | {stageObject}");
				gridParent.position = stageObject.transform.position;
				DespawnAllBuildingObject();
				SpawnAllBuildingObject(worldStage);
			}
		}

		// GridData는 따로 처리해야 함 - 2025.03.24 00:32
		private void SpawnAllBuildingObject(WorldStage worldStage)
		{
			GridData gridData = worldStage.GridData;

			foreach ((Vector3Int coord, BuildingInstanceData runtimeBuildingData) in gridData.BuildingData)
			{
				SpawnBuildingObject(coord, runtimeBuildingData);
			}
		}

		// GridData는 따로 처리해야 함 - 2025.03.24 00:32
		private void SpawnBuildingObject(Vector3Int pivot, BuildingInstanceData data)
		{
			Building building = SOHelper.Get<Building>(data.BuildingID);

			BuildingObject buildingObject = objectPoolManager.Spawn(BuildingObjectPrefab).GetComponent<BuildingObject>();
			buildingObject.transform.position = BuildCellToWorld(pivot, building.Size);
			buildingObject.gameObject.SetActive(true);

			buildingObject.Initialize(data, pivot);

			GetBuildingCoords(pivot, building.Size).ForEach(c =>
			{
				BuildingObjectsByPos.Add(c, buildingObject);
			});

			BuildingObjectsByPos[pivot] = buildingObject;
		}

		// GridData는 따로 처리해야 함 - 2025.03.24 00:32
		private void DespawnAllBuildingObject()
		{
			List<Vector3Int> keys = new(BuildingObjectsByPos.Keys);
			// 무엇이 pivot인지는 모르지만 일단 고
			foreach (Vector3Int coord in keys)
				DespawnBuildingObject(coord);
		}

		// GridData는 따로 처리해야 함 - 2025.03.24 00:32
		private void DespawnBuildingObject(Vector3Int pivot)
		{
			// 잘못된 좌표이거나, 아래에서 이미 제거된 경우
			if (BuildingObjectsByPos.TryGetValue(pivot, out BuildingObject buildingObject) == false)
			{
				Debug.LogWarning("BuildingObject not found at " + pivot);
				return;
			}

			// Size가 1이 아닌 Building들의 경우, 다른 좌표에도 같은 BuildingObject이 있을 수 있으므로 찾아서 제거
			GetBuildingCoords(pivot, buildingObject.Building.Size).ForEach(c =>
			{
				BuildingObjectsByPos.Remove(c);
			});

			// Debug.Log($"{nameof(DespawnBuildingObject)} ({pivot}, {buildingObject.name})");
			buildingObject.Despawn();
			objectPoolManager.Despawn(buildingObject.gameObject);
		}

		public List<Vector3Int> GetBuildingCoords(Vector3Int pivot, Vector2 size)
		{
			List<Vector3Int> coords = new();

			for (int x = 0; x < size.x; x++)
			{
				for (int z = 0; z < size.y; z++)
				{
					// TASK-WM-181 INC-2 — 월드-정수 셀이라 footprint 도 월드 XZ (-X, +Z). 옛 그리드 XY(-x, y, 0) 폐기.
					Vector3Int coord = pivot + new Vector3Int(-x, 0, z);
					coords.Add(coord);
				}
			}

			return coords;
		}
	}
}