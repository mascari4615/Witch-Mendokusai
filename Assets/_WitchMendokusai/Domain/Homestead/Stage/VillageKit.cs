using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// 저폴리 부품을 모아 재질과 색마다 한 메시로. 모양은 맵 오브젝트와 같은 MapObjectModelMesh (면 투영 UV, 각진 음영)
	// 건물 하나가 렌더러 몇 개로 끝나게 합침
	public sealed class VillageKit
	{
		private const float UV_PER_METER = 0.5f;

		private static readonly int BASE_COLOR = Shader.PropertyToID("_BaseColor");
		private static readonly List<Mesh> generated = new();
		private static MaterialPropertyBlock block;

		private readonly Dictionary<(Material Material, Color Color), List<CombineInstance>> groups = new();
		private readonly List<(Vector3 Center, Vector3 Size, Quaternion Rotation)> solids = new();

		// 판을 나갈 때 만든 메시 정리
		public static void ReleaseMeshes()
		{
			foreach (Mesh mesh in generated)
			{
				if (mesh != null)
					Object.Destroy(mesh);
			}
			generated.Clear();
		}

		public VillageKit Box(Material material, Color color, Vector3 foot, Vector3 size, Vector3 euler = default, bool solid = false)
		{
			Add(MapObjectPartShape.Box, material, color, foot, size, euler, 4, 1f);
			if (solid)
				Solid(foot, size, euler);
			return this;
		}

		public VillageKit Prism(Material material, Color color, Vector3 foot, Vector3 size, int sides, float topScale = 1f, Vector3 euler = default)
		{
			Add(MapObjectPartShape.Prism, material, color, foot, size, euler, sides, topScale);
			return this;
		}

		public VillageKit Crystal(Material material, Color color, Vector3 foot, Vector3 size, int sides, Vector3 euler = default)
		{
			Add(MapObjectPartShape.Crystal, material, color, foot, size, euler, sides, 1f);
			return this;
		}

		// 박공 지붕. 긴 쪽이 x, 용마루가 x 방향. 경사 판 둘과 삼각 벽 둘
		public VillageKit Gable(Material roof, Color roofColor, Material wall, Color wallColor, Vector3 foot, float width, float depth, float height, float overhang)
		{
			float halfDepth = depth * 0.5f + overhang;
			float slope = Mathf.Sqrt(halfDepth * halfDepth + height * height);
			float angle = Mathf.Atan2(height, halfDepth) * Mathf.Rad2Deg;
			const float THICKNESS = 0.14f;
			for (int side = -1; side <= 1; side += 2)
			{
				Vector3 center = foot + new Vector3(0f, height * 0.5f, side * halfDepth * 0.5f);
				Quaternion rotation = Quaternion.Euler(side * angle, 0f, 0f);
				Vector3 size = new(width + overhang * 2f, THICKNESS, slope + THICKNESS);
				Add(MapObjectPartShape.Box, roof, roofColor, center - rotation * Vector3.up * THICKNESS * 0.5f, size, new Vector3(side * angle, 0f, 0f), 4, 1f);
			}
			AddMesh(wall, wallColor, GableWalls(width, depth, height), Matrix4x4.Translate(foot));
			Add(MapObjectPartShape.Box, roof, roofColor * 0.85f, foot + new Vector3(0f, height - 0.06f, 0f), new Vector3(width + overhang * 2f + 0.1f, 0.18f, 0.22f), default, 4, 1f);
			return this;
		}

		public VillageKit Solid(Vector3 foot, Vector3 size, Vector3 euler = default)
		{
			Quaternion rotation = Quaternion.Euler(euler);
			solids.Add((foot + rotation * Vector3.up * (size.y * 0.5f), size, rotation));
			return this;
		}

		private static Mesh GableWalls(float width, float depth, float height)
		{
			Mesh mesh = new();
			List<Vector3> vertices = new();
			List<Vector3> normals = new();
			List<Vector2> uvs = new();
			List<int> triangles = new();
			for (int side = -1; side <= 1; side += 2)
			{
				float x = side * width * 0.5f;
				int first = vertices.Count;
				vertices.Add(new Vector3(x, 0f, -depth * 0.5f));
				vertices.Add(new Vector3(x, height, 0f));
				vertices.Add(new Vector3(x, 0f, depth * 0.5f));
				for (int i = 0; i < 3; i++)
				{
					normals.Add(new Vector3(side, 0f, 0f));
					uvs.Add(new Vector2(vertices[first + i].z, vertices[first + i].y) * UV_PER_METER);
				}
				triangles.AddRange(side > 0 ? new[] { first, first + 1, first + 2 } : new[] { first, first + 2, first + 1 });
			}
			mesh.SetVertices(vertices);
			mesh.SetNormals(normals);
			mesh.SetUVs(0, uvs);
			mesh.SetTriangles(triangles, 0);
			return mesh;
		}

		private void Add(MapObjectPartShape shape, Material material, Color color, Vector3 foot, Vector3 size, Vector3 euler, int sides, float topScale)
		{
			Mesh mesh = MapObjectModelMesh.Build(shape, size, sides, topScale, Color.white, UV_PER_METER, 0f);
			AddMesh(material, color, mesh, Matrix4x4.TRS(foot, Quaternion.Euler(euler), Vector3.one));
		}

		private void AddMesh(Material material, Color color, Mesh mesh, Matrix4x4 matrix)
		{
			if (groups.TryGetValue((material, color), out List<CombineInstance> parts) == false)
			{
				parts = new List<CombineInstance>();
				groups.Add((material, color), parts);
			}
			parts.Add(new CombineInstance { mesh = mesh, transform = matrix });
		}

		public GameObject Build(string objectName, Transform parent, Vector3 localPosition, float yaw, bool castShadow = true)
		{
			GameObject root = new(objectName);
			root.transform.SetParent(parent, false);
			root.transform.SetLocalPositionAndRotation(localPosition, Quaternion.Euler(0f, yaw, 0f));
			block ??= new MaterialPropertyBlock();
			foreach (KeyValuePair<(Material Material, Color Color), List<CombineInstance>> pair in groups)
			{
				Mesh mesh = new() { name = objectName, hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
				mesh.CombineMeshes(pair.Value.ToArray(), true, true);
				generated.Add(mesh);
				foreach (CombineInstance part in pair.Value)
					Object.Destroy(part.mesh);
				GameObject piece = new(pair.Key.Material.name);
				piece.transform.SetParent(root.transform, false);
				piece.AddComponent<MeshFilter>().sharedMesh = mesh;
				MeshRenderer renderer = piece.AddComponent<MeshRenderer>();
				renderer.sharedMaterial = pair.Key.Material;
				renderer.shadowCastingMode = castShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
				if (pair.Key.Material.HasProperty(BASE_COLOR))
				{
					renderer.GetPropertyBlock(block);
					block.SetColor(BASE_COLOR, pair.Key.Color * pair.Key.Material.GetColor(BASE_COLOR));
					renderer.SetPropertyBlock(block);
					block.Clear();
				}
			}
			foreach ((Vector3 center, Vector3 size, Quaternion rotation) in solids)
			{
				GameObject solid = new("Solid");
				solid.transform.SetParent(root.transform, false);
				solid.transform.SetLocalPositionAndRotation(center, rotation);
				solid.AddComponent<BoxCollider>().size = size;
			}
			return root;
		}
	}
}
