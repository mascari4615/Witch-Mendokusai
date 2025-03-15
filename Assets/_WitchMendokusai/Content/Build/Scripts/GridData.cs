using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	public class GridData : ISavable<List<KeyValuePair<Vector3Int, RuntimeBuildingData>>>
	{
		public Dictionary<Vector3Int, RuntimeBuildingData> BuildingData { get; set; } = new();

		public bool HasObjectAt(Vector3Int gridPosition)
		{
			// Debug.Log($"{nameof(HasObjectAt)}({gridPosition}) = {BuildingData.ContainsKey(gridPosition)}");
			return BuildingData.ContainsKey(gridPosition);
		}

		public bool TryGetObjectAt(Vector3Int gridPosition, out RuntimeBuildingData runtimeBuildingData)
		{
			// Debug.Log($"{nameof(TryGetObjectAt)}({gridPosition}) = {BuildingData.TryGetValue(gridPosition, out runtimeBuildingData)} {runtimeBuildingData}");
			return BuildingData.TryGetValue(gridPosition, out runtimeBuildingData);
		}

		public void AddObjectAt(Vector3Int gridPosition, Building building)
		{
			// Debug.Log("AddObjectAt " + gridPosition);
			if (BuildingData.ContainsKey(gridPosition))
			{
				Debug.LogWarning("Already has object at " + gridPosition);
				return;
			}

			BuildingData[gridPosition] = new RuntimeBuildingData()
			{
				State = BuildingState.Placed,
				SOID = building.ID
			};
		}

		public void RemoveObjectAt(Vector3Int gridPosition)
		{
			// Debug.Log("RemoveObjectAt " + gridPosition);
			if (BuildingData.ContainsKey(gridPosition) == false)
			{
				Debug.LogWarning("No object at " + gridPosition);
				return;
			}

			BuildingData.Remove(gridPosition);
			// BuildingObject 관리 클래스에서 Remove
		}

		public void Load(List<KeyValuePair<Vector3Int, RuntimeBuildingData>> saveData)
		{
			foreach ((Vector3Int key, RuntimeBuildingData value) in saveData)
			{
				AddObjectAt(key, SOHelper.Get<Building>(value.SOID));
			}
		}

		public List<KeyValuePair<Vector3Int, RuntimeBuildingData>> Save()
		{
			return BuildingData.ToList();
		}
	}
}