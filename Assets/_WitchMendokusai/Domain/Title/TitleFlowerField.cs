using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 시작 화면 들꽃. 납작한 점을 지형 위에 흩뿌려 색마다 한 서브메시로 합침
	// 메시는 자산으로 저장하지 않고 켜질 때 생성. 재질 순서가 색 순서
	[ExecuteAlways]
	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
	public class TitleFlowerField : MonoBehaviour
	{
		[SerializeField] private TitleTerrain terrain;
		[SerializeField] private int seed = 7;
		[SerializeField] private int count = 2400;
		[SerializeField] private Vector2 center = new(-8f, -45f);
		[SerializeField] private float radius = 70f;
		[Tooltip("반지름에 곱하는 x, z 배율. 카메라 앞쪽으로 넓게 깔기")]
		[SerializeField] private Vector2 stretch = new(0.9f, 0.5f);
		[Tooltip("이 z 보다 카메라 쪽에는 두지 않음. 너무 가까운 점은 크게 보임")]
		[SerializeField] private float nearLimit = -80f;
		[SerializeField] private Vector2 sizeRange = new(0.06f, 0.11f);
		[SerializeField] private float flatten = 0.5f;
		[SerializeField] private float lift = 0.12f;
		[Tooltip("색마다 뽑힐 비중. 길이는 재질 수와 같게")]
		[SerializeField] private float[] colorWeights = { 2f, 1f, 1f };
		[Tooltip("점 하나의 둘레 꼭짓점 수")]
		[SerializeField, Min(3)] private int segments = 6;

		private void Awake() => Build();

		[ContextMenu("Build")]
		public void Build()
		{
			if (terrain == null)
				return;
			MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
			int colorCount = Mathf.Max(1, meshRenderer.sharedMaterials.Length);
			List<Vector3> vertices = new();
			List<Vector3> normals = new();
			List<int>[] triangles = new List<int>[colorCount];
			for (int i = 0; i < colorCount; i++)
				triangles[i] = new List<int>();

			System.Random random = new(seed);
			for (int i = 0; i < count; i++)
			{
				float angle = (float)random.NextDouble() * Mathf.PI * 2f;
				float distance = Mathf.Sqrt((float)random.NextDouble()) * radius;
				float x = center.x + Mathf.Cos(angle) * distance * stretch.x;
				float z = center.y + Mathf.Sin(angle) * distance * stretch.y;
				int colorIndex = PickColor(random, colorCount);
				float size = Mathf.Lerp(sizeRange.x, sizeRange.y, (float)random.NextDouble());
				if (z < nearLimit)
					continue;
				Vector3 position = transform.InverseTransformPoint(new Vector3(x, terrain.HeightAt(x, z) + lift, z));
				AddDot(vertices, normals, triangles[colorIndex], position, size);
			}

			Mesh mesh = new() { name = "TitleFlowers", hideFlags = HideFlags.DontSave };
			mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			mesh.SetVertices(vertices);
			mesh.SetNormals(normals);
			mesh.subMeshCount = colorCount;
			for (int i = 0; i < colorCount; i++)
				mesh.SetTriangles(triangles[i], i);
			mesh.RecalculateBounds();
			GetComponent<MeshFilter>().sharedMesh = mesh;
		}

		private int PickColor(System.Random random, int colorCount)
		{
			float total = 0f;
			for (int i = 0; i < colorCount; i++)
				total += Weight(i);
			float roll = (float)random.NextDouble() * total;
			for (int i = 0; i < colorCount; i++)
			{
				roll -= Weight(i);
				if (roll <= 0f)
					return i;
			}
			return colorCount - 1;
		}

		private float Weight(int index) => colorWeights != null && index < colorWeights.Length ? colorWeights[index] : 1f;

		// 납작한 공 하나. 카메라가 낮아 옆에서도 보이게 위아래 두께를 둠
		private void AddDot(List<Vector3> vertices, List<Vector3> normals, List<int> triangles, Vector3 position, float size)
		{
			int start = vertices.Count;
			float height = size * flatten;
			vertices.Add(position + Vector3.up * height);
			normals.Add(Vector3.up);
			for (int i = 0; i < segments; i++)
			{
				float angle = i * Mathf.PI * 2f / segments;
				Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
				vertices.Add(position + direction * size);
				normals.Add(direction);
			}
			vertices.Add(position + Vector3.down * height);
			normals.Add(Vector3.down);
			int bottom = start + segments + 1;
			for (int i = 0; i < segments; i++)
			{
				int current = start + 1 + i;
				int next = start + 1 + (i + 1) % segments;
				triangles.Add(start);
				triangles.Add(next);
				triangles.Add(current);
				triangles.Add(bottom);
				triangles.Add(current);
				triangles.Add(next);
			}
		}
	}
}
