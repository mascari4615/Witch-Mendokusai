using UnityEngine;
// ★ 좌표는 판정 쪽 (TASK-WM-214) — 엔진으로 나갈 땐 자동, 엔진에서 받을 땐 캐스트.
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;
using Vector3 = WitchMendokusai.Numerics.Vector3;
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
			if (TryComputeInteractionWorldBounds(Model, out Bounds worldBounds) == false)
				return;

			BoxCollider interactionCollider = GetComponent<BoxCollider>();
			if (interactionCollider == null)
				interactionCollider = gameObject.AddComponent<BoxCollider>();

			// BuildingObject 루트 = identity 회전·scale 1 → world bounds 가 곧 local. InverseTransformPoint 로 안전 변환.
			interactionCollider.center = transform.InverseTransformPoint(worldBounds.center);
			interactionCollider.size = worldBounds.size;
		}

		// TASK-WM-181 — 인터랙션 콜라이더 bounds 계산 seam (회귀 테스트 진입점, WildBeastFleeTest 동격).
		// 모델 자식 전 Renderer 의 world bounds 합산. 렌더러 0 = false (콜라이더 안 만듦 = 클릭 불가).
		// 모든 Building prefab 이 비퇴화 bounds 를 내는지가 「일관 클릭」의 데이터 전제 — BuildingColliderFitTest 가 락.
		public static bool TryComputeInteractionWorldBounds(GameObject model, out Bounds worldBounds)
		{
			worldBounds = default;
			if (model == null)
				return false;

			Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
			if (renderers.Length == 0)
				return false;

			worldBounds = renderers[0].bounds;
			for (int i = 1; i < renderers.Length; i++)
				worldBounds.Encapsulate(renderers[i].bounds);
			return true;
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