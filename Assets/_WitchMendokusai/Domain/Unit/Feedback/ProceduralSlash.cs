using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// 참격 모양은 재질, 각 인스턴스의 재생 위치는 두 번째 UV 채널에서 전달
	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
	public class ProceduralSlash : MonoBehaviour
	{
		[field: SerializeField, Min(0.01f)] public float Duration { get; private set; } = 0.32f;
		[SerializeField] private Material slashMaterial;

		private readonly Vector2[] phaseCoordinates = new Vector2[4];
		private Mesh slashMesh;
		private MeshRenderer slashRenderer;
		private ObjectPoolManager pool;
		private GameObject owner;
		private float elapsed;

		private void Awake()
		{
			slashMesh = new Mesh { name = "Procedural slash quad" };
			slashMesh.MarkDynamic();
			slashMesh.vertices = new[]
			{
				new Vector3(-1, -1, 0), new Vector3(1, -1, 0),
				new Vector3(1, 1, 0), new Vector3(-1, 1, 0),
			};
			slashMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
			slashMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
			slashMesh.bounds = new Bounds(Vector3.zero, new Vector3(2, 2, 0.01f));
			GetComponent<MeshFilter>().sharedMesh = slashMesh;
			slashRenderer = GetComponent<MeshRenderer>();
			slashRenderer.sharedMaterial = slashMaterial;
			slashRenderer.shadowCastingMode = ShadowCastingMode.Off;
			slashRenderer.receiveShadows = false;
			slashRenderer.enabled = false;
		}

		// 전투 참격은 판정과 독립된 수명으로 풀 재사용
		public void PlayPooled(ObjectPoolManager objectPool, GameObject attackOwner)
		{
			pool = objectPool;
			owner = attackOwner;
			elapsed = 0;
			Evaluate(0);
		}

		private void Update()
		{
			if (pool == null) return;
			elapsed += Time.deltaTime;
			if (owner == null || owner.activeInHierarchy == false || elapsed >= Duration)
			{
				pool.Despawn(gameObject);
				return;
			}
			Evaluate(elapsed);
		}

		// 카드 연출에서는 호출 측의 비스케일 시간 사용
		public void Evaluate(float age)
		{
			bool visible = isActiveAndEnabled && slashMaterial != null && age >= 0 && age < Duration;
			slashRenderer.enabled = visible;
			if (visible == false) return;
			float phase = age / Duration;
			for (int i = 0; i < phaseCoordinates.Length; i++) phaseCoordinates[i] = new Vector2(phase, 0);
			slashMesh.SetUVs(1, phaseCoordinates);
		}

		private void OnDisable()
		{
			pool = null;
			owner = null;
			if (slashRenderer != null) slashRenderer.enabled = false;
		}

		private void OnDestroy()
		{
			if (slashMesh != null) Destroy(slashMesh);
		}
	}
}
