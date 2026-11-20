using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary> 코드 생성 메시 중 구 계열 (달 반쪽, 바위, 원기둥) 과 정이십면체 도우미. 공통 설명은 UltimateMeshFactory.cs </summary>
	public static partial class UltimateMeshFactory
	{
		/// <summary>
		/// 달 반쪽. 반지름 1 구를 cutNormal 평면으로 자른 한쪽 (cutNormal 쪽이 positiveSide) 과 단면 뚜껑
		/// - 표면: 운석 구덩이 (테두리가 솟은 그릇) 를 실제로 파고, 어두운 바다 무늬를 꼭짓점 색으로. 같은 seed 면 두 반쪽이 이음매에서 맞음
		/// - 단면 뚜껑은 하위 메시 1 (단면 빛 재질), 표면은 하위 메시 0
		/// </summary>
		public static Mesh MoonHalf(int seed, Vector3 cutNormal, bool positiveSide, int rings, int segments, float craterDepth)
		{
			MoonSurface surface = new(seed, craterDepth);
			Vector3 pole = (positiveSide ? cutNormal : -cutNormal).normalized;
			Vector3 tangent = Vector3.Cross(pole, Mathf.Abs(pole.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
			Vector3 bitangent = Vector3.Cross(pole, tangent);
			List<Vector3> vertices = new();
			List<Color> colors = new();
			List<int> shell = new();
			List<int> cap = new();

			// 극점 하나, 그 아래 고리들. 마지막 고리가 단면 테두리 (극에서 90도)
			vertices.Add(pole * surface.Radius(pole));
			colors.Add(surface.Tone(pole));
			for (int ring = 1; ring <= rings; ring++)
			{
				float theta = ring / (float)rings * Mathf.PI * 0.5f;
				for (int segment = 0; segment < segments; segment++)
				{
					float phi = segment / (float)segments * Mathf.PI * 2f;
					Vector3 direction = pole * Mathf.Cos(theta) + (tangent * Mathf.Cos(phi) + bitangent * Mathf.Sin(phi)) * Mathf.Sin(theta);
					vertices.Add(direction * surface.Radius(direction));
					colors.Add(surface.Tone(direction));
				}
			}
			int RingIndex(int ring, int segment) => 1 + (ring - 1) * segments + (segment % segments);
			for (int segment = 0; segment < segments; segment++)
			{
				shell.Add(0);
				shell.Add(RingIndex(1, segment + 1));
				shell.Add(RingIndex(1, segment));
			}
			for (int ring = 1; ring < rings; ring++)
			for (int segment = 0; segment < segments; segment++)
			{
				int upperLeft = RingIndex(ring, segment);
				int upperRight = RingIndex(ring, segment + 1);
				int lowerLeft = RingIndex(ring + 1, segment);
				int lowerRight = RingIndex(ring + 1, segment + 1);
				shell.Add(upperLeft);
				shell.Add(upperRight);
				shell.Add(lowerLeft);
				shell.Add(upperRight);
				shell.Add(lowerRight);
				shell.Add(lowerLeft);
			}

			// 단면 뚜껑: 테두리 고리 점을 복제해 법선을 따로. 가운데가 가장 밝고 바깥으로 어두워지는 고리 여럿, 고리마다 밝기 얼룩 (평평한 흰 판으로 읽히지 않게)
			const int CAP_RINGS = 6;
			System.Random capRandom = new(seed * 7 + (positiveSide ? 1 : 2));
			int capCenter = vertices.Count;
			vertices.Add(Vector3.zero);
			colors.Add(new Color(1f, 1f, 1f, 1f));
			int capStart = vertices.Count;
			for (int capRing = 1; capRing <= CAP_RINGS; capRing++)
			{
				float fraction = capRing / (float)CAP_RINGS;
				for (int segment = 0; segment < segments; segment++)
				{
					Vector3 rim = vertices[RingIndex(rings, segment)];
					// 표면 요철이 테두리를 들쭉날쭉하게 해도 뚜껑은 평면에
					Vector3 flat = rim - pole * Vector3.Dot(rim, pole);
					vertices.Add(flat * fraction);
					float glow = Mathf.Pow(1f - fraction, 1.4f) * (0.6f + 0.4f * (float)capRandom.NextDouble()) + 0.12f;
					float shade = Mathf.Lerp(1f, 0.55f, fraction);
					colors.Add(new Color(shade, shade, Mathf.Min(1f, shade * 1.15f), glow));
				}
			}
			int CapIndex(int capRing, int segment) => capStart + (capRing - 1) * segments + (segment % segments);
			for (int segment = 0; segment < segments; segment++)
			{
				cap.Add(capCenter);
				cap.Add(CapIndex(1, segment));
				cap.Add(CapIndex(1, segment + 1));
			}
			for (int capRing = 1; capRing < CAP_RINGS; capRing++)
			for (int segment = 0; segment < segments; segment++)
			{
				cap.Add(CapIndex(capRing, segment));
				cap.Add(CapIndex(capRing + 1, segment));
				cap.Add(CapIndex(capRing, segment + 1));
				cap.Add(CapIndex(capRing, segment + 1));
				cap.Add(CapIndex(capRing + 1, segment));
				cap.Add(CapIndex(capRing + 1, segment + 1));
			}

			OrientTriangles(vertices, shell, centroid => centroid);
			OrientTriangles(vertices, cap, _ => -pole);

			Mesh mesh = new() { name = "UltimateMoonHalf", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, subMeshCount = 2 };
			mesh.SetVertices(vertices);
			mesh.SetColors(colors);
			mesh.SetTriangles(shell, 0);
			mesh.SetTriangles(cap, 1);
			mesh.RecalculateNormals();
			// 뚜껑 법선은 평면 바깥쪽 (반쪽의 반대 방향) 으로 고정
			Vector3[] normals = mesh.normals;
			for (int index = capCenter; index < normals.Length; index++)
				normals[index] = -pole;
			mesh.normals = normals;
			mesh.RecalculateBounds();
			return mesh;
		}

		/// <summary> 각진 바위 덩어리. 가운데 원점, 지름 약 1. 정이십면체를 한 번 나누고 점마다 반지름을 흔듦 </summary>
		public static Mesh Rock(int seed, Color tone, float roughness)
		{
			System.Random random = new(seed);
			List<Vector3> points = new();
			List<int[]> faces = new();
			Icosahedron(points, faces);
			Subdivide(points, faces);
			float[] radii = new float[points.Count];
			for (int index = 0; index < points.Count; index++)
				radii[index] = 0.5f * (1f - roughness * 0.5f + (float)random.NextDouble() * roughness);
			Builder builder = new() { InsidePoint = _ => Vector3.zero };
			foreach (int[] face in faces)
			{
				float shade = 0.75f + (float)random.NextDouble() * 0.35f;
				Color color = new(tone.r * shade, tone.g * shade, tone.b * shade, 0f);
				builder.Facet(points[face[0]] * radii[face[0]], points[face[1]] * radii[face[1]], points[face[2]] * radii[face[2]], color);
			}
			return builder.Build("UltimateRock");
		}

		/// <summary> 뚜껑 없는 원기둥 (빛줄기). 바닥 중심 원점, 높이 1, 반지름 0.5. UV v 가 높이, 꼭짓점 알파는 바닥 bottomAlpha 에서 꼭대기 topAlpha </summary>
		public static Mesh OpenCylinder(int segments, float bottomAlpha, float topAlpha)
		{
			List<Vector3> vertices = new();
			List<Color> colors = new();
			List<Vector2> uvs = new();
			List<int> triangles = new();
			for (int segment = 0; segment <= segments; segment++)
			{
				float angle = segment / (float)segments * Mathf.PI * 2f;
				Vector3 offset = new(Mathf.Cos(angle) * 0.5f, 0f, Mathf.Sin(angle) * 0.5f);
				vertices.Add(offset);
				vertices.Add(offset + Vector3.up);
				colors.Add(new Color(1f, 1f, 1f, bottomAlpha));
				colors.Add(new Color(1f, 1f, 1f, topAlpha));
				uvs.Add(new Vector2(segment / (float)segments, 0f));
				uvs.Add(new Vector2(segment / (float)segments, 1f));
			}
			for (int segment = 0; segment < segments; segment++)
			{
				int start = segment * 2;
				triangles.Add(start);
				triangles.Add(start + 1);
				triangles.Add(start + 2);
				triangles.Add(start + 1);
				triangles.Add(start + 3);
				triangles.Add(start + 2);
			}
			Mesh mesh = new() { name = "UltimateOpenCylinder" };
			mesh.SetVertices(vertices);
			mesh.SetColors(colors);
			mesh.SetUVs(0, uvs);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			return mesh;
		}

		// 삼각형마다 법선이 outward(중심) 쪽을 보게 감는 순서를 맞춤
		private static void OrientTriangles(List<Vector3> vertices, List<int> triangles, System.Func<Vector3, Vector3> outward)
		{
			for (int index = 0; index + 2 < triangles.Count; index += 3)
			{
				Vector3 first = vertices[triangles[index]];
				Vector3 second = vertices[triangles[index + 1]];
				Vector3 third = vertices[triangles[index + 2]];
				Vector3 normal = Vector3.Cross(second - first, third - first);
				if (Vector3.Dot(normal, outward((first + second + third) / 3f)) < 0f)
					(triangles[index + 1], triangles[index + 2]) = (triangles[index + 2], triangles[index + 1]);
			}
		}

		// ---- 달 표면 ----

		private sealed class MoonSurface
		{
			private readonly Vector3[] craterCenters;
			private readonly float[] craterRadii;
			private readonly Vector3[] mareCenters;
			private readonly float[] mareRadii;
			private readonly float depth;

			public MoonSurface(int seed, float depth)
			{
				this.depth = depth;
				System.Random random = new(seed);
				Vector3 RandomDirection()
				{
					Vector3 direction = new((float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f);
					return direction.sqrMagnitude < 0.0001f ? Vector3.up : direction.normalized;
				}
				const int CRATERS = 46;
				craterCenters = new Vector3[CRATERS];
				craterRadii = new float[CRATERS];
				for (int index = 0; index < CRATERS; index++)
				{
					craterCenters[index] = RandomDirection();
					// 큰 구덩이 몇 개와 작은 구덩이 다수
					float pick = (float)random.NextDouble();
					craterRadii[index] = index < 6 ? 0.22f + pick * 0.14f : 0.05f + pick * pick * 0.12f;
				}
				const int MARIA = 7;
				mareCenters = new Vector3[MARIA];
				mareRadii = new float[MARIA];
				for (int index = 0; index < MARIA; index++)
				{
					mareCenters[index] = RandomDirection();
					mareRadii[index] = 0.35f + (float)random.NextDouble() * 0.4f;
				}
			}

			public float Radius(Vector3 direction)
			{
				float height = 0f;
				for (int index = 0; index < craterCenters.Length; index++)
				{
					float distance = Mathf.Acos(Mathf.Clamp(Vector3.Dot(direction, craterCenters[index]), -1f, 1f)) / craterRadii[index];
					if (distance > 1.6f)
						continue;
					// 그릇 (안쪽 오목) 과 솟은 테두리. 큰 구덩이일수록 깊게
					float bowl = distance < 1f ? -(1f - distance * distance) : 0f;
					float rim = Mathf.Exp(-Mathf.Pow((distance - 1f) / 0.22f, 2f)) * 0.35f;
					height += (bowl + rim) * craterRadii[index];
				}
				return 1f + height * depth;
			}

			public Color Tone(Vector3 direction)
			{
				float mare = 0f;
				for (int index = 0; index < mareCenters.Length; index++)
				{
					float distance = Mathf.Acos(Mathf.Clamp(Vector3.Dot(direction, mareCenters[index]), -1f, 1f)) / mareRadii[index];
					mare = Mathf.Max(mare, Mathf.SmoothStep(1f, 0f, distance));
				}
				// 구덩이 바닥은 약간 밝고 (분출물), 바다는 어두움
				float bright = 0f;
				for (int index = 0; index < craterCenters.Length; index++)
				{
					float distance = Mathf.Acos(Mathf.Clamp(Vector3.Dot(direction, craterCenters[index]), -1f, 1f)) / craterRadii[index];
					bright = Mathf.Max(bright, Mathf.Clamp01(1.4f - distance) * 0.25f);
				}
				float value = Mathf.Lerp(0.92f, 0.42f, mare) + bright;
				return new Color(value, value, value * 1.03f, 0f);
			}
		}

		private static void Icosahedron(List<Vector3> points, List<int[]> faces)
		{
			float golden = (1f + Mathf.Sqrt(5f)) * 0.5f;
			Vector3[] raw =
			{
				new(-1, golden, 0), new(1, golden, 0), new(-1, -golden, 0), new(1, -golden, 0),
				new(0, -1, golden), new(0, 1, golden), new(0, -1, -golden), new(0, 1, -golden),
				new(golden, 0, -1), new(golden, 0, 1), new(-golden, 0, -1), new(-golden, 0, 1),
			};
			foreach (Vector3 point in raw)
				points.Add(point.normalized);
			int[,] table =
			{
				{ 0, 11, 5 }, { 0, 5, 1 }, { 0, 1, 7 }, { 0, 7, 10 }, { 0, 10, 11 },
				{ 1, 5, 9 }, { 5, 11, 4 }, { 11, 10, 2 }, { 10, 7, 6 }, { 7, 1, 8 },
				{ 3, 9, 4 }, { 3, 4, 2 }, { 3, 2, 6 }, { 3, 6, 8 }, { 3, 8, 9 },
				{ 4, 9, 5 }, { 2, 4, 11 }, { 6, 2, 10 }, { 8, 6, 7 }, { 9, 8, 1 },
			};
			for (int index = 0; index < table.GetLength(0); index++)
				faces.Add(new[] { table[index, 0], table[index, 1], table[index, 2] });
		}

		private static void Subdivide(List<Vector3> points, List<int[]> faces)
		{
			Dictionary<long, int> middles = new();
			int Middle(int first, int second)
			{
				long key = first < second ? ((long)first << 32) + second : ((long)second << 32) + first;
				if (middles.TryGetValue(key, out int found))
					return found;
				points.Add(((points[first] + points[second]) * 0.5f).normalized);
				middles[key] = points.Count - 1;
				return points.Count - 1;
			}
			List<int[]> next = new();
			foreach (int[] face in faces)
			{
				int ab = Middle(face[0], face[1]);
				int bc = Middle(face[1], face[2]);
				int ca = Middle(face[2], face[0]);
				next.Add(new[] { face[0], ab, ca });
				next.Add(new[] { face[1], bc, ab });
				next.Add(new[] { face[2], ca, bc });
				next.Add(new[] { ab, bc, ca });
			}
			faces.Clear();
			faces.AddRange(next);
		}
	}
}
