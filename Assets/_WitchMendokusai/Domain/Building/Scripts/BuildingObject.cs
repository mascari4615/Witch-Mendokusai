using UnityEngine;
using VContainer;
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

		private ObjectPoolManager objectPoolManager;
		private StageManager stageManager;

		[Inject]
		public void Construct(ObjectPoolManager objectPoolManager, StageManager stageManager)
		{
			this.objectPoolManager = objectPoolManager;
			this.stageManager = stageManager;
		}

		public void Initialize(BuildingInstanceData saveData, Vector3Int pivot)
		{
			SaveData = saveData;
			Pivot = pivot;

			Model = objectPoolManager.Spawn(Building.Prefab, modelParent);
			Model.SetActive(true);
		}

		public void UpdateRuntimeData(string json)
		{
			SaveData = new BuildingInstanceData(SaveData.BuildingID, SaveData.State, SaveData.Level, json);
			if (stageManager.CurStage is WorldStage worldStage)
				worldStage.GridData.BuildingData[Pivot] = SaveData;
		}

		public void Despawn()
		{
			// Debug.Log($"{nameof(Despawn)} ({Pivot}, {Building.name})");
			Model.SetActive(false);
			objectPoolManager.Despawn(Model);
			Model = null;
		}
	}
}