using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>Idle 전투 화면에서 쓰는 임시 메시와 런타임 머티리얼 생성만 담당한다.</summary>
	internal static class IdleBattleVisualFactory
	{
		public static Material Paint(GameObject piece, Color color)
		{
			Material made = MakeMaterial(color);
			piece.GetComponent<MeshRenderer>().sharedMaterial = made;
			return made;
		}

		public static Material MakeMaterial(Color color)
		{
			Shader shader = Shader.Find("Universal Render Pipeline/Lit");
			if (shader == null)
			{
				shader = Shader.Find("Standard");
			}

			Material made = new Material(shader);
			made.hideFlags = HideFlags.DontSave;
			made.color = color;
			if (made.HasProperty("_BaseColor"))
			{
				made.SetColor("_BaseColor", color);
			}

			return made;
		}

		public static Mesh BuildImpactMesh()
		{
			return NgonPrism(4, 0.5f, 0.5f);
		}

		/// <summary>면별 정점을 분리한 저분할 면체. 공유 노멀을 만들지 않는다.</summary>
		public static Mesh NgonPrism(int sides, float radius, float height)
		{
			Mesh mesh = new Mesh();
			mesh.name = "Polyhedron" + sides;
			sides = Mathf.Clamp(sides, 4, 12);
			List<Vector3> vertices = new List<Vector3>();
			List<int> triangles = new List<int>();
			Vector3 top = new Vector3(0f, height * 0.5f, 0f);
			Vector3 bottom = new Vector3(0f, -height * 0.5f, 0f);
			for (int at = 0; at < sides; at++)
			{
				float angle = at * Mathf.PI * 2f / sides;
				float nextAngle = (at + 1) * Mathf.PI * 2f / sides;
				Vector3 current = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
				Vector3 next = new Vector3(Mathf.Cos(nextAngle) * radius, 0f, Mathf.Sin(nextAngle) * radius);
				int start = vertices.Count;
				vertices.Add(top); vertices.Add(current); vertices.Add(next);
				triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
				start = vertices.Count;
				vertices.Add(bottom); vertices.Add(next); vertices.Add(current);
				triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
			}

			mesh.SetVertices(vertices);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			return mesh;
		}
	}
}
