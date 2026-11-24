using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 변신 연출의 메시 (깃, 빛 막대, 휘감는 띠, 권총형 완드). 전부 코드 생성, 만든 쪽이 Destroy
	/// - FormEnergy 셰이더용 메시는 uv.x 가 뿌리 0 에서 끝 1, uv.y 가 가로 0..1
	/// - 막대 심과 완드는 빛 받는 UltimateMeshGlow 용이라 면마다 법선이 따로 (각진 결)
	/// </summary>
	public static class FormMeshes
	{
		/// <summary> 칼날 모양 깃 하나. 뿌리가 원점, 끝이 +X, 길이 1. 아래 (-Y) 로 bend, 뒤 (+Z) 로 sweep 만큼 휨. 가로로 살짝 볼록 (camber) </summary>
		public static Mesh Feather(int segments, float width, float bend, float sweep, float camber)
		{
			const int COLUMNS = 5;
			List<Vector3> vertices = new();
			List<Vector2> uvs = new();
			List<int> triangles = new();
			for (int segment = 0; segment <= segments; segment++)
			{
				float along = segment / (float)segments;
				// 뿌리에서 빠르게 넓어지고 끝은 날카롭게
				float half = width * 0.5f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Pow(along, 0.72f))), 0.75f);
				Vector3 center = new(along, -bend * along * along, sweep * along * along);
				for (int column = 0; column < COLUMNS; column++)
				{
					float across = column / (float)(COLUMNS - 1);
					float side = across * 2f - 1f;
					// 아랫깃을 더 넓게 (깃의 비대칭)
					float reach = side < 0f ? half * 1.25f : half * 0.75f;
					vertices.Add(center + new Vector3(0f, side * reach, -camber * (1f - side * side) * half));
					uvs.Add(new Vector2(along, across));
				}
			}
			Strip(triangles, segments, COLUMNS, 0);
			return Build("FormFeather", vertices, uvs, triangles);
		}

		/// <summary> 빛 막대 둘레 빛. 원점에서 +Y 로 길이 1. 날 모양 판 planes 장을 돌려 겹쳐 어느 쪽에서 봐도 두께가 있음 </summary>
		public static Mesh Sheath(int planes, int segments, float width)
		{
			List<Vector3> vertices = new();
			List<Vector2> uvs = new();
			List<int> triangles = new();
			for (int plane = 0; plane < planes; plane++)
			{
				float angle = Mathf.PI * plane / planes;
				Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
				int start = vertices.Count;
				for (int segment = 0; segment <= segments; segment++)
				{
					float along = segment / (float)segments;
					float half = width * 0.5f * (along < 0.12f ? Mathf.Sqrt(along / 0.12f) : Mathf.Pow(Mathf.Max(0f, 1f - (along - 0.12f) / 0.88f), 0.55f));
					for (int column = 0; column < 3; column++)
					{
						vertices.Add(Vector3.up * along + direction * (half * (column - 1)));
						uvs.Add(new Vector2(along, column * 0.5f));
					}
				}
				Strip(triangles, segments, 3, start);
			}
			return Build("FormSheath", vertices, uvs, triangles);
		}

		/// <summary> +Y 축을 turns 바퀴 감아 오르는 띠. 길이 1, 반지름은 뿌리 startRadius 에서 끝 endRadius </summary>
		public static Mesh Helix(int segments, float turns, float startRadius, float endRadius, float width, float phase)
		{
			List<Vector3> vertices = new();
			List<Vector2> uvs = new();
			List<int> triangles = new();
			for (int segment = 0; segment <= segments; segment++)
			{
				float along = segment / (float)segments;
				float angle = (along * turns + phase) * Mathf.PI * 2f;
				float radius = Mathf.Lerp(startRadius, endRadius, along);
				Vector3 center = new(Mathf.Cos(angle) * radius, along, Mathf.Sin(angle) * radius);
				float half = width * 0.5f * Mathf.Sin(Mathf.PI * along);
				Vector3 across = new Vector3(Mathf.Cos(angle) * 0.35f, 1f, Mathf.Sin(angle) * 0.35f).normalized;
				for (int column = 0; column < 3; column++)
				{
					vertices.Add(center + across * (half * (column - 1)));
					uvs.Add(new Vector2(along, column * 0.5f));
				}
			}
			Strip(triangles, segments, 3, 0);
			return Build("FormHelix", vertices, uvs, triangles);
		}

		/// <summary> 빛기둥 옆면 (UltimateBeamVolume 용). 높이 -1~1, 반지름 0.5, 법선은 옆 </summary>
		public static Mesh Column(int segments)
		{
			List<Vector3> vertices = new();
			List<Vector3> normals = new();
			List<int> triangles = new();
			for (int segment = 0; segment <= segments; segment++)
			{
				float angle = Mathf.PI * 2f * segment / segments;
				Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
				vertices.Add(direction * 0.5f + Vector3.down);
				vertices.Add(direction * 0.5f + Vector3.up);
				normals.Add(direction);
				normals.Add(direction);
			}
			for (int segment = 0; segment < segments; segment++)
			{
				int start = segment * 2;
				triangles.AddRange(new[] { start, start + 1, start + 2, start + 1, start + 3, start + 2 });
			}
			Mesh mesh = new() { name = "FormColumn" };
			mesh.SetVertices(vertices);
			mesh.SetNormals(normals);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateBounds();
			return mesh;
		}

		/// <summary> 네모 단면 빛 막대 심. 원점에서 +Y 로 길이 1. 밑동과 끝이 뾰족, 손 위에 마디 하나 </summary>
		public static Mesh Bar(float width)
		{
			FacetBuilder builder = new();
			float[] heights = { 0f, 0.04f, 0.1f, 0.12f, 0.15f, 0.8f, 1f };
			float[] halves = { 0.05f, 0.5f, 0.5f, 0.85f, 0.42f, 0.36f, 0f };
			for (int ring = 0; ring + 1 < heights.Length; ring++)
			{
				for (int side = 0; side < 4; side++)
				{
					Vector3 a = Corner(side, halves[ring] * width, heights[ring]);
					Vector3 b = Corner(side, halves[ring + 1] * width, heights[ring + 1]);
					Vector3 c = Corner(side + 1, halves[ring + 1] * width, heights[ring + 1]);
					Vector3 d = Corner(side + 1, halves[ring] * width, heights[ring]);
					builder.Quad(a, b, c, d, Color.white);
				}
			}
			return builder.Build("FormBar");
		}

		/// <summary> 권총형 완드 (각진 마법공학풍, 임시 외형). 총구가 +X, 손잡이가 -Y, 길이 약 1. 꼭짓점 a 는 발광 </summary>
		public static Mesh Wand()
		{
			FacetBuilder builder = new();
			Color body = new(0.72f, 0.46f, 0.16f, 0f);
			Color grip = new(0.3f, 0.2f, 0.14f, 0f);
			Color glow = new(1f, 0.95f, 0.7f, 1f);
			builder.Box(new Vector3(0.32f, 0.1f, 0f), new Vector3(0.42f, 0.085f, 0.06f), body);
			builder.Box(new Vector3(0.36f, 0.1f, 0f), new Vector3(0.36f, 0.03f, 0.068f), glow);
			builder.Box(new Vector3(0.78f, 0.1f, 0f), new Vector3(0.04f, 0.11f, 0.08f), body);
			builder.Box(new Vector3(0.84f, 0.1f, 0f), new Vector3(0.03f, 0.045f, 0.045f), glow);
			builder.Box(new Vector3(-0.03f, 0.04f, 0f), new Vector3(0.12f, 0.15f, 0.065f), body);
			builder.Box(new Vector3(-0.12f, -0.2f, 0f), new Vector3(0.085f, 0.18f, 0.052f), grip);
			builder.Box(new Vector3(-0.13f, -0.39f, 0f), new Vector3(0.1f, 0.03f, 0.06f), body);
			builder.Box(new Vector3(0.12f, -0.1f, 0f), new Vector3(0.1f, 0.018f, 0.03f), body);
			builder.Box(new Vector3(0.2f, -0.05f, 0f), new Vector3(0.018f, 0.06f, 0.03f), body);
			return builder.Build("FormWand");
		}

		private static Vector3 Corner(int side, float half, float height)
		{
			float angle = side * Mathf.PI * 0.5f;
			return new Vector3(Mathf.Cos(angle) * half, height, Mathf.Sin(angle) * half);
		}

		private static void Strip(List<int> triangles, int segments, int columns, int start)
		{
			for (int segment = 0; segment < segments; segment++)
			for (int column = 0; column + 1 < columns; column++)
			{
				int corner = start + segment * columns + column;
				triangles.Add(corner);
				triangles.Add(corner + columns);
				triangles.Add(corner + 1);
				triangles.Add(corner + 1);
				triangles.Add(corner + columns);
				triangles.Add(corner + columns + 1);
			}
		}

		private static Mesh Build(string name, List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
		{
			Mesh mesh = new() { name = name };
			Color[] colors = new Color[vertices.Count];
			System.Array.Fill(colors, Color.white);
			mesh.SetVertices(vertices);
			mesh.SetUVs(0, uvs);
			mesh.SetColors(colors);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			return mesh;
		}

		// 꼭짓점을 공유하지 않는 각진 면
		private sealed class FacetBuilder
		{
			private readonly List<Vector3> vertices = new();
			private readonly List<Color> colors = new();
			private readonly List<int> triangles = new();

			public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
			{
				int start = vertices.Count;
				vertices.AddRange(new[] { a, b, c, d });
				for (int i = 0; i < 4; i++)
					colors.Add(color);
				triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
			}

			public void Box(Vector3 center, Vector3 half, Color color)
			{
				Vector3 P(int x, int y, int z) => center + Vector3.Scale(half, new Vector3(x, y, z));
				Quad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), color);
				Quad(P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), P(-1, -1, 1), color);
				Quad(P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1), color);
				Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), color);
				Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), color);
				Quad(P(-1, -1, 1), P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), color);
			}

			public Mesh Build(string name)
			{
				Mesh mesh = new() { name = name };
				mesh.SetVertices(vertices);
				mesh.SetColors(colors);
				mesh.SetTriangles(triangles, 0);
				mesh.RecalculateNormals();
				mesh.RecalculateBounds();
				return mesh;
			}
		}
	}
}
