using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public enum BuildingState
	{
		Building,
		Placed,
		Upgrading,
		UpgradingComplete,
	}

	[Serializable]
	public struct BuildingInstanceData
	{
		public int BuildingID;
		public BuildingState State;
		public int Level;
		public string RuntimeData;

		public BuildingInstanceData(int buildingID, BuildingState state = BuildingState.Placed, int level = 1, string runtimeData = "")
		{
			BuildingID = buildingID;
			State = state;
			Level = level;
			RuntimeData = runtimeData;
		}
	}

	public class BuildingObject : MonoBehaviour
	{
		public BuildingInstanceData SaveData { get; private set; } = new();
		public Building Building => Get<Building>(SaveData.BuildingID);

		public Vector3Int Pivot { get; private set; }
		public GameObject Model { get; private set; } = null;
		[SerializeField] private Transform modelParent = null;

		public void Initialize(BuildingInstanceData saveData, Vector3Int pivot)
		{
			SaveData = saveData;
			Pivot = pivot;

			Model = ObjectPoolManager.Instance.Spawn(Building.Prefab, modelParent);
			Model.SetActive(true);
		}

		public void Despawn()
		{
			// Debug.Log($"{nameof(Despawn)} ({Pivot}, {Building.name})");
			Model.SetActive(false);
			ObjectPoolManager.Instance.Despawn(Model);
			Model = null;
		}
	}
}