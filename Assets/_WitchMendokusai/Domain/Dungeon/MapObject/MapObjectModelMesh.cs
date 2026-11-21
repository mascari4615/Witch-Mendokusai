using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 맵 오브젝트 부품 저폴리 메시. 면마다 정점을 따로 둬 각진 음영, UV 는 면 평면 투영 (미터 단위라 텍스처 밀도가 크기와 무관)
	// 바닥이 y 0, 가로 세로 가운데가 원점. 정점 색은 부품 색 (빛 재질이 씀)
	public static class MapObjectModelMesh
	{
		private sealed class Builder
		{
			private readonly List<Vector3> vertices = new();
			private readonly List<Vector3> normals = new();
			private readonly List<Vector2> uvs = new();
			private readonly List<Color> colors = new();
			private readonly List<int> triangles = new();
			private readonly Color color;
			private readonly float uvPerMeter;
			private readonly float facetShade;

			public Builder(Color color, float uvPerMeter, float facetShade)
			{
				this.color = color;
				this.uvPerMeter = uvPerMeter;
				this.facetShade = facetShade;
			}

			// 볼록 다각형 한 면 (바깥에서 보면 시계 방향). UV 는 면 평면 투영, 옆면은 가로가 수평 위가 위 (바깥에서 봐서 안 뒤집힘)
			public void Face(params Vector3[] corners)
			{
				Vector3 normal = Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]).normalized;
				Vector3 tangent = Vector3.Cross(normal, Vector3.up);
				if (tangent.sqrMagnitude < 0.0001f)
					tangent = Vector3.right;
				tangent.Normalize();
				Vector3 bitangent = Vector3.Cross(tangent, normal);
				// 조명 없는 빛 재질용 면 밝기. 위를 볼수록 밝게
				float brightness = 1f - facetShade * (0.5f - 0.5f * normal.y);
				Color faceColor = new(color.r * brightness, color.g * brightness, color.b * brightness, color.a);
				int start = vertices.Count;
				foreach (Vector3 corner in corners)
				{
					vertices.Add(corner);
					normals.Add(normal);
					uvs.Add(new Vector2(Vector3.Dot(corner, tangent), Vector3.Dot(corner, bitangent)) * uvPerMeter);
					colors.Add(faceColor);
				}
				for (int i = 1; i < corners.Length - 1; i++)
				{
					triangles.Add(start);
					triangles.Add(start + i);
					triangles.Add(start + i + 1);
				}
			}

			public Mesh ToMesh()
			{
				Mesh mesh = new();
				mesh.SetVertices(vertices);
				mesh.SetNormals(normals);
				mesh.SetUVs(0, uvs);
				mesh.SetColors(colors);
				mesh.SetTriangles(triangles, 0);
				mesh.RecalculateBounds();
				return mesh;
			}
		}

		// 결정 허리 높이 비율. 위아래 대칭 쌍뿔
		private const float CRYSTAL_WAIST = 0.5f;

		public static Mesh Build(MapObjectPartShape shape, Vector3 size, int sides, float topScale, Color color, float uvPerMeter, float facetShade)
		{
			Builder builder = new(color, uvPerMeter, facetShade);
			switch (shape)
			{
				case MapObjectPartShape.Box:
					Prism(builder, size, 4, 1f, Mathf.PI * 0.25f, Mathf.Sqrt(2f));
					break;
				case MapObjectPartShape.Prism:
					Prism(builder, size, sides, topScale, 0f, 1f);
					break;
				case MapObjectPartShape.Crystal:
					Crystal(builder, size, sides);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(shape), shape, null);
			}
			return builder.ToMesh();
		}

		// 둘레 꼭짓점. radiusScale 은 상자처럼 모서리가 반 변에 오게 늘리는 배율
		private static Vector3 Ring(Vector3 size, int sides, int index, float startAngle, float radiusScale, float scale, float height)
		{
			float angle = startAngle + Mathf.PI * 2f * index / sides;
			return new Vector3(Mathf.Cos(angle) * size.x * 0.5f * radiusScale * scale, height, Mathf.Sin(angle) * size.z * 0.5f * radiusScale * scale);
		}

		private static void Prism(Builder builder, Vector3 size, int sides, float topScale, float startAngle, float radiusScale)
		{
			Vector3[] bottom = new Vector3[sides];
			Vector3[] top = new Vector3[sides];
			for (int i = 0; i < sides; i++)
			{
				bottom[i] = Ring(size, sides, i, startAngle, radiusScale, 1f, 0f);
				top[i] = Ring(size, sides, i, startAngle, radiusScale, topScale, size.y);
			}
			for (int i = 0; i < sides; i++)
			{
				int next = (i + 1) % sides;
				builder.Face(bottom[i], top[i], top[next], bottom[next]);
			}
			// 윗면은 위에서 시계 방향, 아랫면은 아래에서 시계 방향
			Vector3[] cap = new Vector3[sides];
			for (int i = 0; i < sides; i++)
				cap[i] = top[sides - 1 - i];
			builder.Face(cap);
			builder.Face(bottom);
		}

		private static void Crystal(Builder builder, Vector3 size, int sides)
		{
			Vector3 tip = new(0f, size.y, 0f);
			Vector3 foot = Vector3.zero;
			Vector3[] middle = new Vector3[sides];
			for (int i = 0; i < sides; i++)
				middle[i] = Ring(size, sides, i, 0f, 1f, 1f, size.y * CRYSTAL_WAIST);
			for (int i = 0; i < sides; i++)
			{
				int next = (i + 1) % sides;
				builder.Face(middle[i], tip, middle[next]);
				builder.Face(middle[next], foot, middle[i]);
			}
		}
	}
}
