using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 궁극기 3D 배치 2 (번개, 화염, 태양 광선, 블랙홀) 의 매끈한 기본 형태. 모두 UV 와 꼭짓점 색 (a 는 투명도) 을 가짐
	/// - 구 (태양 구, 블랙홀 핵), 원환 (경로 고리, 광자 고리), 고리판 (강착 원반), 원뿔 (불길 뼈대), 꺾인 관 (번개 줄기)
	/// - 관은 같은 메시를 다시 채워 모양을 바꿈 (번개 깜빡임). 나머지는 한 번 만들고 부른 쪽이 정리
	/// </summary>
	public static partial class UltimateMeshFactory
	{
		/// <summary> 반지름 0.5 UV 구. u 는 경도, v 는 위도 </summary>
		public static Mesh Sphere(int rings, int segments)
		{
			List<Vector3> vertices = new();
			List<Vector2> uvs = new();
			List<int> triangles = new();
			for (int ring = 0; ring <= rings; ring++)
			{
				float latitude = Mathf.PI * ring / rings;
				for (int segment = 0; segment <= segments; segment++)
				{
					float longitude = Mathf.PI * 2f * segment / segments;
					vertices.Add(new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude)) * 0.5f);
					uvs.Add(new Vector2(segment / (float)segments, 1f - ring / (float)rings));
				}
			}
			int stride = segments + 1;
			for (int ring = 0; ring < rings; ring++)
			for (int segment = 0; segment < segments; segment++)
			{
				int corner = ring * stride + segment;
				triangles.Add(corner);
				triangles.Add(corner + 1);
				triangles.Add(corner + stride);
				triangles.Add(corner + 1);
				triangles.Add(corner + stride + 1);
				triangles.Add(corner + stride);
			}
			return Smooth("UltimateSphere", vertices, uvs, White(vertices.Count), triangles);
		}

		/// <summary> XZ 평면에 누운 원환. 큰 반지름 1, 굵기 반지름 tubeRatio. u 는 둘레, v 는 단면 </summary>
		public static Mesh Torus(int majorSegments, int minorSegments, float tubeRatio)
		{
			List<Vector3> vertices = new();
			List<Vector2> uvs = new();
			List<int> triangles = new();
			for (int major = 0; major <= majorSegments; major++)
			{
				float around = Mathf.PI * 2f * major / majorSegments;
				Vector3 center = new(Mathf.Cos(around), 0f, Mathf.Sin(around));
				for (int minor = 0; minor <= minorSegments; minor++)
				{
					float section = Mathf.PI * 2f * minor / minorSegments;
					vertices.Add(center * (1f + tubeRatio * Mathf.Cos(section)) + Vector3.up * tubeRatio * Mathf.Sin(section));
					uvs.Add(new Vector2(major / (float)majorSegments, minor / (float)minorSegments));
				}
			}
			int stride = minorSegments + 1;
			for (int major = 0; major < majorSegments; major++)
			for (int minor = 0; minor < minorSegments; minor++)
			{
				int corner = major * stride + minor;
				triangles.Add(corner);
				triangles.Add(corner + 1);
				triangles.Add(corner + stride);
				triangles.Add(corner + 1);
				triangles.Add(corner + stride + 1);
				triangles.Add(corner + stride);
			}
			return Smooth("UltimateTorus", vertices, uvs, White(vertices.Count), triangles);
		}

		/// <summary>
		/// XZ 평면 고리판. 바깥 반지름 1, 안 반지름 innerRatio. u 는 둘레를 turns 바퀴, v 는 안 (0) 에서 바깥 (1)
		/// - 꼭짓점 알파: 안쪽 가장자리 innerAlpha, 가운데 띠 1, 바깥 0 으로 옅어짐
		/// </summary>
		public static Mesh Annulus(int segments, int bands, float innerRatio, float innerAlpha, float turns)
		{
			List<Vector3> vertices = new();
			List<Vector2> uvs = new();
			List<Color> colors = new();
			List<int> triangles = new();
			for (int segment = 0; segment <= segments; segment++)
			{
				float around = Mathf.PI * 2f * segment / segments;
				Vector3 direction = new(Mathf.Cos(around), 0f, Mathf.Sin(around));
				for (int band = 0; band <= bands; band++)
				{
					float radial = band / (float)bands;
					vertices.Add(direction * Mathf.Lerp(innerRatio, 1f, radial));
					uvs.Add(new Vector2(segment / (float)segments * turns, radial));
					float alpha = radial < 0.25f ? Mathf.Lerp(innerAlpha, 1f, radial / 0.25f) : Mathf.Pow(1f - (radial - 0.25f) / 0.75f, 1.6f);
					colors.Add(new Color(1f, 1f, 1f, alpha));
				}
			}
			int stride = bands + 1;
			for (int segment = 0; segment < segments; segment++)
			for (int band = 0; band < bands; band++)
			{
				int corner = segment * stride + band;
				triangles.Add(corner);
				triangles.Add(corner + stride);
				triangles.Add(corner + 1);
				triangles.Add(corner + 1);
				triangles.Add(corner + stride);
				triangles.Add(corner + stride + 1);
			}
			return Smooth("UltimateAnnulus", vertices, uvs, colors, triangles);
		}

		/// <summary>
		/// +Z 로 열린 원뿔 옆면. z 0 에서 반지름 startRatio, z 1 에서 반지름 1. u 는 둘레를 turns 바퀴, v 는 길이
		/// - 꼭짓점 알파: 시작 0 에서 오르고 끝으로 갈수록 옅어짐 (입자 사이 빈틈만 메우는 보조층)
		/// </summary>
		public static Mesh Cone(int segments, int rings, float startRatio, float turns)
		{
			List<Vector3> vertices = new();
			List<Vector2> uvs = new();
			List<Color> colors = new();
			List<int> triangles = new();
			for (int ring = 0; ring <= rings; ring++)
			{
				float along = ring / (float)rings;
				float radius = Mathf.Lerp(startRatio, 1f, Mathf.Sqrt(along));
				float alpha = Mathf.Clamp01(along * 6f) * Mathf.Pow(1f - along, 0.8f);
				for (int segment = 0; segment <= segments; segment++)
				{
					float around = Mathf.PI * 2f * segment / segments;
					vertices.Add(new Vector3(Mathf.Cos(around) * radius, Mathf.Sin(around) * radius, along));
					uvs.Add(new Vector2(segment / (float)segments * turns, along));
					colors.Add(new Color(1f, 1f, 1f, alpha));
				}
			}
			int stride = segments + 1;
			for (int ring = 0; ring < rings; ring++)
			for (int segment = 0; segment < segments; segment++)
			{
				int corner = ring * stride + segment;
				triangles.Add(corner);
				triangles.Add(corner + stride);
				triangles.Add(corner + 1);
				triangles.Add(corner + 1);
				triangles.Add(corner + stride);
				triangles.Add(corner + stride + 1);
			}
			return Smooth("UltimateCone", vertices, uvs, colors, triangles);
		}

		/// <summary>
		/// 꺾인 선 여럿을 관으로 채움 (번개 줄기와 가지). 같은 mesh 를 다시 채우면 모양이 바뀜
		/// - strands 의 점마다 반지름 radii (같은 길이). 꼭짓점 알파는 각 선 끝으로 갈수록 tipAlpha 로, 첫 선 (번개 줄기) 만 trunkTipAlpha 로
		/// </summary>
		public static void FillTube(Mesh mesh, IReadOnlyList<IReadOnlyList<Vector3>> strands, IReadOnlyList<IReadOnlyList<float>> radii, int sides, float tipAlpha, float trunkTipAlpha)
		{
			List<Vector3> vertices = new();
			List<Color> colors = new();
			List<Vector2> uvs = new();
			List<int> triangles = new();
			for (int strandIndex = 0; strandIndex < strands.Count; strandIndex++)
			{
				IReadOnlyList<Vector3> points = strands[strandIndex];
				IReadOnlyList<float> widths = radii[strandIndex];
				if (points.Count < 2)
					continue;
				int start = vertices.Count;
				Vector3 previousNormal = Vector3.zero;
				for (int index = 0; index < points.Count; index++)
				{
					Vector3 forward = (points[Mathf.Min(index + 1, points.Count - 1)] - points[Mathf.Max(index - 1, 0)]).normalized;
					Vector3 normal = previousNormal == Vector3.zero ? Vector3.Cross(forward, Mathf.Abs(forward.y) > 0.9f ? Vector3.right : Vector3.up).normalized : Vector3.ProjectOnPlane(previousNormal, forward).normalized;
					previousNormal = normal;
					Vector3 binormal = Vector3.Cross(forward, normal);
					float along = index / (float)(points.Count - 1);
					float alpha = Mathf.Lerp(1f, strandIndex == 0 ? trunkTipAlpha : tipAlpha, along * along);
					for (int side = 0; side <= sides; side++)
					{
						float around = Mathf.PI * 2f * side / sides;
						vertices.Add(points[index] + (normal * Mathf.Cos(around) + binormal * Mathf.Sin(around)) * widths[index]);
						colors.Add(new Color(1f, 1f, 1f, alpha));
						uvs.Add(new Vector2(side / (float)sides, along));
					}
				}
				int stride = sides + 1;
				for (int index = 0; index < points.Count - 1; index++)
				for (int side = 0; side < sides; side++)
				{
					int corner = start + index * stride + side;
					triangles.Add(corner);
					triangles.Add(corner + stride);
					triangles.Add(corner + 1);
					triangles.Add(corner + 1);
					triangles.Add(corner + stride);
					triangles.Add(corner + stride + 1);
				}
			}
			mesh.Clear();
			if (vertices.Count > 65000)
				mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			mesh.SetVertices(vertices);
			mesh.SetColors(colors);
			mesh.SetUVs(0, uvs);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
		}

		/// <summary>
		/// 번개 줄기 하나의 꺾인 점들. from 에서 to 까지 nodes 마디, 마디마다 옆으로 jitter 만큼 흔듦 (끝 둘은 고정)
		/// - 가지: 줄기 중간 마디에서 아래쪽 바깥으로 뻗는 짧은 선. branches 개
		/// </summary>
		public static List<List<Vector3>> BoltStrands(Vector3 from, Vector3 to, int nodes, float jitter, int branches, float branchLength)
		{
			List<List<Vector3>> strands = new();
			List<Vector3> trunk = new();
			Vector3 axis = to - from;
			Vector3 side = Vector3.Cross(axis.normalized, Vector3.up);
			if (side.sqrMagnitude < 0.01f)
				side = Vector3.right;
			side.Normalize();
			Vector3 depth = Vector3.Cross(axis.normalized, side);
			Vector2 drift = Vector2.zero;
			for (int node = 0; node <= nodes; node++)
			{
				float along = node / (float)nodes;
				drift = node == 0 || node == nodes ? Vector2.zero : drift * 0.45f + Random.insideUnitCircle * jitter;
				trunk.Add(from + axis * along + side * drift.x + depth * drift.y);
			}
			strands.Add(trunk);
			for (int branch = 0; branch < branches; branch++)
			{
				int origin = Random.Range(Mathf.Max(1, nodes / 5), Mathf.Max(2, nodes * 4 / 5));
				Vector3 outward = (side * Random.Range(-1f, 1f) + depth * Random.Range(-1f, 1f)).normalized;
				Vector3 direction = (outward + axis.normalized * Random.Range(0.6f, 1.2f)).normalized;
				int length = Random.Range(3, 6);
				List<Vector3> twig = new() { trunk[origin] };
				for (int step = 1; step <= length; step++)
					twig.Add(twig[step - 1] + direction * branchLength / length + Random.insideUnitSphere * jitter * 0.5f);
				strands.Add(twig);
			}
			return strands;
		}

		// 스프라이트 셰이더가 꼭짓점 색을 곱하므로 색 없는 메시도 흰색을 채움
		private static List<Color> White(int count)
		{
			List<Color> colors = new(count);
			for (int index = 0; index < count; index++)
				colors.Add(Color.white);
			return colors;
		}

		private static Mesh Smooth(string name, List<Vector3> vertices, List<Vector2> uvs, List<Color> colors, List<int> triangles)
		{
			Mesh mesh = new() { name = name };
			if (vertices.Count > 65000)
				mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			mesh.SetVertices(vertices);
			mesh.SetUVs(0, uvs);
			if (colors != null)
				mesh.SetColors(colors);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			return mesh;
		}
	}
}
