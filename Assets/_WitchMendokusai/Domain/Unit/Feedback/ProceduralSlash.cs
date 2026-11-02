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

		private const int LengthSegments = 32;
		private const int DepthSegments = 4;
		private readonly Vector2[] phaseCoordinates = new Vector2[(LengthSegments + 1) * (DepthSegments + 1)];
		private Mesh slashMesh;
		private MeshRenderer slashRenderer;
		private ObjectPoolManager pool;
		private GameObject owner;
		private float elapsed;

		private void Awake()
		{
			slashMesh = new Mesh { name = "Curved slash sheet" };
			slashMesh.MarkDynamic();
			Vector3[] vertices = new Vector3[phaseCoordinates.Length];
			Vector2[] coordinates = new Vector2[phaseCoordinates.Length];
			int[] triangles = new int[LengthSegments * DepthSegments * 6];
			for (int length = 0; length <= LengthSegments; length++)
			{
				for (int depth = 0; depth <= DepthSegments; depth++)
				{
					int index = length * (DepthSegments + 1) + depth;
					coordinates[index] = new Vector2((float)length / LengthSegments, (float)depth / DepthSegments);
					vertices[index] = coordinates[index];
					if (length == LengthSegments || depth == DepthSegments) continue;
					int triangle = (length * DepthSegments + depth) * 6;
					triangles[triangle] = index;
					triangles[triangle + 1] = index + DepthSegments + 1;
					triangles[triangle + 2] = index + 1;
					triangles[triangle + 3] = index + 1;
					triangles[triangle + 4] = index + DepthSegments + 1;
					triangles[triangle + 5] = index + DepthSegments + 2;
				}
			}
			slashMesh.vertices = vertices;
			slashMesh.uv = coordinates;
			slashMesh.triangles = triangles;
			// 셰이더가 곡면으로 접고 펼치는 전체 범위 포함
			slashMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4);
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
