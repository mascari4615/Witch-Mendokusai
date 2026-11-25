using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// 집 안과 갱도, 마을이 같이 쓰는 등불과 바깥 가림
	public static class VillageLights
	{
		private const int SHADE_COUNT = 20;
		private const float DOME_SINK = 0.08f;

		private static HomesteadArt Art => HomesteadArt.Instance;

		public static Light AddGlow(Transform body, Vector3 localPosition, float range, float intensity, Color color)
		{
			GameObject lightObject = new("Glow");
			lightObject.transform.SetParent(body, false);
			lightObject.transform.localPosition = localPosition;
			Light lamp = lightObject.AddComponent<Light>();
			lamp.type = LightType.Point;
			lamp.range = range;
			lamp.intensity = intensity;
			lamp.color = color;
			return lamp;
		}

		// 카메라까지 덮는 어두운 반구와 바닥, 해만 막는 지붕. 안쪽 면만 보임
		public static GameObject BuildDome(Transform root, float radius, float roofHeight, Color shade)
		{
			const int SIDES = 32;
			const int RINGS = 10;
			System.Collections.Generic.List<Vector3> points = new();
			System.Collections.Generic.List<Vector3> normals = new();
			System.Collections.Generic.List<Color> colors = new();
			System.Collections.Generic.List<int> triangles = new();
			for (int ring = 0; ring <= RINGS; ring++)
			{
				float rise = ring / (float)RINGS * Mathf.PI * 0.5f;
				for (int side = 0; side <= SIDES; side++)
				{
					float turn = side / (float)SIDES * Mathf.PI * 2f;
					Vector3 direction = new(Mathf.Cos(turn) * Mathf.Cos(rise), Mathf.Sin(rise), Mathf.Sin(turn) * Mathf.Cos(rise));
					points.Add(direction * radius + Vector3.down * DOME_SINK);
					normals.Add(-direction);
					colors.Add(shade);
				}
			}
			int row = SIDES + 1;
			for (int ring = 0; ring < RINGS; ring++)
			{
				for (int side = 0; side < SIDES; side++)
				{
					int first = ring * row + side;
					triangles.AddRange(new[] { first, first + 1, first + row, first + 1, first + row + 1, first + row });
				}
			}
			int center = points.Count;
			points.Add(Vector3.down * DOME_SINK);
			normals.Add(Vector3.up);
			colors.Add(shade);
			for (int side = 0; side < SIDES; side++)
				triangles.AddRange(new[] { center, side + 1, side });
			Mesh mesh = new() { name = "Dome", hideFlags = HideFlags.DontSave };
			mesh.SetVertices(points);
			mesh.SetNormals(normals);
			mesh.SetColors(colors);
			mesh.SetTriangles(triangles, 0);
			GameObject dome = new("Dome");
			dome.transform.SetParent(root, false);
			dome.AddComponent<MeshFilter>().sharedMesh = mesh;
			MeshRenderer domeRenderer = dome.AddComponent<MeshRenderer>();
			domeRenderer.sharedMaterial = Art.Dark;
			domeRenderer.shadowCastingMode = ShadowCastingMode.Off;
			BuildRoof(root, radius * 0.7f, roofHeight, 0f);
			return dome;
		}

		private static void BuildRoof(Transform root, float radius, float height, float depth)
		{
			VillageKit roof = new();
			roof.Prism(Art.Dark, Color.white, new Vector3(0f, height, 0f), new Vector3(radius * 2.4f, 0.4f, radius * 2.4f), 16);
			for (int i = 0; i < SHADE_COUNT; i++)
			{
				float angle = i / (float)SHADE_COUNT * Mathf.PI * 2f;
				Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
				roof.Box(Art.Dark, Color.white, direction * radius * 1.15f - Vector3.up * depth, new Vector3(radius * 0.4f, height + depth, 0.4f), new Vector3(0f, 90f - angle * Mathf.Rad2Deg, 0f));
			}
			foreach (MeshRenderer part in roof.Build("Shade", root, Vector3.zero, 0f).GetComponentsInChildren<MeshRenderer>())
				part.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
		}

		// 바깥 하늘과 해를 가림. 어두운 바닥과 둘레 벽, 해만 막는 지붕
		public static void BuildShade(Transform root, float radius, float height, float depth, Color shade)
		{
			VillageKit dark = new();
			dark.Prism(Art.Dark, shade, new Vector3(0f, -depth, 0f), new Vector3(radius * 6f, 0.3f, radius * 6f), 24);
			for (int i = 0; i < SHADE_COUNT; i++)
			{
				float angle = i / (float)SHADE_COUNT * Mathf.PI * 2f;
				Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
				Vector3 euler = new(0f, 90f - angle * Mathf.Rad2Deg, 0f);
				float far = radius * 1.6f;
				// 카메라가 서는 남쪽은 비움
				if (direction.z > -0.5f)
					dark.Box(Art.Dark, shade, direction * far - Vector3.up * depth, new Vector3(far * 0.36f, height * 2.6f, 0.4f), euler);
			}
			dark.Build("Dark", root, Vector3.zero, 0f, castShadow: false);
			BuildRoof(root, radius, height, depth);
		}
	}
}
