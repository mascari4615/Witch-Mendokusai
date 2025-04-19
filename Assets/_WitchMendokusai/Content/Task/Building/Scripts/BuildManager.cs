using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

		[SerializeField] private UIBuild buildUI;

		private Building selectedBuilding;
		private Vector3Int gridPosition;
		public bool IsBuilding { get; private set; } = false;

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
			StopBuilding();
		}

		[ContextMenu(nameof(StartBuilding))]
		public void StartBuilding()
		{
			IsBuilding = true;
			UIManager.Instance.SetCanvas(MCanvasType.Build);

			InputManager.RegisterInputEvent(InputEventType.Click0, InputEventResponseType.Started, ClickCell);
			InputManager.RegisterInputEvent(InputEventType.Click1, InputEventResponseType.Get, TryRemoveCell);
			gridVisualization.SetActive(true);
			marker.SetBool(MarkerEnabled, true);
		}

		[ContextMenu(nameof(StopBuilding))]
		public void StopBuilding()
		{
			IsBuilding = false;
			UIManager.Instance.SetCanvas(MCanvasType.None);

			InputManager.UnregisterInputEvent(InputEventType.Click0, InputEventResponseType.Started, ClickCell);
			InputManager.UnregisterInputEvent(InputEventType.Click1, InputEventResponseType.Get, TryRemoveCell);
			gridVisualization.SetActive(false);
			marker.SetBool(MarkerEnabled, false);
		}

		private void Update()
		{
			// TODO: 임시 Build 키
			if (Input.GetKeyDown(KeyCode.B))
			{
				if (IsBuilding == true)
					StopBuilding();
				else
					StartBuilding();
			}

			if (IsBuilding == false)
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

			if (BuildingObjectsByPos.TryGetValue(gridPosition, out BuildingObject buildingObject))
			{
				// 해당 위치에 이미 건물이 있는 경우
				Vector3Int pivot = buildingObject.Pivot;

				worldStage.GridData.RemoveBuildingAt(gridPosition);
				DespawnBuildingObject(pivot);
				return;
			}
			else
			{
				List<Vector3Int> coords = GetBuildingCoords(gridPosition, selectedBuilding.Size);
				foreach (Vector3Int coord in coords)
				{
					if (BuildingObjectsByPos.ContainsKey(coord))
					{
						Debug.LogWarning("Already has object at " + coord);
						return;
					}
				}
				worldStage.GridData.AddBuildingAt(gridPosition, selectedBuilding);
				SpawnBuildingObject(gridPosition, selectedBuilding);
			}

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

			foreach ((Vector3Int coord, RuntimeBuildingData runtimeBuildingData) in gridData.BuildingData)
			{
				Building building = SOHelper.Get<Building>(runtimeBuildingData.BuildingID);
				SpawnBuildingObject(coord, building);
			}
		}

		// GridData는 따로 처리해야 함 - 2025.03.24 00:32
		private void SpawnBuildingObject(Vector3Int pivot, Building building)
		{
			// Debug.Log($"{nameof(SpawnBuildingObject)} ({pivot}, {building.name})");

			BuildingObject buildingObject = ObjectPoolManager.Instance.Spawn(BuildingObjectPrefab).GetComponent<BuildingObject>();
			buildingObject.transform.position = GetWorldPosition(pivot, building.Size);
			buildingObject.gameObject.SetActive(true);

			buildingObject.Initialize(new RuntimeBuildingData(building.ID), pivot);

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