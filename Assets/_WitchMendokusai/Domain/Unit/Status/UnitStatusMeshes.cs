using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 상태이상 머리 위 3D 궤도용 코드 생성 메시. 별 (두께 있는 뾰족 별) 과 고리 (납작한 도넛)
	// - 한 변 1 기준. 크기는 transform 으로
	// - 모양 값 (뾰족 수, 안쪽 비율, 두께, 고리 굵기) 이 같으면 한 메시를 같이 씀. 값이 바뀌면 새로 만듦
	internal static class UnitStatusMeshes
	{
		private static readonly Dictionary<Vector3Int, Mesh> stars = new();
		private static readonly Dictionary<Vector2Int, Mesh> rings = new();
		private const float KEY_SCALE = 1000f;

		// 별: 앞뒤 면 (가운데가 볼록) 과 옆면. 앞은 -z (카메라를 볼 때 LookRotation 의 앞)
		public static Mesh Star(int points, float innerRatio, float depth)
		{
			points = Mathf.Max(3, points);
			Vector3Int key = new(points, Mathf.RoundToInt(innerRatio * KEY_SCALE), Mathf.RoundToInt(depth * KEY_SCALE));
			if (stars.TryGetValue(key, out Mesh cached) && cached != null)
				return cached;

			int corners = points * 2;
			Vector3[] outline = new Vector3[corners];
			for (int i = 0; i < corners; i++)
			{
				float angle = Mathf.PI * 0.5f + i * Mathf.PI / points;
				float radius = (i % 2 == 0 ? 0.5f : 0.5f * innerRatio);
				outline[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
			}

			float halfDepth = depth * 0.5f;
			List<Vector3> vertices = new();
			List<Vector3> normals = new();
			List<int> triangles = new();

			// 앞뒤 면. 가운데 꼭짓점을 두께만큼 띄워 볼록하게
			for (int side = 0; side < 2; side++)
			{
				float sign = side == 0 ? -1f : 1f;
				Vector3 apex = new(0f, 0f, sign * halfDepth);
				for (int i = 0; i < corners; i++)
				{
					Vector3 from = outline[i];
					Vector3 to = outline[(i + 1) % corners];
					// 바깥 법선. 앞면은 -z 쪽, 뒷면은 +z 쪽을 보게
					Vector3 normal = Vector3.Cross(from - apex, to - apex).normalized;
					if (normal.z * sign < 0f)
						normal = -normal;
					int start = vertices.Count;
					vertices.Add(apex);
					vertices.Add(from);
					vertices.Add(to);
					normals.Add(normal);
					normals.Add(normal);
					normals.Add(normal);
					triangles.Add(start);
					triangles.Add(start + 1);
					triangles.Add(start + 2);
				}
			}

			Mesh mesh = new() { name = $"StatusStar{points}" };
			mesh.SetVertices(vertices);
			mesh.SetNormals(normals);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateBounds();
			FixWinding(mesh);
			stars[key] = mesh;
			return mesh;
		}

		// 고리: 반지름 0.5 의 납작한 띠 (위아래 면과 바깥 옆면). y 가 위
		public static Mesh Ring(int segments, float widthRatio)
		{
			segments = Mathf.Max(8, segments);
			Vector2Int key = new(segments, Mathf.RoundToInt(widthRatio * KEY_SCALE));
			if (rings.TryGetValue(key, out Mesh cached) && cached != null)
				return cached;

			float outer = 0.5f + widthRatio * 0.5f;
			float inner = 0.5f - widthRatio * 0.5f;
			float halfHeight = widthRatio * 0.35f;
			List<Vector3> vertices = new();
			List<Vector3> normals = new();
			List<int> triangles = new();
			for (int i = 0; i < segments; i++)
			{
				float angleFrom = i * Mathf.PI * 2f / segments;
				float angleTo = (i + 1) * Mathf.PI * 2f / segments;
				Vector3 directionFrom = new(Mathf.Cos(angleFrom), 0f, Mathf.Sin(angleFrom));
				Vector3 directionTo = new(Mathf.Cos(angleTo), 0f, Mathf.Sin(angleTo));
				// 윗면, 아랫면
				AddQuad(vertices, normals, triangles,
					directionFrom * inner + Vector3.up * halfHeight, directionFrom * outer + Vector3.up * halfHeight,
					directionTo * outer + Vector3.up * halfHeight, directionTo * inner + Vector3.up * halfHeight, Vector3.up);
				AddQuad(vertices, normals, triangles,
					directionTo * inner - Vector3.up * halfHeight, directionTo * outer - Vector3.up * halfHeight,
					directionFrom * outer - Vector3.up * halfHeight, directionFrom * inner - Vector3.up * halfHeight, Vector3.down);
				// 바깥, 안쪽 옆면
				AddQuad(vertices, normals, triangles,
					directionFrom * outer + Vector3.up * halfHeight, directionFrom * outer - Vector3.up * halfHeight,
					directionTo * outer - Vector3.up * halfHeight, directionTo * outer + Vector3.up * halfHeight, (directionFrom + directionTo).normalized);
				AddQuad(vertices, normals, triangles,
					directionTo * inner + Vector3.up * halfHeight, directionTo * inner - Vector3.up * halfHeight,
					directionFrom * inner - Vector3.up * halfHeight, directionFrom * inner + Vector3.up * halfHeight, -(directionFrom + directionTo).normalized);
			}
			Mesh mesh = new() { name = "StatusRing" };
			mesh.SetVertices(vertices);
			mesh.SetNormals(normals);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateBounds();
			FixWinding(mesh);
			rings[key] = mesh;
			return mesh;
		}

		private static void AddQuad(List<Vector3> vertices, List<Vector3> normals, List<int> triangles, Vector3 first, Vector3 second, Vector3 third, Vector3 fourth, Vector3 normal)
		{
			int start = vertices.Count;
			vertices.Add(first);
			vertices.Add(second);
			vertices.Add(third);
			vertices.Add(fourth);
			for (int i = 0; i < 4; i++)
				normals.Add(normal);
			triangles.Add(start);
			triangles.Add(start + 1);
			triangles.Add(start + 2);
			triangles.Add(start);
			triangles.Add(start + 2);
			triangles.Add(start + 3);
		}

		// 삼각형 감김을 법선 쪽이 앞면이 되게 맞춤. 손으로 감김 방향을 틀리지 않게 한 번 더 보정
		private static void FixWinding(Mesh mesh)
		{
			Vector3[] vertices = mesh.vertices;
			Vector3[] normals = mesh.normals;
			int[] triangles = mesh.triangles;
			for (int i = 0; i < triangles.Length; i += 3)
			{
				Vector3 first = vertices[triangles[i]];
				Vector3 faceNormal = Vector3.Cross(vertices[triangles[i + 1]] - first, vertices[triangles[i + 2]] - first);
				if (Vector3.Dot(faceNormal, normals[triangles[i]]) < 0f)
					(triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
			}
			mesh.triangles = triangles;
		}
	}
}
