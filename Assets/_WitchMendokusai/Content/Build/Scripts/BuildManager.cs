using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class BuildManager : Singleton<BuildManager>
	{
		private const string MarkerEnabled = "ENABLED";
		private const string MarkerResetTrigger = "RESET";

		private InputManager InputManager => InputManager.Instance;

		[SerializeField] private Grid grid;
		[SerializeField] private GameObject gridVisualization;
		[SerializeField] private Animator marker;
		[SerializeField] private Building defaultBuilding;
		[field: SerializeField] public GameObject BuildingObjectPrefab { get; private set; } = null;
		public Dictionary<Vector3Int, BuildingObject> BuildingObjectDict { get; } = new();

		[SerializeField] private UIBuild buildUI;

		private Building selectedBuilding;
		private Vector3Int gridPosition;
		private bool isBuilding = false;

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
			isBuilding = true;
			UIManager.Instance.SetCanvas(MCanvasType.Build);

			InputManager.RegisterMouseEvent(InputMouseEventType.Button0Down, () => ClickCell());
			gridVisualization.SetActive(true);
			marker.SetBool(MarkerEnabled, true);
		}

		[ContextMenu(nameof(StopBuilding))]
		public void StopBuilding()
		{
			isBuilding = false;
			UIManager.Instance.SetCanvas(MCanvasType.None);

			InputManager.UnregisterMouseEvent(InputMouseEventType.Button0Down);
			gridVisualization.SetActive(false);
			marker.SetBool(MarkerEnabled, false);
		}

		private void Update()
		{
			// TODO: 임시 Build 키
			if (Input.GetKeyDown(KeyCode.B))
			{
				if (isBuilding == true)
					StopBuilding();
				else
					StartBuilding();
			}

			if (isBuilding == false)
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

		private void UpdateCellPos()
		{
			Vector3 mousePosition = InputManager.MouseWorldPosition;
			gridPosition = grid.WorldToCell(mousePosition);
		}

		private void ClickCell()
		{
			if (InputManager.IsPointerOverUI())
				return;

			if (StageManager.Instance.CurStage is WorldStage worldStage)
			{
				GridData gridData = worldStage.GridData;

				if (gridData.HasObjectAt(gridPosition))
				{
					gridData.RemoveObjectAt(gridPosition);
					DespawnBuildingObject(gridPosition);
					return;
				}
				else
				{
					gridData.AddObjectAt(gridPosition, selectedBuilding);
					SpawnBuildingObject(gridPosition, selectedBuilding);
				}
			}

			// buildingState.OnAction(gridPosition);
		}

		public void SelectBuilding(Building building)
		{
			selectedBuilding = building;
		}

		private void OnStageChanged(Stage stage, StageObject stageObject)
		{
			if (stage is WorldStage worldStage)
			{
				// Debug.Log($"{nameof(OnStageChanged)} {grid} | {stageObject}");
				grid.transform.position = stageObject.transform.position;
				DespawnAllBuildingObject();
				SpawnAllBuildingObject(worldStage);
			}
		}

		private void SpawnAllBuildingObject(WorldStage worldStage)
		{
			GridData gridData = worldStage.GridData;

			foreach ((Vector3Int coord, RuntimeBuildingData runtimeBuildingData) in gridData.BuildingData)
			{
				Building building = SOHelper.Get<Building>(runtimeBuildingData.SOID);
				SpawnBuildingObject(coord, building);
			}
		}

		private void SpawnBuildingObject(Vector3Int coord, Building building)
		{
			// Debug.Log($"{nameof(SpawnBuildingObject)} ({coord}, {building.name})");

			BuildingObject buildingObject = ObjectPoolManager.Instance.Spawn(BuildingObjectPrefab).GetComponent<BuildingObject>();
			buildingObject.transform.position = GetWorldPosition(coord);
			buildingObject.gameObject.SetActive(true);

			buildingObject.Initialize(new RuntimeBuildingData()
			{
				State = BuildingState.Placed,
				SOID = building.ID
			});

			BuildingObjectDict[coord] = buildingObject;
		}

		private void DespawnAllBuildingObject()
		{
			List<Vector3Int> keys = new(BuildingObjectDict.Keys);
			foreach (Vector3Int coord in keys)
			{
				DespawnBuildingObject(coord);
			}
		}

		private void DespawnBuildingObject(Vector3Int coord)
		{
			if (BuildingObjectDict.TryGetValue(coord, out BuildingObject buildingObject) == false)
				return;

			// Debug.Log($"{nameof(DespawnBuildingObject)} ({coord}, {buildingObject.name})");
			ObjectPoolManager.Instance.Despawn(buildingObject.gameObject);
			BuildingObjectDict.Remove(coord);
		}
	}
}