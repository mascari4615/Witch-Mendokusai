using System.Collections.Generic;
using System.Linq;
using WitchMendokusai.Numerics;

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

		public void AddBuildingAt(Vector3Int pivot, BuildingInstanceData data)
		{
			// Debug.Log("AddObjectAt " + pivot);
			if (BuildingData.ContainsKey(pivot))
			{
				SdkLog.Warning("Already has object at " + pivot);
				return;
			}

			BuildingData[pivot] = data;
		}

		public void RemoveBuildingAt(Vector3Int pivot)
		{
			// Debug.Log("RemoveObjectAt " + pivot);
			if (BuildingData.ContainsKey(pivot) == false)
			{
				SdkLog.Warning("No object at " + pivot);
				return;
			}

			BuildingData.Remove(pivot);
			// BuildingObject 관리 클래스에서 Remove
		}

		public void Load(List<KeyValuePair<Vector3Int, BuildingInstanceData>> saveData)
		{
			// 건물이 없던 세이브(또는 밭만 있는 세이브)는 이 칸이 비어 있다 - 형제 레이어들처럼 그냥 지나간다
			// (여기만 null 에 터지면 「옛 세이브를 못 여는」 이유가 이 한 줄이 된다). TASK-WM-410 에서 실측.
			if (saveData == null)
			{
				return;
			}

			foreach ((Vector3Int key, BuildingInstanceData value) in saveData)
			{
				BuildingData[key] = value;
			}
		}

		public List<KeyValuePair<Vector3Int, BuildingInstanceData>> Save()
		{
			return BuildingData.ToList();
		}
	}
}