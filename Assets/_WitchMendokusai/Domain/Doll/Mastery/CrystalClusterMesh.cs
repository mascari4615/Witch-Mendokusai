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
		[Min(3), Tooltip("둘레 면 수. 4 면 마름모 보석, 6 이면 육각 기둥")] public int Sides;
		[Range(0f, 1f), Tooltip("위쪽 고리 폭 비율 (1 이면 기둥이 곧음)")] public float TopRingScale;
	}

	// 저폴리 보석 메시. 결정마다 둘레 기둥에 위아래 뾰족한 끝, 면마다 꼭짓점을 따로 둬 각진 음영 (WM 저폴리 결)
	// 정점 색 r 은 면 음영 (둘레 면 번갈아, 위 면 밝게, 아래 면 어둡게). MasteryGem 셰이더가 세 단 색에 씀
	// 숙련 조각 외형 (MasteryShardObject). 같은 설정은 같은 메시라 판 안에서 한 번만 만듦
	public static class CrystalClusterMesh
	{
		[System.Serializable]
		public struct FacetShade
		{
			[Range(0f, 1f), Tooltip("둘레 면 짝수 번째")] public float SideEven;
			[Range(0f, 1f), Tooltip("둘레 면 홀수 번째")] public float SideOdd;
			[Range(0f, 1f), Tooltip("위 뾰족한 면")] public float Crown;
			[Range(0f, 1f), Tooltip("아래 뾰족한 면")] public float Pavilion;
		}

		public static Mesh Build(IReadOnlyList<CrystalSpec> crystals, FacetShade shade, string meshName)
		{
			List<Vector3> vertices = new();
			List<Vector3> normals = new();
			List<Color> colors = new();
			List<int> triangles = new();
			foreach (CrystalSpec crystal in crystals)
			{
				if (crystal.Sides < 3)
					throw new System.ArgumentException($"{meshName}: 결정 둘레 면 수 {crystal.Sides} (3 이상)");
				AddCrystal(crystal, shade, new MeshLists(vertices, normals, colors, triangles));
			}

			Mesh mesh = new() { name = meshName };
			mesh.SetVertices(vertices);
			mesh.SetNormals(normals);
			mesh.SetColors(colors);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateBounds();
			return mesh;
		}

		private readonly struct MeshLists
		{
			public readonly List<Vector3> Vertices;
			public readonly List<Vector3> Normals;
			public readonly List<Color> Colors;
			public readonly List<int> Triangles;

			public MeshLists(List<Vector3> vertices, List<Vector3> normals, List<Color> colors, List<int> triangles)
			{
				Vertices = vertices;
				Normals = normals;
				Colors = colors;
				Triangles = triangles;
			}
		}

		private static void AddCrystal(CrystalSpec crystal, FacetShade shade, MeshLists lists)
		{
			int sides = crystal.Sides;
			Quaternion rotation = Quaternion.Euler(crystal.Tilt.x, 0f, crystal.Tilt.y);
			Vector3 Place(Vector3 local) => crystal.Offset + rotation * local;

			Vector3[] bottomRing = new Vector3[sides];
			Vector3[] topRing = new Vector3[sides];
			for (int side = 0; side < sides; side++)
			{
				float angle = Mathf.PI * 2f * side / sides;
				Vector3 direction = new(Mathf.Cos(angle) * crystal.Radius, 0f, Mathf.Sin(angle) * crystal.Radius);
				bottomRing[side] = Place(direction);
				// 위쪽 고리를 좁혀 결정이 끝으로 갈수록 가늘어지게
				topRing[side] = Place(direction * crystal.TopRingScale + Vector3.up * crystal.Height);
			}
			Vector3 tip = Place(Vector3.up * (crystal.Height + crystal.TipHeight));
			Vector3 baseCenter = Place(Vector3.down * crystal.BaseTipHeight);

			for (int side = 0; side < sides; side++)
			{
				int next = (side + 1) % sides;
				float sideShade = side % 2 == 0 ? shade.SideEven : shade.SideOdd;
				AddTriangle(bottomRing[side], bottomRing[next], topRing[next], sideShade, lists);
				AddTriangle(bottomRing[side], topRing[next], topRing[side], sideShade, lists);
				// 위아래 끝 면도 번갈아 살짝 달리해 면이 갈라져 보이게
				float alternate = side % 2 == 0 ? 1f : shade.SideOdd / Mathf.Max(shade.SideEven, 0.01f);
				AddTriangle(topRing[side], topRing[next], tip, shade.Crown * alternate, lists);
				AddTriangle(bottomRing[next], bottomRing[side], baseCenter, shade.Pavilion * alternate, lists);
			}
		}

		// 바깥에서 보아 시계 방향 (Unity 앞면)
		private static void AddTriangle(Vector3 a, Vector3 b, Vector3 c, float shade, MeshLists lists)
		{
			Vector3 normal = Vector3.Cross(c - a, b - a).normalized;
			Color color = new(shade, shade, shade, 1f);
			int start = lists.Vertices.Count;
			lists.Vertices.Add(a);
			lists.Vertices.Add(b);
			lists.Vertices.Add(c);
			for (int i = 0; i < 3; i++)
			{
				lists.Normals.Add(normal);
				lists.Colors.Add(color);
			}
			lists.Triangles.Add(start);
			lists.Triangles.Add(start + 2);
			lists.Triangles.Add(start + 1);
		}
	}
}
