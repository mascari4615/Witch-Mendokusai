using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
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

		public void UpdateRuntimeData(string json)
		{
			SaveData = new BuildingInstanceData(SaveData.BuildingID, SaveData.State, SaveData.Level, json);
			if (StageManager.Instance.CurStage is WorldStage worldStage)
				worldStage.GridData.BuildingData[Pivot] = SaveData;
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