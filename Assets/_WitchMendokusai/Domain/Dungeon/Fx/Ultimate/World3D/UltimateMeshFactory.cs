using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 궁극기 3D 연출의 주제 물체 메시를 코드로 생성. 외부 모델 없이 결정, 창, 카드, 달 반구, 바위
	/// - 꼭짓점 색: rgb 는 바탕색 배율, a 는 자체 발광 세기 (UltimateMeshGlow 의 _UseVertexColor 1 과 짝)
	/// - 면마다 꼭짓점을 따로 두는 각진 음영 (결정, 바위, 카드) 과 매끈한 음영 (달) 을 구분
	/// - 만든 메시는 부른 쪽이 정리 (연출 끝에 Destroy)
	/// </summary>
	public static class UltimateMeshFactory
	{
		private sealed class Builder
		{
			public readonly List<Vector3> Vertices = new();
			public readonly List<Color> Colors = new();
			public readonly List<int> Triangles = new();

			/// <summary> 있으면 삼각형 중심에서 이 점의 반대쪽이 바깥이 되게 감는 순서를 맞춤 (볼록한 물체용) </summary>
			public System.Func<Vector3, Vector3> InsidePoint;

			// 각진 삼각형 하나. 꼭짓점을 공유하지 않아 면마다 법선이 따로
			public void Facet(Vector3 first, Vector3 second, Vector3 third, Color color)
			{
				if (InsidePoint != null)
				{
					Vector3 centroid = (first + second + third) / 3f;
					Vector3 normal = Vector3.Cross(second - first, third - first);
					if (Vector3.Dot(normal, centroid - InsidePoint(centroid)) < 0f)
						(second, third) = (third, second);
				}
				int start = Vertices.Count;
				Vertices.Add(first);
				Vertices.Add(second);
				Vertices.Add(third);
				Colors.Add(color);
				Colors.Add(color);
				Colors.Add(color);
				Triangles.Add(start);
				Triangles.Add(start + 1);
				Triangles.Add(start + 2);
			}

			public void Quad(Vector3 first, Vector3 second, Vector3 third, Vector3 fourth, Color color)
			{
				Facet(first, second, third, color);
				Facet(first, third, fourth, color);
			}

			// 볼록 다각형을 가운데 점 부채꼴로. 점 순서는 바깥에서 볼 때 시계 방향
			public void Fan(Vector3 center, IReadOnlyList<Vector3> outline, Color color)
			{
				for (int index = 0; index < outline.Count; index++)
					Facet(center, outline[index], outline[(index + 1) % outline.Count], color);
			}

			public Mesh Build(string name)
			{
				Mesh mesh = new() { name = name };
				if (Vertices.Count > 65000)
					mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
				mesh.SetVertices(Vertices);
				mesh.SetColors(Colors);
				mesh.SetTriangles(Triangles, 0);
				mesh.RecalculateNormals();
				mesh.RecalculateBounds();
				return mesh;
			}
		}

		/// <summary>
		/// 뾰족한 결정 기둥. 바닥 중심 원점, 높이 1, 굵기 약 0.36
		/// - 바닥 고리, 어깨 고리, 꼭대기 한 점. 고리 점마다 반지름과 높이를 흔들어 면 크기가 제각각 (쿼터뷰에서 면 음영 차가 나게)
		/// - 면마다 바탕 밝기 (rgb) 와 발광 (a) 이 달라 같은 빛에서도 면이 갈림
		/// </summary>
		public static Mesh Crystal(int seed, int sides)
		{
			System.Random random = new(seed);
			float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
			Builder builder = new() { InsidePoint = centroid => new Vector3(0f, Mathf.Clamp(centroid.y, 0f, 0.6f), 0f) };
			int count = Mathf.Max(4, sides);
			Vector3[] bottom = new Vector3[count];
			Vector3[] shoulder = new Vector3[count];
			float twist = Range(0f, Mathf.PI);
			for (int index = 0; index < count; index++)
			{
				float angle = twist + (index + Range(-0.18f, 0.18f)) / count * Mathf.PI * 2f;
				float radius = Range(0.14f, 0.2f);
				bottom[index] = new Vector3(Mathf.Cos(angle) * radius, -0.08f, Mathf.Sin(angle) * radius);
				float shoulderRadius = radius * Range(0.8f, 1.05f);
				shoulder[index] = new Vector3(Mathf.Cos(angle) * shoulderRadius, Range(0.62f, 0.78f), Mathf.Sin(angle) * shoulderRadius);
			}
			Vector3 tip = new(Range(-0.03f, 0.03f), 1f, Range(-0.03f, 0.03f));
			for (int index = 0; index < count; index++)
			{
				int next = (index + 1) % count;
				Color side = new(Range(0.55f, 1f), Range(0.55f, 1f), Range(0.55f, 1f), Range(0.15f, 0.55f));
				side.g = side.b = side.r;
				builder.Quad(bottom[index], shoulder[index], shoulder[next], bottom[next], side);
				Color cap = new(Range(0.75f, 1.15f), 0f, 0f, Range(0.45f, 1f));
				cap.g = cap.b = cap.r;
				builder.Facet(shoulder[index], tip, shoulder[next], cap);
			}
			return builder.Build("UltimateCrystal");
		}

		/// <summary>
		/// 창. 칼끝이 원점, 자루가 +Y, 전체 길이 1
		/// - 칼날 (마름모 단면의 납작한 쌍뿔, 발광 a 1), 날밑 (작은 판), 자루 (8각 기둥, 발광 0)
		/// </summary>
		public static Mesh Spear(Color bladeTone, Color shaftTone, Color guardTone)
		{
			Builder builder = new() { InsidePoint = centroid => new Vector3(0f, Mathf.Clamp(centroid.y, 0.05f, 0.95f), 0f) };
			const float BLADE_LENGTH = 0.34f;
			const float BLADE_WIDTH = 0.075f;
			const float BLADE_THICK = 0.022f;
			Vector3 tip = Vector3.zero;
			Vector3 bladeMid = new(0f, BLADE_LENGTH * 0.62f, 0f);
			Vector3 bladeBase = new(0f, BLADE_LENGTH, 0f);
			Vector3[] ring =
			{
				bladeMid + new Vector3(BLADE_WIDTH, 0f, 0f),
				bladeMid + new Vector3(0f, 0f, BLADE_THICK),
				bladeMid + new Vector3(-BLADE_WIDTH, 0f, 0f),
				bladeMid + new Vector3(0f, 0f, -BLADE_THICK),
			};
			Color blade = bladeTone;
			blade.a = 1f;
			Color bladeEdge = bladeTone * 1.2f;
			bladeEdge.a = 0.7f;
			for (int index = 0; index < 4; index++)
			{
				int next = (index + 1) % 4;
				builder.Facet(tip, ring[next], ring[index], index % 2 == 0 ? blade : bladeEdge);
				builder.Facet(bladeBase, ring[index], ring[next], index % 2 == 0 ? bladeEdge : blade);
			}

			// 날밑: 칼날 아래 가로 막대
			Color guard = guardTone;
			guard.a = 0.35f;
			AddBox(builder, new Vector3(0f, BLADE_LENGTH + 0.01f, 0f), new Vector3(0.085f, 0.014f, 0.022f), guard);

			// 자루: 8각 기둥
			Color shaft = shaftTone;
			shaft.a = 0f;
			const int SIDES = 8;
			const float SHAFT_RADIUS = 0.012f;
			float shaftStart = BLADE_LENGTH + 0.02f;
			for (int index = 0; index < SIDES; index++)
			{
				float angle = index / (float)SIDES * Mathf.PI * 2f;
				float nextAngle = (index + 1) / (float)SIDES * Mathf.PI * 2f;
				Vector3 offset = new(Mathf.Cos(angle) * SHAFT_RADIUS, 0f, Mathf.Sin(angle) * SHAFT_RADIUS);
				Vector3 nextOffset = new(Mathf.Cos(nextAngle) * SHAFT_RADIUS, 0f, Mathf.Sin(nextAngle) * SHAFT_RADIUS);
				Vector3 low = new(0f, shaftStart, 0f);
				Vector3 high = new(0f, 1f, 0f);
				builder.Quad(low + offset, high + offset, high + nextOffset, low + nextOffset, shaft);
				builder.Facet(high, high + nextOffset, high + offset, shaft);
			}
			return builder.Build("UltimateSpear");
		}

		/// <summary>
		/// 두께 있는 카드. 가운데 원점, 앞면이 -Z (카메라를 볼 때 LookRotation(카메라 반대) 로 앞면이 보임), 가로 0.63, 세로 0.88, 두께 0.02
		/// - 둥근 모서리 판 위에 무늬 판을 0.0012 띄워 겹침 (텍스처 없음, 꼭짓점 색)
		/// - 앞면: 상아색 바탕, 가운데 큰 마름모, 네 귀퉁이 작은 마름모. 뒷면: 금 테두리, 짙은 바탕, 금 마름모 격자. 옆면 금
		/// - 금 부분은 발광 a 1
		/// </summary>
		public static Mesh Card(Color faceTone, Color pipTone, Color backTone, Color goldTone)
		{
			Builder builder = new();
			const float WIDTH = 0.63f;
			const float HEIGHT = 0.88f;
			const float HALF_THICK = 0.01f;
			const float LIFT = 0.0012f;
			Color gold = goldTone;
			gold.a = 1f;
			Color face = faceTone;
			face.a = 0f;
			Color pip = pipTone;
			pip.a = 0.15f;
			Color back = backTone;
			back.a = 0f;

			List<Vector3> outline = RoundedRect(WIDTH, HEIGHT, 0.05f, 5);
			// 앞면 (-Z) 과 뒷면 (+Z) 바탕. 앞면은 -Z 에서 볼 때 시계 방향
			List<Vector3> front = Offset(outline, -HALF_THICK);
			List<Vector3> rear = Offset(outline, HALF_THICK);
			rear.Reverse();
			builder.Fan(new Vector3(0f, 0f, -HALF_THICK), Reverse(front), face);
			builder.Fan(new Vector3(0f, 0f, HALF_THICK), Reverse(rear), gold);
			// 옆면
			for (int index = 0; index < outline.Count; index++)
			{
				int next = (index + 1) % outline.Count;
				Vector3 current = outline[index];
				Vector3 following = outline[next];
				builder.Quad(new Vector3(current.x, current.y, -HALF_THICK), new Vector3(following.x, following.y, -HALF_THICK), new Vector3(following.x, following.y, HALF_THICK), new Vector3(current.x, current.y, HALF_THICK), gold);
			}

			// 앞면 무늬: 얇은 금 테두리선, 가운데 큰 마름모, 귀퉁이 작은 마름모
			float frontZ = -HALF_THICK - LIFT;
			AddRingRect(builder, WIDTH * 0.86f, HEIGHT * 0.9f, 0.012f, frontZ, true, gold);
			AddDiamond(builder, new Vector3(0f, 0f, frontZ), 0.17f, 0.25f, true, pip);
			AddDiamond(builder, new Vector3(-WIDTH * 0.33f, HEIGHT * 0.37f, frontZ), 0.035f, 0.05f, true, pip);
			AddDiamond(builder, new Vector3(WIDTH * 0.33f, -HEIGHT * 0.37f, frontZ), 0.035f, 0.05f, true, pip);

			// 뒷면 무늬: 짙은 바탕 판, 안쪽 금 테두리, 금 마름모 격자
			float backZ = HALF_THICK + LIFT;
			List<Vector3> inner = Offset(RoundedRect(WIDTH * 0.84f, HEIGHT * 0.88f, 0.035f, 4), backZ);
			builder.Fan(new Vector3(0f, 0f, backZ), inner, back);
			float patternZ = backZ + LIFT;
			AddRingRect(builder, WIDTH * 0.72f, HEIGHT * 0.78f, 0.01f, patternZ, false, gold);
			for (int row = -2; row <= 2; row++)
			for (int column = -1; column <= 1; column++)
			{
				if ((row + column) % 2 != 0)
					continue;
				float size = row == 0 && column == 0 ? 0.075f : 0.035f;
				AddDiamond(builder, new Vector3(column * WIDTH * 0.2f, row * HEIGHT * 0.15f, patternZ), size, size * 1.4f, false, gold);
			}
			return builder.Build("UltimateCard");
		}

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

			// 단면 뚜껑: 테두리 고리 점을 복제해 법선을 따로. 가운데 점은 평면 위
			int capCenter = vertices.Count;
			vertices.Add(Vector3.zero);
			colors.Add(new Color(1f, 1f, 1f, 1f));
			int capStart = vertices.Count;
			for (int segment = 0; segment < segments; segment++)
			{
				Vector3 rim = vertices[RingIndex(rings, segment)];
				// 표면 요철이 테두리를 들쭉날쭉하게 해도 뚜껑은 평면에
				vertices.Add(rim - pole * Vector3.Dot(rim, pole));
				colors.Add(new Color(1f, 1f, 1f, 0.6f));
			}
			for (int segment = 0; segment < segments; segment++)
			{
				cap.Add(capCenter);
				cap.Add(capStart + segment);
				cap.Add(capStart + (segment + 1) % segments);
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
			normals[capCenter] = -pole;
			for (int segment = 0; segment < segments; segment++)
				normals[capStart + segment] = -pole;
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

		// ---- 도형 도우미 ----

		private static void AddBox(Builder builder, Vector3 center, Vector3 halfSize, Color color)
		{
			Vector3 Corner(int signX, int signY, int signZ) => center + Vector3.Scale(halfSize, new Vector3(signX, signY, signZ));
			builder.Quad(Corner(-1, -1, -1), Corner(-1, 1, -1), Corner(1, 1, -1), Corner(1, -1, -1), color);
			builder.Quad(Corner(1, -1, 1), Corner(1, 1, 1), Corner(-1, 1, 1), Corner(-1, -1, 1), color);
			builder.Quad(Corner(-1, -1, 1), Corner(-1, 1, 1), Corner(-1, 1, -1), Corner(-1, -1, -1), color);
			builder.Quad(Corner(1, -1, -1), Corner(1, 1, -1), Corner(1, 1, 1), Corner(1, -1, 1), color);
			builder.Quad(Corner(-1, 1, -1), Corner(-1, 1, 1), Corner(1, 1, 1), Corner(1, 1, -1), color);
			builder.Quad(Corner(-1, -1, 1), Corner(-1, -1, -1), Corner(1, -1, -1), Corner(1, -1, 1), color);
		}

		// 둥근 모서리 직사각형 외곽 (XY, 반시계)
		private static List<Vector3> RoundedRect(float width, float height, float radius, int cornerSteps)
		{
			List<Vector3> points = new();
			Vector2[] corners = { new(width * 0.5f - radius, height * 0.5f - radius), new(-width * 0.5f + radius, height * 0.5f - radius), new(-width * 0.5f + radius, -height * 0.5f + radius), new(width * 0.5f - radius, -height * 0.5f + radius) };
			for (int corner = 0; corner < 4; corner++)
			for (int step = 0; step <= cornerSteps; step++)
			{
				float angle = (corner * 90f + step * 90f / cornerSteps) * Mathf.Deg2Rad;
				points.Add(new Vector3(corners[corner].x + Mathf.Cos(angle) * radius, corners[corner].y + Mathf.Sin(angle) * radius, 0f));
			}
			return points;
		}

		private static List<Vector3> Offset(List<Vector3> points, float depth)
		{
			List<Vector3> result = new(points.Count);
			foreach (Vector3 point in points)
				result.Add(new Vector3(point.x, point.y, depth));
			return result;
		}

		private static List<Vector3> Reverse(List<Vector3> points)
		{
			List<Vector3> result = new(points);
			result.Reverse();
			return result;
		}

		// 마름모. facingNegativeZ 면 -Z 에서 보이게 감음
		private static void AddDiamond(Builder builder, Vector3 center, float halfWidth, float halfHeight, bool facingNegativeZ, Color color)
		{
			List<Vector3> outline = new()
			{
				center + new Vector3(halfWidth, 0f, 0f),
				center + new Vector3(0f, halfHeight, 0f),
				center + new Vector3(-halfWidth, 0f, 0f),
				center + new Vector3(0f, -halfHeight, 0f),
			};
			builder.Fan(center, facingNegativeZ ? Reverse(outline) : outline, color);
		}

		// 직사각형 테두리선 (네 변의 얇은 띠)
		private static void AddRingRect(Builder builder, float width, float height, float thickness, float depth, bool facingNegativeZ, Color color)
		{
			float halfWidth = width * 0.5f;
			float halfHeight = height * 0.5f;
			void Band(float left, float bottom, float right, float top)
			{
				Vector3 lowerLeft = new(left, bottom, depth);
				Vector3 upperLeft = new(left, top, depth);
				Vector3 upperRight = new(right, top, depth);
				Vector3 lowerRight = new(right, bottom, depth);
				if (facingNegativeZ)
					builder.Quad(lowerLeft, upperLeft, upperRight, lowerRight, color);
				else
					builder.Quad(lowerRight, upperRight, upperLeft, lowerLeft, color);
			}
			Band(-halfWidth, halfHeight - thickness, halfWidth, halfHeight);
			Band(-halfWidth, -halfHeight, halfWidth, -halfHeight + thickness);
			Band(-halfWidth, -halfHeight, -halfWidth + thickness, halfHeight);
			Band(halfWidth - thickness, -halfHeight, halfWidth, halfHeight);
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
