using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// 면마다 정점을 따로 두는 각진 저폴리 메시 조립. 색은 색 띠 텍스처의 줄, 결은 U 좌표
	public sealed class BreakableMeshBuilder
	{
		private readonly List<Vector3> vertices = new();
		private readonly List<Vector3> normals = new();
		private readonly List<Vector2> uvs = new();
		private readonly List<int> triangles = new();
		private readonly BreakableAtlas atlas;
		private readonly System.Random random;
		private readonly float grainPerMeter;
		private Matrix4x4 matrix = Matrix4x4.identity;

		public BreakableMeshBuilder(BreakableAtlas atlas, System.Random random, float grainPerMeter)
		{
			this.atlas = atlas;
			this.random = random;
			this.grainPerMeter = grainPerMeter;
		}

		public System.Random Random => random;

		public float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

		public void SetMatrix(Matrix4x4 newMatrix) => matrix = newMatrix;

		// a에서 b가 결을 가로지르는 방향. 결은 a에서 d 방향으로 흐름
		public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int row)
		{
			a = matrix.MultiplyPoint3x4(a);
			b = matrix.MultiplyPoint3x4(b);
			c = matrix.MultiplyPoint3x4(c);
			d = matrix.MultiplyPoint3x4(d);
			Vector3 normal = Vector3.Cross(b - a, d - a).normalized;
			float v = atlas.RowV(row);
			float uStart = (float)random.NextDouble();
			float uEnd = uStart + Vector3.Distance(a, b) * grainPerMeter;
			int start = vertices.Count;
			vertices.Add(a);
			vertices.Add(b);
			vertices.Add(c);
			vertices.Add(d);
			for (int i = 0; i < 4; i++)
				normals.Add(normal);
			uvs.Add(new Vector2(uStart, v));
			uvs.Add(new Vector2(uEnd, v));
			uvs.Add(new Vector2(uEnd, v));
			uvs.Add(new Vector2(uStart, v));
			triangles.Add(start);
			triangles.Add(start + 1);
			triangles.Add(start + 2);
			triangles.Add(start);
			triangles.Add(start + 2);
			triangles.Add(start + 3);
		}

		public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, int row)
		{
			a = matrix.MultiplyPoint3x4(a);
			b = matrix.MultiplyPoint3x4(b);
			c = matrix.MultiplyPoint3x4(c);
			Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
			float v = atlas.RowV(row);
			float uStart = (float)random.NextDouble();
			int start = vertices.Count;
			vertices.Add(a);
			vertices.Add(b);
			vertices.Add(c);
			for (int i = 0; i < 3; i++)
				normals.Add(normal);
			uvs.Add(new Vector2(uStart, v));
			uvs.Add(new Vector2(uStart + Vector3.Distance(a, b) * grainPerMeter, v));
			uvs.Add(new Vector2(uStart + Vector3.Distance(a, c) * grainPerMeter * .5f, v));
			triangles.Add(start);
			triangles.Add(start + 1);
			triangles.Add(start + 2);
		}

		// 결은 각 면의 긴 변을 따라 흐름
		public void AddBox(Vector3 center, Vector3 size, Quaternion rotation, int sideRow, int capRow)
		{
			Vector3 half = size * .5f;
			Vector3 right = rotation * new Vector3(half.x, 0f, 0f);
			Vector3 up = rotation * new Vector3(0f, half.y, 0f);
			Vector3 forward = rotation * new Vector3(0f, 0f, half.z);
			AddBoxFace(center + forward, right, up, size.x, size.y, sideRow);
			AddBoxFace(center - forward, -right, up, size.x, size.y, sideRow);
			AddBoxFace(center + right, -forward, up, size.z, size.y, sideRow);
			AddBoxFace(center - right, forward, up, size.z, size.y, sideRow);
			AddBoxFace(center + up, right, -forward, size.x, size.z, capRow);
			AddBoxFace(center - up, right, forward, size.x, size.z, capRow);
		}

		public void AddBox(Vector3 center, Vector3 size, Quaternion rotation, int row) => AddBox(center, size, rotation, row, row);

		// 바깥에서 볼 때 시계 방향 (Unity 앞면). axisA 와 axisB 는 면 중심에서 두 변까지의 반 벡터
		private void AddBoxFace(Vector3 center, Vector3 axisA, Vector3 axisB, float lengthA, float lengthB, int row)
		{
			if (lengthA <= lengthB)
				AddQuad(center - axisA - axisB, center + axisA - axisB, center + axisA + axisB, center - axisA + axisB, row);
			else
				AddQuad(center + axisB - axisA, center - axisB - axisA, center - axisB + axisA, center + axisB + axisA, row);
		}

		// 회전체. heights 와 radii 는 같은 길이, 아래에서 위로. rows 는 띠마다 색 줄 (길이 heights - 1)
		public void AddLathe(Vector3 origin, float[] heights, float[] radii, int sides, int[] rows, int bottomRow, int topRow, float phase)
		{
			for (int band = 0; band < heights.Length - 1; band++)
			{
				for (int side = 0; side < sides; side++)
				{
					float angleA = (side + phase) * Mathf.PI * 2f / sides;
					float angleB = (side + 1 + phase) * Mathf.PI * 2f / sides;
					Vector3 directionA = new(Mathf.Cos(angleA), 0f, Mathf.Sin(angleA));
					Vector3 directionB = new(Mathf.Cos(angleB), 0f, Mathf.Sin(angleB));
					Vector3 lowA = origin + directionA * radii[band] + Vector3.up * heights[band];
					Vector3 lowB = origin + directionB * radii[band] + Vector3.up * heights[band];
					Vector3 highA = origin + directionA * radii[band + 1] + Vector3.up * heights[band + 1];
					Vector3 highB = origin + directionB * radii[band + 1] + Vector3.up * heights[band + 1];
					AddQuad(lowB, lowA, highA, highB, rows[band]);
				}
			}
			if (bottomRow >= 0)
				AddDisc(origin + Vector3.up * heights[0], radii[0], sides, bottomRow, phase, false);
			if (topRow >= 0)
				AddDisc(origin + Vector3.up * heights[heights.Length - 1], radii[radii.Length - 1], sides, topRow, phase, true);
		}

		public void AddDisc(Vector3 center, float radius, int sides, int row, float phase, bool facingUp)
		{
			for (int side = 0; side < sides; side++)
			{
				float angleA = (side + phase) * Mathf.PI * 2f / sides;
				float angleB = (side + 1 + phase) * Mathf.PI * 2f / sides;
				Vector3 pointA = center + new Vector3(Mathf.Cos(angleA), 0f, Mathf.Sin(angleA)) * radius;
				Vector3 pointB = center + new Vector3(Mathf.Cos(angleB), 0f, Mathf.Sin(angleB)) * radius;
				if (facingUp)
					AddTriangle(center, pointB, pointA, row);
				else
					AddTriangle(center, pointA, pointB, row);
			}
		}

		public Mesh Build(string meshName)
		{
			Mesh mesh = new() { name = meshName };
			if (vertices.Count > ushort.MaxValue)
				mesh.indexFormat = IndexFormat.UInt32;
			mesh.SetVertices(vertices);
			mesh.SetNormals(normals);
			mesh.SetUVs(0, uvs);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateBounds();
			mesh.RecalculateTangents();
			mesh.UploadMeshData(true);
			vertices.Clear();
			normals.Clear();
			uvs.Clear();
			triangles.Clear();
			matrix = Matrix4x4.identity;
			return mesh;
		}
	}
}
