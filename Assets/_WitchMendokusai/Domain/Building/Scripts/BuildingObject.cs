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

			FitInteractionCollider();
		}

		// TASK-WM-181 — 아트 메쉬의 콜라이더 유무/구조/오프셋과 무관하게 BuildingObject 루트에
		// 렌더 bounds 맞춤 BoxCollider 부착. 임시블럭(BoxCollider O)·마녀의집(루트 콜라이더)·Lab(콜라이더 X)·
		// 모루/솥(메쉬 따로) 전부 일관 클릭 = 우클릭 위 적층 / 좌클릭 제거 견고. 풀 재사용 시 기존 콜라이더 재사용.
		private void FitInteractionCollider()
		{
			Renderer[] renderers = Model.GetComponentsInChildren<Renderer>();
			if (renderers.Length == 0)
				return;

			Bounds worldBounds = renderers[0].bounds;
			for (int i = 1; i < renderers.Length; i++)
				worldBounds.Encapsulate(renderers[i].bounds);

			BoxCollider interactionCollider = GetComponent<BoxCollider>();
			if (interactionCollider == null)
				interactionCollider = gameObject.AddComponent<BoxCollider>();

			// BuildingObject 루트 = identity 회전·scale 1 → world bounds 가 곧 local. InverseTransformPoint 로 안전 변환.
			interactionCollider.center = transform.InverseTransformPoint(worldBounds.center);
			interactionCollider.size = worldBounds.size;
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