using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class BuildManager : MonoBehaviour
	{
		private const string MarkerEnabled = "ENABLED";
		private const string MarkerResetTrigger = "RESET";

		private InputManager InputManager => InputManager.Instance;

		[SerializeField] private Grid grid;
		[SerializeField] private GameObject gridVisualization;
		[SerializeField] private Animator marker;
		[SerializeField] private GameObject blockPrefab;

		private Vector3 targetCellPos;
		private readonly GridData gridData = new();
		private bool isBuilding = false;

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

			if (marker.transform.position != targetCellPos)
			{
				if (marker.GetBool(MarkerEnabled) == true)
				{
					marker.transform.position = targetCellPos;
					marker.SetTrigger(MarkerResetTrigger);
				}
			}
		}

		private void UpdateCellPos()
		{
			Vector3 mousePosition = InputManager.MouseWorldPosition;
			Vector3Int gridPosition = grid.WorldToCell(mousePosition);
			Vector3 newTargetCellPos = grid.GetCellCenterWorld(gridPosition);
			newTargetCellPos.y = 0.01f;
			targetCellPos = newTargetCellPos;
		}

		private void ClickCell()
		{
			if (InputManager.IsPointerOverUI())
				return;

			if (gridData.TryGetObjectAt(grid.WorldToCell(targetCellPos), out GameObject obj))
			{
				gridData.RemoveObjectAt(grid.WorldToCell(targetCellPos));
				ObjectPoolManager.Instance.Despawn(obj);
				return;
			}
			else
			{
				GameObject block = ObjectPoolManager.Instance.Spawn(blockPrefab);
				block.transform.position = targetCellPos;
				block.SetActive(true);

				gridData.AddObjectAt(grid.WorldToCell(targetCellPos), block);
			}

			// buildingState.OnAction(gridPosition);
		}
	}
}