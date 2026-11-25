using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 1m 칸 보기. 건설 중이나 개발 확인 때만 만듦
	public sealed partial class VillageStage
	{
		[Header("Grid")]
		[SerializeField] private float gridHeight = 0.16f;
		[SerializeField] private Color gridColor = new(1f, 1f, 1f, 0.16f);
		[SerializeField] private Color gridTakenColor = new(1f, 0.86f, 0.35f, 0.95f);

		private GameObject gridBody;
		private Material lineMaterial;

		private Material LineMaterial
		{
			get
			{
				if (lineMaterial == null)
					lineMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.DontSave };
				return lineMaterial;
			}
		}

		public bool ToggleGrid()
		{
			ShowGrid(gridBody == null);
			return gridBody != null;
		}

		private void ShowGrid(bool visible)
		{
			if (gridBody != null)
				Destroy(gridBody);
			gridBody = null;
			if (visible == false)
				return;
			VillageAreaDef area = Catalog.VillageArea;
			List<Vector3> points = new();
			List<Color> colors = new();
			for (int x = area.X; x <= area.X + area.Width; x++)
				AddGridLine(points, colors, new Vector2(x, area.Z), new Vector2(x, area.Z + area.Depth), gridColor);
			for (int z = area.Z; z <= area.Z + area.Depth; z++)
				AddGridLine(points, colors, new Vector2(area.X, z), new Vector2(area.X + area.Width, z), gridColor);
			foreach (PlacedPiece piece in Sim.State.Village)
			{
				PlaceableDef placeable = Sim.PlaceableOf(piece);
				if (placeable.Kind == PlaceableKind.Building)
					AddGridBox(points, colors, new Rect(piece.X, piece.Z, VillageRules.WidthOf(placeable, piece.Turn), VillageRules.DepthOf(placeable, piece.Turn)));
			}

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
			gridBody.AddComponent<MeshRenderer>().sharedMaterial = LineMaterial;
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
