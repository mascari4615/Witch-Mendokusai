using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	public class GridData : ISavable<List<KeyValuePair<Vector3Int, BuildingInstanceData>>>
	{
		public Dictionary<Vector3Int, BuildingInstanceData> BuildingData { get; private set; } = new();

		public bool HasBuildingAt(Vector3Int pivot)
		{
			// Debug.Log($"{nameof(HasObjectAt)}({pivot}) = {BuildingData.ContainsKey(pivot)}");
			return BuildingData.ContainsKey(pivot);
		}

		public bool TryGetBuildingAt(Vector3Int pivot, out BuildingInstanceData runtimeBuildingData)
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

			BuildingData[pivot] = new BuildingInstanceData()
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

		public void Load(List<KeyValuePair<Vector3Int, BuildingInstanceData>> saveData)
		{
			foreach ((Vector3Int key, BuildingInstanceData value) in saveData)
			{
				AddBuildingAt(key, SOHelper.Get<Building>(value.BuildingID));
			}
		}

		public List<KeyValuePair<Vector3Int, BuildingInstanceData>> Save()
		{
			return BuildingData.ToList();
		}
	}
}