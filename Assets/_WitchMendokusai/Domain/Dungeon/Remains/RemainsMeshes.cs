using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 조각 모양 메시. 처음 쓸 때 한 번 만들고 계속 씀. 크기 1 기준, 중심이 원점
	public static class RemainsMeshes
	{
		private static readonly Dictionary<RemainsPieceShape, Mesh> cache = new();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetCache() => cache.Clear();

		public static Mesh Get(RemainsPieceShape shape)
		{
			if (cache.TryGetValue(shape, out Mesh mesh) && mesh != null)
				return mesh;
			mesh = shape switch
			{
				RemainsPieceShape.Chip => Box(new Vector3(1f, .7f, .85f)),
				RemainsPieceShape.Blob => Blob(),
				RemainsPieceShape.Shard => Box(new Vector3(1f, .22f, .55f)),
				RemainsPieceShape.Tuft => Box(new Vector3(1f, .3f, .3f)),
				_ => throw new System.ArgumentOutOfRangeException(nameof(shape), shape, null),
			};
			mesh.name = $"Remains_{shape}";
			cache[shape] = mesh;
			return mesh;
		}

		// 누웠을 때 중심 높이와 날 때 띄울 거리 (크기 1 기준)
		public static float RestClearance(RemainsPieceShape shape) => shape switch
		{
			RemainsPieceShape.Chip => .35f,
			RemainsPieceShape.Blob => .1f,
			RemainsPieceShape.Shard => .11f,
			RemainsPieceShape.Tuft => .15f,
			_ => throw new System.ArgumentOutOfRangeException(nameof(shape), shape, null),
		};

		public static float FlyClearance(RemainsPieceShape shape) => shape == RemainsPieceShape.Blob ? .5f : .6f;

		private static Mesh Box(Vector3 size)
		{
			Vector3 half = size * .5f;
			Vector3[] corners =
			{
				new(-half.x, -half.y, -half.z), new(half.x, -half.y, -half.z), new(half.x, half.y, -half.z), new(-half.x, half.y, -half.z),
				new(-half.x, -half.y, half.z), new(half.x, -half.y, half.z), new(half.x, half.y, half.z), new(-half.x, half.y, half.z),
			};
			int[][] faces = { new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 }, new[] { 3, 7, 6, 2 }, new[] { 0, 4, 7, 3 }, new[] { 1, 2, 6, 5 } };
			List<Vector3> vertices = new();
			List<int> triangles = new();
			foreach (int[] face in faces)
			{
				int start = vertices.Count;
				foreach (int corner in face)
					vertices.Add(corners[corner]);
				triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
			}
			Mesh mesh = new();
			mesh.SetVertices(vertices);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			return mesh;
		}

		// 납작한 팔면체를 한 번 나눈 덩이. 바닥 쪽이 더 납작해 눌린 점액처럼
		private static Mesh Blob()
		{
			List<Vector3> vertices = new() { Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
			List<int> triangles = new() { 0, 4, 2, 0, 2, 5, 0, 5, 3, 0, 3, 4, 1, 2, 4, 1, 5, 2, 1, 3, 5, 1, 4, 3 };
			Dictionary<long, int> midpoints = new();
			List<int> divided = new();
			for (int i = 0; i < triangles.Count; i += 3)
			{
				int a = triangles[i];
				int b = triangles[i + 1];
				int c = triangles[i + 2];
				int ab = Midpoint(vertices, midpoints, a, b);
				int bc = Midpoint(vertices, midpoints, b, c);
				int ca = Midpoint(vertices, midpoints, c, a);
				divided.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
			}
			for (int i = 0; i < vertices.Count; i++)
			{
				Vector3 point = vertices[i].normalized * .5f;
				point.y *= point.y < 0f ? .2f : .38f;
				vertices[i] = point;
			}
			Mesh mesh = new();
			mesh.SetVertices(vertices);
			mesh.SetTriangles(divided, 0);
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			return mesh;
		}

		private static int Midpoint(List<Vector3> vertices, Dictionary<long, int> midpoints, int a, int b)
		{
			long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
			if (midpoints.TryGetValue(key, out int index))
				return index;
			index = vertices.Count;
			vertices.Add((vertices[a] + vertices[b]) * .5f);
			midpoints[key] = index;
			return index;
		}
	}
}
