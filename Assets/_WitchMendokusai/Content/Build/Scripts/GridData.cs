using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	public class GridData : ISavable<List<KeyValuePair<Vector3Int, RuntimeBuildingData>>>
	{
		public Dictionary<Vector3Int, RuntimeBuildingData> BuildingData { get; private set; } = new();

		public bool HasBuildingAt(Vector3Int pivot)
		{
			// Debug.Log($"{nameof(HasObjectAt)}({pivot}) = {BuildingData.ContainsKey(pivot)}");
			return BuildingData.ContainsKey(pivot);
		}

		public bool TryGetBuildingAt(Vector3Int pivot, out RuntimeBuildingData runtimeBuildingData)
		{
			// Debug.Log($"{nameof(TryGetObjectAt)}({pivot}) = {BuildingData.TryGetValue(pivot, out runtimeBuildingData)} {runtimeBuildingData}");
			return BuildingData.TryGetValue(pivot, out runtimeBuildingData);
		}

		public void AddBuildingAt(Vector3Int pivot, Building building)
		{
			// Debug.Log("AddObjectAt " + pivot);
			if (BuildingData.ContainsKey(pivot))
			{
				Debug.LogWarning("Already has object at " + pivot);
				return;
			}

			BuildingData[pivot] = new RuntimeBuildingData()
			{
				State = BuildingState.Placed,
				BuildingID = building.ID
			};
		}

		public void RemoveBuildingAt(Vector3Int pivot)
		{
			// Debug.Log("RemoveObjectAt " + pivot);
			if (BuildingData.ContainsKey(pivot) == false)
			{
				Debug.LogWarning("No object at " + pivot);
				return;
			}

			BuildingData.Remove(pivot);
			// BuildingObject 관리 클래스에서 Remove
		}

		public void Load(List<KeyValuePair<Vector3Int, RuntimeBuildingData>> saveData)
		{
			foreach ((Vector3Int key, RuntimeBuildingData value) in saveData)
			{
				AddBuildingAt(key, SOHelper.Get<Building>(value.BuildingID));
			}
		}

		public List<KeyValuePair<Vector3Int, RuntimeBuildingData>> Save()
		{
			return BuildingData.ToList();
		}
	}
}