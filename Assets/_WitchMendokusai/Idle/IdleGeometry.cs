using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 적이 쓰는 기하 도형 (visual.md 2). 구역이 깊을수록 면 수 증가
	///
	/// ★ 정다면체는 다섯뿐. 그 위는 세분으로 구에 수렴 (refs/geometric-enemies.md 3)
	/// ★ 면마다 정점 분리. 공유하면 면이 둥글게 뭉개져 도형이 안 읽힘
	/// ★ 정12면체는 정20면체의 쌍대. 면 목록을 손으로 적으면 틀린 곳 추적 불가
	/// </summary>
	public static class IdleGeometry
	{
		/// <summary>도형 계단. 순서가 곧 구역 깊이</summary>
		public enum Shape
		{
			Tetrahedron = 0,
			Cube = 1,
			Octahedron = 2,
			Dodecahedron = 3,
			Icosahedron = 4,
			SphereOnce = 5,
			SphereTwice = 6,
		}

		/// <summary>계단 수. 이 위로는 면을 안 늘린다 (60면 위는 눈이 구분 못 함)</summary>
		public const int SHAPE_COUNT = 7;

		/// <summary>이 구역의 도형. <paramref name="stagesPerStep"/> 구역마다 한 계단</summary>
		public static Shape ShapeOfStage(int stage, int stagesPerStep)
		{
			if (stagesPerStep < 1)
			{
				stagesPerStep = 1;
			}

			int step = (stage - 1) / stagesPerStep;
			if (step < 0)
			{
				step = 0;
			}

			return (Shape)(step >= SHAPE_COUNT ? SHAPE_COUNT - 1 : step);
		}

		/// <summary>사람이 읽는 이름. 툴팁과 로그용</summary>
		public static string NameOf(Shape shape)
		{
			switch (shape)
			{
				case Shape.Tetrahedron: return "정사면체";
				case Shape.Cube: return "정육면체";
				case Shape.Octahedron: return "정팔면체";
				case Shape.Dodecahedron: return "정12면체";
				case Shape.Icosahedron: return "정20면체";
				case Shape.SphereOnce: return "세분 구";
				default: return "잔 세분 구";
			}
		}

		/// <summary>도형 하나. <paramref name="radius"/> 는 중심에서 꼭짓점까지</summary>
		public static Mesh Build(Shape shape, float radius)
		{
			List<Vector3> points = new List<Vector3>();
			List<int[]> faces = new List<int[]>();

			switch (shape)
			{
				case Shape.Tetrahedron:
					Tetrahedron(points, faces);
					break;
				case Shape.Cube:
					Cube(points, faces);
					break;
				case Shape.Octahedron:
					Octahedron(points, faces);
					break;
				case Shape.Dodecahedron:
					Dodecahedron(points, faces);
					break;
				case Shape.Icosahedron:
					Icosahedron(points, faces);
					break;
				case Shape.SphereOnce:
					Icosahedron(points, faces);
					Subdivide(points, faces, 1);
					break;
				default:
					Icosahedron(points, faces);
					Subdivide(points, faces, 2);
					break;
			}

			return Flat(points, faces, radius, NameOf(shape));
		}

		/// <summary>
		/// 별 만들기. 면마다 바깥으로 뿔을 세운다 (visual.md 4, 깊은 구역 보스)
		///
		/// <paramref name="spike"/> 는 반지름 대비 뿔 길이. 0.4 면 반지름의 40%
		/// </summary>
		public static Mesh Stellate(Shape shape, float radius, float spike)
		{
			List<Vector3> points = new List<Vector3>();
			List<int[]> faces = new List<int[]>();
			BasePoints(shape, points, faces);

			List<Vector3> made = new List<Vector3>();
			List<int> triangles = new List<int>();

			foreach (int[] face in faces)
			{
				Vector3 middle = Vector3.zero;
				for (int at = 0; at < face.Length; at++)
				{
					middle += points[face[at]];
				}

				middle /= face.Length;
				Vector3 tip = middle.normalized * (1f + spike);

				for (int at = 0; at < face.Length; at++)
				{
					Vector3 one = points[face[at]].normalized;
					Vector3 next = points[face[(at + 1) % face.Length]].normalized;
					int start = made.Count;
					made.Add(one * radius);
					made.Add(next * radius);
					made.Add(tip * radius);
					triangles.Add(start);
					triangles.Add(start + 1);
					triangles.Add(start + 2);
				}
			}

			Mesh mesh = new Mesh();
			mesh.name = "Stellated" + NameOf(shape);
			mesh.hideFlags = HideFlags.DontSave;
			mesh.SetVertices(made);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			return mesh;
		}

		/// <summary>
		/// 껍질 조각 하나 (visual.md 4). 도형의 면 하나를 뜯어낸 얇은 판
		///
		/// <paramref name="which"/> 는 몇 번째 면인지. 면 수보다 크면 순환
		/// </summary>
		public static Mesh FaceShard(Shape shape, float radius, int which, float thickness)
		{
			List<Vector3> points = new List<Vector3>();
			List<int[]> faces = new List<int[]>();
			BasePoints(shape, points, faces);

			int[] face = faces[((which % faces.Count) + faces.Count) % faces.Count];
			Vector3 center = Vector3.zero;
			for (int at = 0; at < face.Length; at++)
			{
				center += points[face[at]];
			}

			center = (center / face.Length).normalized;

			// 조각을 원점 기준으로 눕히기. 자리와 회전은 무대 소관
			Quaternion lay = Quaternion.FromToRotation(center, Vector3.up);
			List<Vector3> rim = new List<Vector3>();
			for (int at = 0; at < face.Length; at++)
			{
				Vector3 corner = lay * (points[face[at]].normalized * radius);
				rim.Add(new Vector3(corner.x, 0f, corner.z));
			}

			List<Vector3> made = new List<Vector3>();
			List<int> triangles = new List<int>();
			Vector3 up = new Vector3(0f, thickness * 0.5f, 0f);

			for (int lid = 0; lid < 2; lid++)
			{
				Vector3 lift = lid == 0 ? up : -up;
				for (int at = 1; at + 1 < rim.Count; at++)
				{
					int start = made.Count;
					made.Add(rim[0] + lift);
					made.Add(rim[lid == 0 ? at : at + 1] + lift);
					made.Add(rim[lid == 0 ? at + 1 : at] + lift);
					triangles.Add(start);
					triangles.Add(start + 1);
					triangles.Add(start + 2);
				}
			}

			for (int at = 0; at < rim.Count; at++)
			{
				Vector3 one = rim[at];
				Vector3 next = rim[(at + 1) % rim.Count];
				int start = made.Count;
				made.Add(one + up);
				made.Add(next + up);
				made.Add(next - up);
				made.Add(one - up);
				triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
				triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
			}

			Mesh mesh = new Mesh();
			mesh.name = "Shard" + which;
			mesh.hideFlags = HideFlags.DontSave;
			mesh.SetVertices(made);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			return mesh;
		}

		/// <summary>이 도형의 면 수. 보스가 조각을 몇 개 띄울지 정할 때</summary>
		public static int FaceCountOf(Shape shape)
		{
			List<Vector3> points = new List<Vector3>();
			List<int[]> faces = new List<int[]>();
			BasePoints(shape, points, faces);
			return faces.Count;
		}

		private static void BasePoints(Shape shape, List<Vector3> points, List<int[]> faces)
		{
			switch (shape)
			{
				case Shape.Tetrahedron: Tetrahedron(points, faces); break;
				case Shape.Cube: Cube(points, faces); break;
				case Shape.Octahedron: Octahedron(points, faces); break;
				case Shape.Dodecahedron: Dodecahedron(points, faces); break;
				default: Icosahedron(points, faces); break;
			}
		}

		private static void Tetrahedron(List<Vector3> points, List<int[]> faces)
		{
			points.Add(new Vector3(1f, 1f, 1f));
			points.Add(new Vector3(1f, -1f, -1f));
			points.Add(new Vector3(-1f, 1f, -1f));
			points.Add(new Vector3(-1f, -1f, 1f));

			faces.Add(new[] { 0, 2, 1 });
			faces.Add(new[] { 0, 1, 3 });
			faces.Add(new[] { 0, 3, 2 });
			faces.Add(new[] { 1, 2, 3 });
		}

		private static void Cube(List<Vector3> points, List<int[]> faces)
		{
			for (int at = 0; at < 8; at++)
			{
				points.Add(new Vector3(
					(at & 1) == 0 ? -1f : 1f,
					(at & 2) == 0 ? -1f : 1f,
					(at & 4) == 0 ? -1f : 1f));
			}

			faces.Add(new[] { 0, 2, 3, 1 });
			faces.Add(new[] { 4, 5, 7, 6 });
			faces.Add(new[] { 0, 1, 5, 4 });
			faces.Add(new[] { 2, 6, 7, 3 });
			faces.Add(new[] { 0, 4, 6, 2 });
			faces.Add(new[] { 1, 3, 7, 5 });
		}

		private static void Octahedron(List<Vector3> points, List<int[]> faces)
		{
			points.Add(new Vector3(1f, 0f, 0f));
			points.Add(new Vector3(-1f, 0f, 0f));
			points.Add(new Vector3(0f, 1f, 0f));
			points.Add(new Vector3(0f, -1f, 0f));
			points.Add(new Vector3(0f, 0f, 1f));
			points.Add(new Vector3(0f, 0f, -1f));

			faces.Add(new[] { 0, 2, 4 });
			faces.Add(new[] { 2, 1, 4 });
			faces.Add(new[] { 1, 3, 4 });
			faces.Add(new[] { 3, 0, 4 });
			faces.Add(new[] { 2, 0, 5 });
			faces.Add(new[] { 1, 2, 5 });
			faces.Add(new[] { 3, 1, 5 });
			faces.Add(new[] { 0, 3, 5 });
		}

		private static void Icosahedron(List<Vector3> points, List<int[]> faces)
		{
			float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;

			points.Add(new Vector3(-1f, phi, 0f));
			points.Add(new Vector3(1f, phi, 0f));
			points.Add(new Vector3(-1f, -phi, 0f));
			points.Add(new Vector3(1f, -phi, 0f));
			points.Add(new Vector3(0f, -1f, phi));
			points.Add(new Vector3(0f, 1f, phi));
			points.Add(new Vector3(0f, -1f, -phi));
			points.Add(new Vector3(0f, 1f, -phi));
			points.Add(new Vector3(phi, 0f, -1f));
			points.Add(new Vector3(phi, 0f, 1f));
			points.Add(new Vector3(-phi, 0f, -1f));
			points.Add(new Vector3(-phi, 0f, 1f));

			int[][] listed =
			{
				new[] { 0, 11, 5 }, new[] { 0, 5, 1 }, new[] { 0, 1, 7 }, new[] { 0, 7, 10 }, new[] { 0, 10, 11 },
				new[] { 1, 5, 9 }, new[] { 5, 11, 4 }, new[] { 11, 10, 2 }, new[] { 10, 7, 6 }, new[] { 7, 1, 8 },
				new[] { 3, 9, 4 }, new[] { 3, 4, 2 }, new[] { 3, 2, 6 }, new[] { 3, 6, 8 }, new[] { 3, 8, 9 },
				new[] { 4, 9, 5 }, new[] { 2, 4, 11 }, new[] { 6, 2, 10 }, new[] { 8, 6, 7 }, new[] { 9, 8, 1 },
			};

			foreach (int[] face in listed)
			{
				faces.Add(face);
			}
		}

		/// <summary>
		/// 정12면체. 정20면체의 쌍대
		///
		/// ★ 면 20개의 중심이 정12면체의 정점 20개. 정20면체 정점 하나에 모이는 면 다섯이 오각형 하나
		/// </summary>
		private static void Dodecahedron(List<Vector3> points, List<int[]> faces)
		{
			List<Vector3> seedPoints = new List<Vector3>();
			List<int[]> seedFaces = new List<int[]>();
			Icosahedron(seedPoints, seedFaces);

			foreach (int[] face in seedFaces)
			{
				Vector3 middle = (seedPoints[face[0]] + seedPoints[face[1]] + seedPoints[face[2]]) / 3f;
				points.Add(middle.normalized);
			}

			for (int corner = 0; corner < seedPoints.Count; corner++)
			{
				List<int> around = new List<int>();
				for (int at = 0; at < seedFaces.Count; at++)
				{
					int[] face = seedFaces[at];
					if (face[0] == corner || face[1] == corner || face[2] == corner)
					{
						around.Add(at);
					}
				}

				if (around.Count != 5)
				{
					continue;
				}

				faces.Add(SortAround(points, around, seedPoints[corner].normalized));
			}
		}

		/// <summary>축을 중심으로 각도 순 정렬. 오각형 정점이 뒤섞이면 면이 꼬인다</summary>
		private static int[] SortAround(List<Vector3> points, List<int> around, Vector3 axis)
		{
			Vector3 first = Vector3.ProjectOnPlane(points[around[0]], axis).normalized;
			Vector3 side = Vector3.Cross(axis, first).normalized;

			around.Sort((one, other) =>
			{
				float oneAngle = Angle(points[one], axis, first, side);
				float otherAngle = Angle(points[other], axis, first, side);
				return oneAngle.CompareTo(otherAngle);
			});

			return around.ToArray();
		}

		private static float Angle(Vector3 point, Vector3 axis, Vector3 first, Vector3 side)
		{
			Vector3 flat = Vector3.ProjectOnPlane(point, axis).normalized;
			return Mathf.Atan2(Vector3.Dot(flat, side), Vector3.Dot(flat, first));
		}

		/// <summary>삼각형을 넷으로 쪼갠다. 20 -> 80 -> 320. 구에 수렴</summary>
		private static void Subdivide(List<Vector3> points, List<int[]> faces, int times)
		{
			for (int round = 0; round < times; round++)
			{
				List<int[]> made = new List<int[]>();
				Dictionary<long, int> middles = new Dictionary<long, int>();

				foreach (int[] face in faces)
				{
					int a = face[0];
					int b = face[1];
					int c = face[2];
					int ab = Middle(points, middles, a, b);
					int bc = Middle(points, middles, b, c);
					int ca = Middle(points, middles, c, a);

					made.Add(new[] { a, ab, ca });
					made.Add(new[] { b, bc, ab });
					made.Add(new[] { c, ca, bc });
					made.Add(new[] { ab, bc, ca });
				}

				faces.Clear();
				faces.AddRange(made);
			}
		}

		private static int Middle(List<Vector3> points, Dictionary<long, int> middles, int one, int other)
		{
			long key = one < other ? (long)one << 32 | (uint)other : (long)other << 32 | (uint)one;
			if (middles.TryGetValue(key, out int found))
			{
				return found;
			}

			points.Add(((points[one] + points[other]) * 0.5f).normalized);
			int made = points.Count - 1;
			middles[key] = made;
			return made;
		}

		/// <summary>면마다 정점을 따로 둔 메시. 공유하면 모서리가 뭉개진다</summary>
		private static Mesh Flat(List<Vector3> points, List<int[]> faces, float radius, string name)
		{
			List<Vector3> made = new List<Vector3>();
			List<int> triangles = new List<int>();

			foreach (int[] face in faces)
			{
				int start = made.Count;
				for (int at = 0; at < face.Length; at++)
				{
					made.Add(points[face[at]].normalized * radius);
				}

				for (int at = 1; at + 1 < face.Length; at++)
				{
					triangles.Add(start);
					triangles.Add(start + at);
					triangles.Add(start + at + 1);
				}
			}

			Mesh mesh = new Mesh();
			mesh.name = name;
			mesh.hideFlags = HideFlags.DontSave;
			mesh.SetVertices(made);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			return mesh;
		}
	}
}
