using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 숲 가장자리 나무 사이로 드는 햇살 줄기. 메시 하나, 방향은 셰이더가 해를 따라 잡음
	public sealed partial class VillageGround
	{
		[Header("Shafts")]
		[SerializeField] private int shaftSeed = 17;
		[SerializeField] private int shaftCount = 26;
		[Tooltip("빈터 경계에서의 거리 (안쪽 음수)")]
		[SerializeField] private Vector2 shaftEdge = new(-3f, 5f);
		[SerializeField] private Vector2 shaftLength = new(9f, 15f);
		[SerializeField] private Vector2 shaftHalfWidth = new(0.5f, 1.3f);

		private void BuildShafts()
		{
			System.Random random = new(shaftSeed);
			List<Vector3> points = new();
			List<Vector2> corners = new();
			List<Vector2> sizes = new();
			List<int> triangles = new();
			const int ATTEMPTS_EACH = 60;
			for (int attempt = 0; attempt < shaftCount * ATTEMPTS_EACH && points.Count < shaftCount * 4; attempt++)
			{
				Vector2 point = new(grassArea.xMin + (float)random.NextDouble() * grassArea.width, grassArea.yMin + (float)random.NextDouble() * grassArea.height);
				float edge = EdgeDistance(point.x, point.y);
				if (edge < shaftEdge.x || edge > shaftEdge.y || WaterDistance(point.x, point.y) < 1f)
					continue;
				Vector2 size = new(Range01(random, shaftLength), Range01(random, shaftHalfWidth));
				int first = points.Count;
				for (int corner = 0; corner < 4; corner++)
				{
					points.Add(new Vector3(point.x, 0f, point.y));
					corners.Add(new Vector2(corner == 0 || corner == 3 ? -1f : 1f, corner < 2 ? 0f : 1f));
					sizes.Add(size);
				}
				triangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
			}
			Mesh mesh = new() { name = "VillageShafts", hideFlags = HideFlags.DontSave };
			mesh.SetVertices(points);
			mesh.SetUVs(0, corners);
			mesh.SetUVs(1, sizes);
			mesh.SetTriangles(triangles, 0);
			// 셰이더가 정점을 옮기므로 경계를 넓힘
			mesh.bounds = new Bounds(new Vector3(clearing.center.x, 5f, clearing.center.y), new Vector3(grassArea.width + 30f, 40f, grassArea.height + 30f));
			AddChildMesh("Shafts", mesh, HomesteadArt.Instance.Shaft);
		}
	}
}
