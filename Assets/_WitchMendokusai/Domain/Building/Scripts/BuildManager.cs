using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WitchMendokusai
{
	// TODO: 이미 있는 곳에 배치하려고 하는 경우 Text 알림
	public class BuildManager : Singleton<BuildManager>
	{
		private const string MarkerEnabled = "ENABLED";
		private const string MarkerResetTrigger = "RESET";

		private InputManager InputManager => InputManager.Instance;

		[SerializeField] private Grid grid;
		[SerializeField] private Transform gridParent;
		[SerializeField] private GameObject gridVisualization;
		[SerializeField] private Animator marker;
		[SerializeField] private Building defaultBuilding;
		[field: SerializeField] public GameObject BuildingObjectPrefab { get; private set; } = null;
		public Dictionary<Vector3Int, BuildingObject> BuildingObjectsByPos { get; } = new();

		private Building selectedBuilding = null;
		private Vector3Int gridPosition = Vector3Int.zero;
		private float lastClickTime = 0f;
		private const float clickCooldown = 0.1f; // 클릭 간 최소 시간 간격 (초)

		protected override void Awake()
		{
			base.Awake();
			Init();
		}

		private void Init()
		{
			selectedBuilding = defaultBuilding;
			StageManager.OnStageChanged += OnStageChanged;
		}

		private void Start()
		{
			GameModeManager.Instance.OnModeChanged += OnGameModeChanged;
			ApplyMode(GameModeManager.Instance.CurrentMode);
		}

		private void OnDestroy()
		{
			if (GameModeManager.TryGetExistingInstance(out GameModeManager gameModeManager))
				gameModeManager.OnModeChanged -= OnGameModeChanged;
		}

		private void OnGameModeChanged(GameMode mode) => ApplyMode(mode);

		private void ApplyMode(GameMode mode)
		{
			bool isBuildMode = mode == GameMode.Build;

			if (isBuildMode)
			{
				InputManager.RegisterInputEvent(InputEventType.Click0, InputEventResponseType.Get, ClickCell);
				InputManager.RegisterInputEvent(InputEventType.Click1, InputEventResponseType.Get, TryRemoveCell);
			}
			else
			{
				InputManager.UnregisterInputEvent(InputEventType.Click0, InputEventResponseType.Get, ClickCell);
				InputManager.UnregisterInputEvent(InputEventType.Click1, InputEventResponseType.Get, TryRemoveCell);
				CameraManager.Instance.SetContentCameraMode(ContentCameraMode.Normal);
			}

			gridVisualization.SetActive(isBuildMode);
			marker.SetBool(MarkerEnabled, isBuildMode);
		}

		private void Update()
		{
			if (GameModeManager.Instance.IsBuildMode == false)
				return;

			UpdateCellPos();

			Vector3 worldPos = GetWorldPosition(gridPosition);
			if (marker.transform.position != worldPos)
			{
				if (marker.GetBool(MarkerEnabled) == true)
				{
					marker.transform.position = worldPos;
					marker.SetTrigger(MarkerResetTrigger);
				}
			}
		}

		public Vector3 GetWorldPosition(Vector3Int gridPosition)
		{
			Vector3 worldPos = grid.GetCellCenterWorld(gridPosition);
			worldPos.y = 0.01f;
			return worldPos;
		}

		public Vector3 GetWorldPosition(Vector3Int gridPosition, Vector2Int size)
		{
			Vector3 pivotPos = grid.GetCellCenterWorld(gridPosition);
			Vector3 endPos = grid.GetCellCenterWorld(gridPosition + new Vector3Int(-size.x + 1, size.y - 1, 0));
			Vector3 worldPos = Vector3.Lerp(pivotPos, endPos, 0.5f);
			worldPos.y = 0.01f;
			return worldPos;
		}

		private void UpdateCellPos()
		{
			Vector3 mousePosition = InputManager.MouseWorldPosition;
			gridPosition = grid.WorldToCell(mousePosition);
		}

		private void ClickCell()
		{
			if (InputManager.IsPointerOverUI())
				return;

			if (StageManager.Instance.CurStage is WorldStage worldStage == false)
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

			if (Time.time - lastClickTime < clickCooldown)
			{
				// Debug.LogWarning("Clicking too fast!");
				return;
			}
			lastClickTime = Time.time;

			worldStage.GridData.AddBuildingAt(gridPosition, selectedBuilding);
			SpawnBuildingObject(gridPosition, worldStage.GridData.BuildingData[gridPosition]);

			// buildingState.OnAction(gridPosition);
		}

		private void TryRemoveCell()
		{
			if (InputManager.IsPointerOverUI())
				return;

			if (StageManager.Instance.CurStage is WorldStage worldStage == false)
				return;

			if (BuildingObjectsByPos.TryGetValue(gridPosition, out BuildingObject buildingObject) == false)
				return;

			if (Time.time - lastClickTime < clickCooldown)
			{
				// Debug.LogWarning("Clicking too fast!");
				return;
			}
			lastClickTime = Time.time;

			Vector3Int pivot = buildingObject.Pivot;
			worldStage.GridData.RemoveBuildingAt(gridPosition);
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

			BuildingObject buildingObject = ObjectPoolManager.Instance.Spawn(BuildingObjectPrefab).GetComponent<BuildingObject>();
			buildingObject.transform.position = GetWorldPosition(pivot, building.Size);
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
			ObjectPoolManager.Instance.Despawn(buildingObject.gameObject);
		}

		public List<Vector3Int> GetBuildingCoords(Vector3Int pivot, Vector2 size)
		{
			List<Vector3Int> coords = new();

			for (int x = 0; x < size.x; x++)
			{
				for (int y = 0; y < size.y; y++)
				{
					Vector3Int coord = pivot + new Vector3Int(-x, y, 0);
					// Debug.Log($"{nameof(GetBuildingCoords)} {coord} ({-x}, {y})");
					coords.Add(coord);
				}
			}

			return coords;
		}
	}
}