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
	public static partial class UltimateMeshFactory
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
	}
}
