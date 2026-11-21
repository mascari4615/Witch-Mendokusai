using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 궁극기 3D 연출의 칼과 벤 자리 메시 (밤의 날). 전부 코드 생성
	/// - 칼날과 자루는 재질이 달라 메시 둘 (칼날은 별밭 창, 자루는 빛 받는 금속)
	/// - 틈과 휘두름 자취는 납작한 판. 꼭짓점 알파가 가장자리 빛 세기 (별밭 창 재질의 _EdgeAmount)
	/// </summary>
	public static partial class UltimateMeshFactory
	{
		/// <summary>
		/// 외날 칼날. 날밑이 원점, 칼끝이 +Y, 길이 1. 날은 +X, 등은 -X, 납작한 면은 ±Z
		/// - 단면은 쐐기 (등 두 점, 날 한 점). 끝으로 갈수록 등 쪽으로 휘고 좁아져 한 점으로 모임
		/// </summary>
		public static Mesh Blade(int segments, float width, float thickness, float curve)
		{
			Builder builder = new() { InsidePoint = centroid => new Vector3(BladeCenter(centroid.y, width, curve), centroid.y, 0f) };
			int count = Mathf.Max(4, segments);
			Vector3[] back = new Vector3[count + 1];
			Vector3[] front = new Vector3[count + 1];
			Vector3[] edge = new Vector3[count + 1];
			for (int index = 0; index <= count; index++)
			{
				float along = index / (float)count;
				float taper = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1f, along));
				float spine = -width * 0.5f - curve * along * along;
				float half = thickness * 0.5f * Mathf.Lerp(0.25f, 1f, taper);
				back[index] = new Vector3(spine, along, -half);
				front[index] = new Vector3(spine, along, half);
				edge[index] = new Vector3(spine + width * taper, along, 0f);
			}
			Color tone = Color.white;
			for (int index = 0; index < count; index++)
			{
				builder.Quad(front[index], front[index + 1], edge[index + 1], edge[index], tone);
				builder.Quad(back[index], edge[index], edge[index + 1], back[index + 1], tone);
				builder.Quad(back[index], back[index + 1], front[index + 1], front[index], tone);
			}
			builder.Facet(back[0], front[0], edge[0], tone);
			return builder.Build("UltimateBlade");
		}

		private static float BladeCenter(float along, float width, float curve) => -width * 0.2f - curve * along * along;

		/// <summary> 날밑 (납작한 판) 과 자루 (기둥). 날밑이 원점, 자루는 -Y 로 gripLength. 꼭짓점 색 rgb 는 바탕 밝기, a 는 발광 </summary>
		public static Mesh Hilt(float guardWidth, float guardThickness, float gripLength, float gripWidth, Color guardTone, Color gripTone)
		{
			Builder builder = new();
			AddBox(builder, new Vector3(0f, -0.012f, 0f), new Vector3(guardWidth * 0.5f, 0.012f, guardThickness * 0.5f), guardTone);
			AddBox(builder, new Vector3(0f, -0.024f - gripLength * 0.5f, 0f), new Vector3(gripWidth * 0.5f, gripLength * 0.5f, gripWidth * 0.35f), gripTone);
			AddBox(builder, new Vector3(0f, -0.024f - gripLength - 0.01f, 0f), new Vector3(gripWidth * 0.65f, 0.012f, gripWidth * 0.5f), guardTone);
			return builder.Build("UltimateHilt");
		}

		/// <summary>
		/// 벤 자리 (틈). XY 평면의 방추형 판, 길이 1 (X -0.5~0.5), 가장 넓은 곳 폭 1 (Y -0.5~0.5). 폭은 물체 크기 Y 로 조절
		/// - 윤곽을 살짝 들쭉날쭉하게 (찢어진 자리). 꼭짓점 알파는 가운데 0, 가장자리 1
		/// </summary>
		public static Mesh Rift(int seed, int segments, float jagged)
		{
			System.Random random = new(seed);
			int count = Mathf.Max(8, segments);
			List<Vector3> vertices = new();
			List<Color> colors = new();
			List<int> triangles = new();
			for (int index = 0; index <= count; index++)
			{
				float along = index / (float)count;
				// 양 끝이 뾰족한 방추형
				// 끝점의 사인은 0 보다 작게 나올 수 있음 (음수의 거듭제곱은 NaN, 메시가 통째로 비게 됨)
				float half = 0.5f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(along * Mathf.PI)), 0.7f);
				float wobble = index == 0 || index == count ? 0f : ((float)random.NextDouble() - 0.5f) * jagged;
				float lean = index == 0 || index == count ? 0f : ((float)random.NextDouble() - 0.5f) * jagged * 0.6f;
				float positionX = along - 0.5f;
				vertices.Add(new Vector3(positionX, lean, 0f));
				vertices.Add(new Vector3(positionX, lean + half * (1f + wobble), 0f));
				vertices.Add(new Vector3(positionX, lean - half * (1f - wobble), 0f));
				colors.Add(new Color(1f, 1f, 1f, 0f));
				colors.Add(Color.white);
				colors.Add(Color.white);
			}
			for (int index = 0; index < count; index++)
			{
				int start = index * 3;
				int next = start + 3;
				AddStrip(triangles, start, start + 1, next, next + 1);
				AddStrip(triangles, start + 2, start, next + 2, next);
			}
			return FlatMesh("UltimateRift", vertices, colors, triangles);
		}

		/// <summary>
		/// 휘두름 자취. XY 평면의 부채꼴 띠, 중심 원점, 각도 0 (+Y) 에서 sweepDegrees 만큼 -Z 축 기준으로 (+X 쪽으로) 돎
		/// - 반지름 innerRatio~1. 꼭짓점 알파는 바깥 가장자리 1, 안쪽 0. 시작 쪽 (자취 꼬리) 으로 갈수록 옅어지게 rgb 를 낮춤
		/// </summary>
		public static Mesh SwingFan(int segments, float sweepDegrees, float innerRatio)
		{
			int count = Mathf.Max(6, segments);
			List<Vector3> vertices = new();
			List<Color> colors = new();
			List<int> triangles = new();
			for (int index = 0; index <= count; index++)
			{
				float along = index / (float)count;
				float angle = along * sweepDegrees * Mathf.Deg2Rad;
				Vector3 direction = new(Mathf.Sin(angle), Mathf.Cos(angle), 0f);
				vertices.Add(direction * innerRatio);
				vertices.Add(direction);
				colors.Add(new Color(along, along, along, 0f));
				colors.Add(new Color(along, along, along, 1f));
			}
			for (int index = 0; index < count; index++)
				AddStrip(triangles, index * 2, index * 2 + 1, index * 2 + 2, index * 2 + 3);
			return FlatMesh("UltimateSwingFan", vertices, colors, triangles);
		}

		private static void AddStrip(List<int> triangles, int lowerLeft, int upperLeft, int lowerRight, int upperRight)
		{
			triangles.Add(lowerLeft);
			triangles.Add(upperLeft);
			triangles.Add(lowerRight);
			triangles.Add(lowerRight);
			triangles.Add(upperLeft);
			triangles.Add(upperRight);
		}

		private static Mesh FlatMesh(string name, List<Vector3> vertices, List<Color> colors, List<int> triangles)
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
