using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 1m 칸 보기. 개발 확인용이라 켤 때만 만듦
	public sealed partial class VillageStage
	{
		[Header("Grid")]
		[SerializeField] private RectInt gridArea = new(-30, -24, 60, 52);
		[SerializeField] private float gridHeight = 0.16f;
		[SerializeField] private Color gridColor = new(1f, 1f, 1f, 0.16f);
		[SerializeField] private Color gridTakenColor = new(1f, 0.86f, 0.35f, 0.95f);

		private GameObject gridBody;

		public bool ToggleGrid()
		{
			if (gridBody != null)
			{
				Destroy(gridBody);
				gridBody = null;
				return false;
			}
			List<Vector3> points = new();
			List<Color> colors = new();
			for (int x = gridArea.xMin; x <= gridArea.xMax; x++)
				AddGridLine(points, colors, new Vector2(x, gridArea.yMin), new Vector2(x, gridArea.yMax), gridColor);
			for (int z = gridArea.yMin; z <= gridArea.yMax; z++)
				AddGridLine(points, colors, new Vector2(gridArea.xMin, z), new Vector2(gridArea.xMax, z), gridColor);
			AddGridBox(points, colors, Models.Footprint(HALL_MODELS[Mathf.Min(Sim.State.TownHall, HALL_MODELS.Length) - 1], townHallPosition, 0f));
			AddGridBox(points, colors, Models.Footprint("HouseMended", housePosition, houseYaw));
			foreach (BuildingSite site in sites)
				AddGridBox(points, colors, Models.Footprint(Sim.IsBuilt(site.Id) ? BUILT_MODELS[site.Id] : YARD_SITES.Contains(site.Id) ? "RuinYard" : RUIN_MODELS[0], site.Position, site.Yaw));

			int[] indices = new int[points.Count];
			for (int i = 0; i < indices.Length; i++)
				indices[i] = i;
			Mesh mesh = new() { name = "VillageGrid" };
			mesh.SetVertices(points);
			mesh.SetColors(colors);
			mesh.SetIndices(indices, MeshTopology.Lines, 0);
			gridBody = new GameObject("VillageGrid");
			gridBody.transform.SetParent(outdoor, false);
			gridBody.AddComponent<MeshFilter>().sharedMesh = mesh;
			gridBody.AddComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default"));
			return true;
		}

		private void AddGridBox(List<Vector3> points, List<Color> colors, Rect box)
		{
			Vector2 low = box.min;
			Vector2 high = box.max;
			AddGridLine(points, colors, low, new Vector2(high.x, low.y), gridTakenColor);
			AddGridLine(points, colors, new Vector2(high.x, low.y), high, gridTakenColor);
			AddGridLine(points, colors, high, new Vector2(low.x, high.y), gridTakenColor);
			AddGridLine(points, colors, new Vector2(low.x, high.y), low, gridTakenColor);
		}

		private void AddGridLine(List<Vector3> points, List<Color> colors, Vector2 from, Vector2 to, Color color)
		{
			points.Add(new Vector3(from.x, gridHeight, from.y));
			points.Add(new Vector3(to.x, gridHeight, to.y));
			colors.Add(color);
			colors.Add(color);
		}
	}
}
