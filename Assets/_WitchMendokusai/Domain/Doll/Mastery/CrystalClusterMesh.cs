using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[System.Serializable]
	public struct CrystalSpec
	{
		[Tooltip("밑면 중심 (조각 기준 로컬)")] public Vector3 Offset;
		[Tooltip("기울기 (도). x 앞뒤, z 좌우")] public Vector2 Tilt;
		[Min(0.01f)] public float Radius;
		[Min(0.01f), Tooltip("기둥 높이 (뾰족한 끝 제외)")] public float Height;
		[Min(0.01f), Tooltip("위 뾰족한 끝 높이")] public float TipHeight;
		[Min(0f), Tooltip("아래 뾰족한 끝 높이. 0 이면 평평한 밑면")] public float BaseTipHeight;
	}

	// 저폴리 수정 무리 메시. 결정마다 육각 기둥에 위아래 뾰족한 끝, 면마다 꼭짓점을 따로 둬 각진 음영 (WM 저폴리 결)
	// 숙련 조각 외형 (MasteryShardObject). 같은 설정은 같은 메시라 판 안에서 한 번만 만듦
	public static class CrystalClusterMesh
	{
		private const int SIDES = 6;

		public static Mesh Build(IReadOnlyList<CrystalSpec> crystals, string meshName)
		{
			List<Vector3> vertices = new();
			List<Vector3> normals = new();
			List<int> triangles = new();
			foreach (CrystalSpec crystal in crystals)
				AddCrystal(crystal, vertices, normals, triangles);

			Mesh mesh = new() { name = meshName };
			mesh.SetVertices(vertices);
			mesh.SetNormals(normals);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateBounds();
			return mesh;
		}

		private static void AddCrystal(CrystalSpec crystal, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
		{
			Quaternion rotation = Quaternion.Euler(crystal.Tilt.x, 0f, crystal.Tilt.y);
			Vector3 Place(Vector3 local) => crystal.Offset + rotation * local;

			Vector3[] bottomRing = new Vector3[SIDES];
			Vector3[] topRing = new Vector3[SIDES];
			for (int side = 0; side < SIDES; side++)
			{
				float angle = Mathf.PI * 2f * side / SIDES;
				Vector3 direction = new(Mathf.Cos(angle) * crystal.Radius, 0f, Mathf.Sin(angle) * crystal.Radius);
				bottomRing[side] = Place(direction);
				// 위쪽 고리를 조금 좁혀 결정이 끝으로 갈수록 가늘어지게
				topRing[side] = Place(direction * 0.85f + Vector3.up * crystal.Height);
			}
			Vector3 tip = Place(Vector3.up * (crystal.Height + crystal.TipHeight));
			Vector3 baseCenter = Place(Vector3.down * crystal.BaseTipHeight);

			for (int side = 0; side < SIDES; side++)
			{
				int next = (side + 1) % SIDES;
				AddQuad(bottomRing[side], bottomRing[next], topRing[next], topRing[side], vertices, normals, triangles);
				AddTriangle(topRing[side], topRing[next], tip, vertices, normals, triangles);
				AddTriangle(bottomRing[next], bottomRing[side], baseCenter, vertices, normals, triangles);
			}
		}

		private static void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
		{
			AddTriangle(a, b, c, vertices, normals, triangles);
			AddTriangle(a, c, d, vertices, normals, triangles);
		}

		// 바깥에서 보아 시계 방향 (Unity 앞면)
		private static void AddTriangle(Vector3 a, Vector3 b, Vector3 c, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
		{
			Vector3 normal = Vector3.Cross(c - a, b - a).normalized;
			int start = vertices.Count;
			vertices.Add(a);
			vertices.Add(b);
			vertices.Add(c);
			normals.Add(normal);
			normals.Add(normal);
			normals.Add(normal);
			triangles.Add(start);
			triangles.Add(start + 2);
			triangles.Add(start + 1);
		}
	}
}
