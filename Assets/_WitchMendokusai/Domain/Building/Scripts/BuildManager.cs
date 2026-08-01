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

		/// <summary>
		/// 월드 건설의 설치 입력 정책 = 연속(드래그로 벽·바닥 죽 긋기). 아래 Click0/Click1 등록이
		/// <see cref="InputEventResponseType.Get"/>(매 프레임 폴)인 이유가 이것 — 의도된 동작이다.
		/// 비용이 붙는 배치(개척 포탑·채집 인형)는 <see cref="PlacementInputMode.SingleClick"/> 을 쓴다
		/// (InputStrategyTowerDefense). TASK-WM-194 — 두 모드를 한 어휘로 분리.
		/// </summary>
		public const PlacementInputMode PLACEMENT_MODE = PlacementInputMode.Continuous;

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
		private float lastClickTime = 0f;
		private const float CLICK_COOLDOWN = 0.1f; // 클릭 간 최소 시간 간격 (초)
		private const float BUILD_REACH_DISTANCE = 100f; // 빌더 레이캐스트 도달 거리
		private ChunkManager chunkManager; // TASK-WM-181 — 빌드모드 지형(복셀) 부수기용. lazy resolve (스테이지 스코프).

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
				// TASK-WM-181 — 마크/복셀(VoxelInteraction) 동형: 좌클릭(Click0)=제거, 우클릭(Click1)=배치.
				inputManager.RegisterInputEvent(InputEventType.Click0, InputEventResponseType.Get, TryRemoveCell);
				inputManager.RegisterInputEvent(InputEventType.Click1, InputEventResponseType.Get, ClickCell);
			}
			else
			{
				inputManager.UnregisterInputEvent(InputEventType.Click0, InputEventResponseType.Get, TryRemoveCell);
				inputManager.UnregisterInputEvent(InputEventType.Click1, InputEventResponseType.Get, ClickCell);
				// content 카메라 복귀는 여기서 하지 않는다 — 이 분기는 *모든* 모드 변경에서 돌기 때문에
				// 다른 모드(개척/투기장)가 방금 잡은 카메라를 구독 순서로 덮어썼다(실측: 개척 화면이 안 바뀜).
				// 게임 모드 → content 카메라는 CameraManager 단일 권위자(GameModeCamera 매핑)가 정한다.
			}

			gridVisualization.SetActive(isBuildMode);
			marker.SetBool(MARKER_ENABLED, isBuildMode);
		}

		private void Update()
		{
			if (gameModeManager.IsBuildMode == false)
				return;
			if (TryBuildRaycast(out RaycastHit hit) == false)
				return;

			// 미리보기 마커 = 맞은 면 바깥 인접 셀 (복셀 동형 — 건물 위/옆/복셀 옆 어디든).
			Vector3Int placeCell = Vector3Int.FloorToInt(hit.point + hit.normal * 0.5f);
			Vector3 worldPos = BuildCellToWorld(placeCell);
			if (marker.GetBool(MARKER_ENABLED) == true && marker.transform.position != worldPos)
			{
				marker.transform.position = worldPos;
				marker.SetTrigger(MARKER_RESET_TRIGGER);
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

		// TASK-WM-181 INC-2 — 빌더 자체 레이캐스트: 복셀 블록·건물·지면 전 레이어 다 맞춤(InputManager 레이어마스크
		// 우회). 이게 있어야 건물 위 적층·건물 제거·복셀 인접 배치가 다 동작. VoxelInteraction 과 동일 모델.
		private bool TryBuildRaycast(out RaycastHit hit)
		{
			hit = default;
			Camera camera = Camera.main;
			if (camera == null)
				return false;

			Ray ray = camera.ScreenPointToRay(inputManager.MouseScreenPosition);
			return Physics.Raycast(ray, out hit, BUILD_REACH_DISTANCE, ~0, QueryTriggerInteraction.Ignore);
		}

		private void ClickCell()
		{
			if (inputManager.IsPointerOverUI())
				return;
			if (stageManager.CurStage is WorldStage worldStage == false)
				return;
			if (Time.time - lastClickTime < CLICK_COOLDOWN)
				return;
			if (TryBuildRaycast(out RaycastHit hit) == false)
				return;

			// 배치 셀 = 맞은 면 바깥 인접 (복셀 동형). 건물 윗면 클릭=위 적층, 옆면=옆, 복셀 옆=복셀 인접.
			Vector3Int placeCell = Vector3Int.FloorToInt(hit.point + hit.normal * 0.5f);
			List<Vector3Int> coords = GetBuildingCoords(placeCell, selectedBuilding.Size);
			foreach (Vector3Int coord in coords)
				if (BuildingObjectsByPos.ContainsKey(coord))
					return; // 이미 건물 점유

			lastClickTime = Time.time;
			worldStage.GridData.AddBuildingAt(placeCell, new BuildingInstanceData(selectedBuilding.ID));
			SpawnBuildingObject(placeCell, worldStage.GridData.BuildingData[placeCell]);
		}

		// 빌드모드 좌클릭 = 부수기 (월드 편집 통일). 가리킨 게 건물이면 건물 제거, 지형(복셀)이면 복셀 블록 부수기.
		// 일반 모드선 VoxelInteraction 이 복셀 편집 안 함(씨앗만) → 실수 지형 편집 차단. 통일 단일 핸들러.
		private void TryRemoveCell()
		{
			if (inputManager.IsPointerOverUI())
				return;
			if (stageManager.CurStage is WorldStage worldStage == false)
				return;
			if (Time.time - lastClickTime < CLICK_COOLDOWN)
				return;
			if (TryBuildRaycast(out RaycastHit hit) == false)
				return;

			lastClickTime = Time.time;

			// 맞은 콜라이더가 건물이면 그 건물 직접 제거 (셀 조회 X — 높이/멀티셀 무관 견고).
			BuildingObject buildingObject = hit.collider.GetComponentInParent<BuildingObject>();
			if (buildingObject != null)
			{
				worldStage.GridData.RemoveBuildingAt(buildingObject.Pivot);
				DespawnBuildingObject(buildingObject.Pivot);
				return;
			}

			// 건물 아님 = 지형 복셀 → 부수기 (VoxelInteraction 옛 break 수식 동일: hit-normal*0.1, +CHUNK_SIZE_Y/2).
			ChunkManager chunks = EnsureChunkManager();
			if (chunks == null)
				return;
			Vector3 targetPos = hit.point - hit.normal * 0.1f;
			int voxelX = Mathf.FloorToInt(targetPos.x);
			int voxelY = Mathf.FloorToInt(targetPos.y + VoxelConstants.CHUNK_SIZE_Y / 2f);
			int voxelZ = Mathf.FloorToInt(targetPos.z);
			chunks.SetBlock(voxelX, voxelY, voxelZ, VoxelConstants.AIR_RUNTIME_ID);
		}

		// 지형 부수기용 ChunkManager — 스테이지 스코프라 사용 시점 lazy resolve. init-order-ok
		private ChunkManager EnsureChunkManager()
		{
			if (chunkManager == null)
				chunkManager = FindAnyObjectByType<ChunkManager>();
			return chunkManager;
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
			// TASK-WM-181 INC-2 — voxel-native 축정렬: 건물은 항상 정방향(복셀 블록과 동형). 풀 재사용 stale 회전 방어.
			buildingObject.transform.rotation = Quaternion.identity;
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