using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public enum BuildingState
	{
		Placed,
	}

	[Serializable]
	public struct RuntimeBuildingData
	{
		public int BuildingID;
		public BuildingState State;

		public RuntimeBuildingData(int buildingID, BuildingState state)
		{
			BuildingID = buildingID;
			State = state;
		}
	}

	public class BuildingObject : MonoBehaviour
	{
		public RuntimeBuildingData SaveData { get; private set; } = new();
		public Building Building => Get<Building>(SaveData.BuildingID);
		public Vector3Int Pivot { get; private set; }

		public GameObject Model { get; private set; } = null;
		[SerializeField] private Transform modelParent = null;

		public void Initialize(RuntimeBuildingData saveData, Vector3Int pivot)
		{
			SaveData = saveData;
			Pivot = pivot;

			Model = ObjectPoolManager.Instance.Spawn(Building.Prefab, modelParent);
			Model.SetActive(true);
		}

		public void Despawn()
		{
			Model.SetActive(false);
			ObjectPoolManager.Instance.Despawn(Model);
			Model = null;
		}
	}
}