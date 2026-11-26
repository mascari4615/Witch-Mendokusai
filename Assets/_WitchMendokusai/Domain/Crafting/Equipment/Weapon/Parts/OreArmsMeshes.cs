using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 광석 무장의 각진 메시. 앞이 +Z, 길이 1 은 월드 1. 꼭짓점 rgb 는 면마다 구운 명암을 곱한 바탕색, a 는 발광 (WM/Fx/FormSolid)
	// - 회전 총열: 직사각 광석 여섯 자루가 축을 두름. 축으로 돌림
	// - 총열 통: 총열을 무는 금 틀 (안 돎)
	// - 발사관: 광석 관 2x3 을 금 틀에
	public static class OreArmsMeshes
	{
		private const int BARRELS = 6;
		private const int RING_SIDES = 8;

		private static readonly Color GOLD = new(0.86f, 0.66f, 0.28f, 0f);
		private static readonly Color DARK = new(0.5f, 0.36f, 0.2f, 0f);
		private static readonly Color ORE = new(1f, 0.88f, 0.46f, 0.3f);
		private static readonly Color PALE = new(1f, 0.97f, 0.8f, 0.45f);
		private static readonly Color CORE = new(1f, 0.95f, 0.7f, 1f);

		/// <summary> 총열 앞끝 </summary>
		public const float BARREL_MUZZLE = 1.16f;
		/// <summary> 발사관 앞끝 </summary>
		public const float POD_MUZZLE = 0.72f;

		public static Mesh Barrels()
		{
			Builder builder = new();
			for (int i = 0; i < BARRELS; i++)
			{
				float angle = i * Mathf.PI * 2f / BARRELS;
				Vector3 outward = new(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
				Vector3 at = outward * 0.17f + Vector3.forward * 0.62f;
				float roll = angle * Mathf.Rad2Deg;
				builder.Box(at, new Vector3(0.072f, 0.072f, 0.52f), i % 2 == 0 ? ORE : PALE, roll);
				builder.Box(at + outward * 0.06f, new Vector3(0.02f, 0.03f, 0.4f), CORE, roll);
				builder.Box(new Vector3(at.x, at.y, 1.15f), new Vector3(0.05f, 0.05f, 0.015f), CORE, roll);
			}
			builder.Ring(0.32f, 0.37f, 0.285f, 0.285f, GOLD);
			builder.Ring(0.9f, 0.94f, 0.275f, 0.275f, GOLD);
			builder.Ring(0f, 1.1f, 0.07f, 0.07f, DARK);
			return builder.Build("OreBarrels");
		}

		public static Mesh Housing()
		{
			Builder builder = new();
			builder.Ring(-0.34f, 0.02f, 0.24f, 0.31f, GOLD);
			builder.Ring(0.02f, 0.14f, 0.31f, 0.27f, DARK);
			builder.Ring(-0.46f, -0.34f, 0.13f, 0.24f, DARK);
			builder.Box(new Vector3(0f, 0.3f, -0.12f), new Vector3(0.06f, 0.06f, 0.16f), PALE);
			builder.Box(new Vector3(0f, -0.3f, -0.14f), new Vector3(0.05f, 0.1f, 0.06f), DARK);
			builder.Box(new Vector3(0.3f, 0f, -0.14f), new Vector3(0.03f, 0.1f, 0.14f), CORE);
			builder.Box(new Vector3(-0.3f, 0f, -0.14f), new Vector3(0.03f, 0.1f, 0.14f), CORE);
			return builder.Build("OreHousing");
		}

		public static Mesh Pod()
		{
			Builder builder = new();
			for (int x = 0; x < 2; x++)
			for (int y = 0; y < 3; y++)
			{
				Vector3 at = new((x - 0.5f) * 0.25f, (y - 1) * 0.25f, 0.15f);
				builder.Box(at, new Vector3(0.1f, 0.1f, 0.55f), (x + y) % 2 == 0 ? ORE : PALE);
				builder.Box(at + Vector3.forward * 0.555f, new Vector3(0.066f, 0.066f, 0.01f), CORE);
			}
			builder.Box(new Vector3(0f, 0f, -0.3f), new Vector3(0.28f, 0.4f, 0.12f), GOLD);
			builder.Box(new Vector3(0f, 0f, -0.08f), new Vector3(0.27f, 0.395f, 0.025f), GOLD);
			builder.Box(new Vector3(0f, 0f, 0.46f), new Vector3(0.27f, 0.395f, 0.03f), GOLD);
			builder.Box(new Vector3(0f, 0f, -0.45f), new Vector3(0.2f, 0.3f, 0.06f), DARK);
			for (int side = -1; side <= 1; side += 2)
				builder.Box(new Vector3(side * 0.235f, 0f, 0.2f), new Vector3(0.012f, 0.3f, 0.2f), CORE);
			for (int i = 0; i < 3; i++)
				builder.Box(new Vector3(0f, 0.36f + i * 0.02f, -0.45f - i * 0.1f), new Vector3(0.03f, 0.2f - i * 0.04f, 0.06f), PALE);
			return builder.Build("OrePod");
		}

		/// <summary> 빛 조각 (팔면체, 반지름 1) </summary>
		public static Mesh Shard()
		{
			Builder builder = new();
			builder.Octahedron(CORE);
			return builder.Build("OreShard");
		}

		// 꼭짓점을 공유하지 않는 면. 면은 조각 가운데에서 바깥을 보게 감음
		private sealed class Builder
		{
			private readonly List<Vector3> vertices = new();
			private readonly List<Color> colors = new();
			private readonly List<int> triangles = new();
			private Vector3 center;

			public void Box(Vector3 at, Vector3 half, Color color, float roll = 0f)
			{
				center = at;
				Quaternion turn = Quaternion.Euler(0f, 0f, roll);
				Vector3 Corner(int x, int y, int z) => at + turn * Vector3.Scale(half, new Vector3(x, y, z));
				for (int side = -1; side <= 1; side += 2)
				{
					Quad(Corner(-1, -1, side), Corner(-1, 1, side), Corner(1, 1, side), Corner(1, -1, side), color);
					Quad(Corner(side, -1, -1), Corner(side, 1, -1), Corner(side, 1, 1), Corner(side, -1, 1), color);
					Quad(Corner(-1, side, -1), Corner(-1, side, 1), Corner(1, side, 1), Corner(1, side, -1), color);
				}
			}

			// z 축 둘레의 각진 통. 양 끝을 막음
			public void Ring(float z0, float z1, float radius0, float radius1, Color color)
			{
				center = Vector3.forward * ((z0 + z1) * 0.5f);
				for (int i = 0; i < RING_SIDES; i++)
				{
					Vector3 one = Around(i + 0.5f);
					Vector3 other = Around(i + 1.5f);
					Vector3 a = one * radius0 + Vector3.forward * z0;
					Vector3 b = other * radius0 + Vector3.forward * z0;
					Vector3 c = other * radius1 + Vector3.forward * z1;
					Vector3 d = one * radius1 + Vector3.forward * z1;
					Quad(a, d, c, b, color);
					Triangle(Vector3.forward * z1, c, d, color);
					Triangle(Vector3.forward * z0, a, b, color);
				}
			}

			public void Octahedron(Color color)
			{
				center = Vector3.zero;
				for (int x = -1; x <= 1; x += 2)
				for (int y = -1; y <= 1; y += 2)
				for (int z = -1; z <= 1; z += 2)
					Triangle(Vector3.right * x, Vector3.up * y, Vector3.forward * z, color);
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

			private static Vector3 Around(float step)
			{
				float angle = step / RING_SIDES * Mathf.PI * 2f;
				return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
			}

			private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
			{
				Triangle(a, b, c, color);
				Triangle(a, c, d, color);
			}

			private void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
			{
				Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
				if (Vector3.Dot(normal, (a + b + c) / 3f - center) < 0f)
				{
					(b, c) = (c, b);
					normal = -normal;
				}
				int start = vertices.Count;
				vertices.Add(a);
				vertices.Add(b);
				vertices.Add(c);
				Color baked = Shade(color, normal);
				for (int i = 0; i < 3; i++)
				{
					colors.Add(baked);
					triangles.Add(start + i);
				}
			}

			// 윗면은 밝고 옆면과 아랫면은 호박색으로 깊어짐. 발광하는 면은 그대로
			private static Color Shade(Color color, Vector3 normal)
			{
				if (color.a > 0.5f)
					return color;
				float shade = normal.y > 0.35f ? 1.12f : normal.y < -0.35f ? 0.5f : 0.72f;
				return new Color(color.r * shade, color.g * Mathf.Pow(shade, 1.15f), color.b * Mathf.Pow(shade, 1.6f), color.a);
			}
		}
	}
}
