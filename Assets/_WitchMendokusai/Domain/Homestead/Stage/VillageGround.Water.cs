using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// 물 자리의 바닥을 파고 수면을 얹음. 수면은 반투명이라 파낸 바닥이 비침
	// 충돌 바닥은 평평한 그대로. 물에 들어가지 못하게 막는 일은 스테이지 몫
	public sealed partial class VillageGround
	{
		[Header("Water")]
		[SerializeField, Min(0.1f)] private float waterMaxDepth = 1.2f;
		[SerializeField, Min(0f)] private float waterLevel = 0.2f;
		[SerializeField, Min(0.1f)] private float bankWidth = 2.4f;
		[SerializeField] private float bankNoiseScale = 0.45f;
		[SerializeField] private float bankNoiseStrength = 1.1f;
		[SerializeField, Min(0.1f)] private float waterCell = 0.35f;
		[SerializeField] private Vector2 bedRange = new(-0.4f, 0.7f);

		// 평지에서 파낸 깊이 (m). 물 밖은 0
		private float DugDepth(float x, float z)
		{
			float inside = -WaterDistance(x, z);
			if (inside <= 0f)
				return 0f;
			return Mathf.SmoothStep(0f, 1f, inside / bankWidth) * (waterMaxDepth + waterLevel);
		}

		// 물 자리 윤곽까지의 거리. 안쪽 음수. 원을 이어 붙인 티가 나지 않게 흔듦
		private float WaterDistance(float x, float z)
		{
			float distance = Nearest(waters, x, z);
			if (distance > bankWidth * 2f)
				return distance;
			return distance + (ForestFloor.Fractal(x * bankNoiseScale, z * bankNoiseScale, noiseSeed + 11, 2) - 0.5f) * bankNoiseStrength;
		}

		// 물과 물가. 놓을 수 없는 자리
		public bool IsWet(float x, float z) => WaterDistance(x, z) < 1f;

		private Rect WaterBounds()
		{
			float margin = bankWidth;
			float xMin = float.MaxValue;
			float zMin = float.MaxValue;
			float xMax = float.MinValue;
			float zMax = float.MinValue;
			foreach ((Vector2 center, float radius) in waters)
			{
				xMin = Mathf.Min(xMin, center.x - radius - margin);
				xMax = Mathf.Max(xMax, center.x + radius + margin);
				zMin = Mathf.Min(zMin, center.y - radius - margin);
				zMax = Mathf.Max(zMax, center.y + radius + margin);
			}
			return Rect.MinMaxRect(xMin, zMin, xMax, zMax);
		}

		private static List<float> Lines(float from, float denseFrom, float denseTo, float to, float step)
		{
			List<float> lines = new() { from };
			int count = Mathf.CeilToInt((denseTo - denseFrom) / step);
			for (int i = 0; i <= count; i++)
				lines.Add(denseFrom + i * step);
			lines.Add(to);
			return lines;
		}

		// 물 둘레만 촘촘한 격자. 나머지는 큰 판
		private Mesh FloorMesh()
		{
			Rect water = WaterBounds();
			List<float> columns = Lines(paintArea.xMin, water.xMin, water.xMax, paintArea.xMax, waterCell);
			List<float> rows = Lines(paintArea.yMin, water.yMin, water.yMax, paintArea.yMax, waterCell);
			List<Vector3> vertices = new();
			List<Vector3> normals = new();
			List<int> triangles = new();
			foreach (float z in rows)
			{
				foreach (float x in columns)
				{
					vertices.Add(new Vector3(x, -DugDepth(x, z), z));
					float slopeX = DugDepth(x + waterCell, z) - DugDepth(x - waterCell, z);
					float slopeZ = DugDepth(x, z + waterCell) - DugDepth(x, z - waterCell);
					normals.Add(new Vector3(slopeX, waterCell * 2f, slopeZ).normalized);
				}
			}
			AddGrid(triangles, columns.Count, rows.Count, _ => true);
			Mesh mesh = new() { name = "VillageFloor", hideFlags = HideFlags.DontSave };
			mesh.SetVertices(vertices);
			mesh.SetNormals(normals);
			mesh.SetTriangles(triangles, 0);
			return mesh;
		}

		private static void AddGrid(List<int> triangles, int columns, int rows, System.Predicate<int> isUsed)
		{
			for (int row = 0; row < rows - 1; row++)
			{
				for (int column = 0; column < columns - 1; column++)
				{
					int corner = row * columns + column;
					if (isUsed(corner) == false && isUsed(corner + 1) == false && isUsed(corner + columns) == false && isUsed(corner + columns + 1) == false)
						continue;
					triangles.Add(corner);
					triangles.Add(corner + columns);
					triangles.Add(corner + columns + 1);
					triangles.Add(corner);
					triangles.Add(corner + columns + 1);
					triangles.Add(corner + 1);
				}
			}
		}

		public GameObject BuildWaterSurface(Transform parent, Material material)
		{
			Rect water = WaterBounds();
			int columns = Mathf.CeilToInt(water.width / waterCell) + 1;
			int rows = Mathf.CeilToInt(water.height / waterCell) + 1;
			List<Vector3> vertices = new();
			List<Color> colors = new();
			List<int> triangles = new();
			for (int row = 0; row < rows; row++)
			{
				for (int column = 0; column < columns; column++)
				{
					float x = water.xMin + column * waterCell;
					float z = water.yMin + row * waterCell;
					vertices.Add(new Vector3(x, -waterLevel, z));
					colors.Add(new Color((DugDepth(x, z) - waterLevel) / waterMaxDepth, 0f, 0f, 1f));
				}
			}
			AddGrid(triangles, columns, rows, index => colors[index].r > 0f);
			Mesh mesh = new() { name = "VillageWater", hideFlags = HideFlags.DontSave };
			mesh.SetVertices(vertices);
			mesh.SetColors(colors);
			mesh.SetTriangles(triangles, 0);
			generated.Add(mesh);
			GameObject surface = new("WaterSurface");
			surface.transform.SetParent(parent, false);
			surface.AddComponent<MeshFilter>().sharedMesh = mesh;
			MeshRenderer surfaceRenderer = surface.AddComponent<MeshRenderer>();
			surfaceRenderer.sharedMaterial = material;
			surfaceRenderer.shadowCastingMode = ShadowCastingMode.Off;
			return surface;
		}

		// 물가와 물 밑은 자갈 (제어 텍스처 A)
		private float BedWeight(float x, float z, float noise) =>
			1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(bedRange.x, bedRange.y, WaterDistance(x, z) + noise * 0.3f));

		public Vector3 ShorePoint(Vector3 center, float angle, float offset)
		{
			Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
			float distance = 0f;
			while (distance < paintArea.width && DugDepth(center.x + direction.x * distance, center.z + direction.y * distance) > waterLevel)
				distance += waterCell * 0.5f;
			distance += offset;
			float x = center.x + direction.x * distance;
			float z = center.z + direction.y * distance;
			return new Vector3(x, -DugDepth(x, z), z);
		}
	}
}
