using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// 다음 공격이 닿을 자리를 발밑에 반투명으로 까는 바닥 실루엣
	// 모양은 공격 판정 상자의 바닥 면적 (끝이 뾰족한 칼날꼴), 방향과 크기는 쓰는 쪽이 정함
	public class AimSilhouette : MonoBehaviour
	{
		[SerializeField] private MeshFilter meshFilter;
		[SerializeField] private MeshRenderer meshRenderer;
		[SerializeField] private Color color = new(0f, 0f, 0f, 0.4f);
		[SerializeField, Min(0f), Tooltip("바닥에서 띄우는 높이 (월드 단위). 바닥과 겹쳐 깜빡이면 올림")] private float liftHeight = 0.03f;
		[SerializeField, Range(0f, 1f), Tooltip("끝이 뾰족해지는 길이. 앞뒤 길이에 대한 비율")] private float tipRatio = 0.35f;

		private Mesh mesh;
		private Vector4 footprint;
		private bool hasFootprint;

		private void Awake()
		{
			meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
			meshRenderer.receiveShadows = false;
			meshRenderer.enabled = false;
		}

		private void OnDestroy()
		{
			if (mesh != null)
				Destroy(mesh);
		}

		// 판정 상자의 바닥 면적. 공격 좌표 기준 (x 오른쪽, z 정면)
		public void SetFootprint(float minX, float maxX, float minZ, float maxZ)
		{
			Vector4 next = new(minX, maxX, minZ, maxZ);
			if (hasFootprint && next == footprint)
				return;
			footprint = next;
			hasFootprint = true;
			Rebuild();
		}

		public void Show(Vector3 origin, Vector3 direction)
		{
			if (hasFootprint == false || direction.sqrMagnitude < 0.0001f)
			{
				Hide();
				return;
			}
			origin.y += liftHeight;
			transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction, Vector3.up));
			meshRenderer.enabled = true;
		}

		public void Hide() => meshRenderer.enabled = false;

		private void Rebuild()
		{
			float minX = footprint.x;
			float maxX = footprint.y;
			float minZ = footprint.z;
			float maxZ = footprint.w;
			if (maxX - minX <= 0f || maxZ - minZ <= 0f)
			{
				hasFootprint = false;
				return;
			}
			float centerX = (minX + maxX) * 0.5f;
			float shoulderZ = maxZ - (maxZ - minZ) * tipRatio;
			List<Vector3> vertices = new()
			{
				new Vector3(centerX, 0f, (minZ + maxZ) * 0.5f),
				new Vector3(minX, 0f, minZ),
				new Vector3(maxX, 0f, minZ),
				new Vector3(maxX, 0f, shoulderZ),
				new Vector3(centerX, 0f, maxZ),
				new Vector3(minX, 0f, shoulderZ),
			};
			List<Color> colors = new();
			foreach (Vector3 vertex in vertices)
				colors.Add(color);
			// 중심에서 둘레를 도는 부채꼴. 위에서 볼 때 시계 방향
			List<int> triangles = new();
			for (int i = 1; i < vertices.Count; i++)
			{
				int next = i == vertices.Count - 1 ? 1 : i + 1;
				triangles.Add(0);
				triangles.Add(next);
				triangles.Add(i);
			}
			if (mesh == null)
				mesh = new Mesh { name = "Aim silhouette" };
			mesh.Clear();
			mesh.SetVertices(vertices);
			mesh.SetColors(colors);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateBounds();
			meshFilter.sharedMesh = mesh;
		}
	}
}
