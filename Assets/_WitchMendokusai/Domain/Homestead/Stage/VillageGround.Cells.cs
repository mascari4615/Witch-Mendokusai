using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 놓인 것에 맞춰 바닥을 고침. 칠은 바뀐 칸 둘레만, 풀은 칸에 걸린 잎만 접음
	public sealed partial class VillageGround
	{
		private const int BLADE_VERTICES = 5;
		private const int FLOWER_VERTICES = 4;

		[Header("Cells")]
		[SerializeField] private float cellPathNoise = 0.22f;
		[SerializeField] private Vector2 cellPathRange = new(0.02f, 0.34f);
		[SerializeField] private Vector2 cellWornRange = new(0f, 0.9f);
		[SerializeField, Range(0f, 1f)] private float cellWorn = 0.5f;
		[SerializeField] private float grassMargin = 0.06f;

		private sealed class GrassPatch
		{
			public Mesh Mesh;
			public Vector3[] Home;
			public int Stride;
			public Rect Area;
		}

		private readonly List<GrassPatch> grassPatches = new();
		private HashSet<Vector2Int> pathCells = new();
		private HashSet<Vector2Int> bareCells = new();
		private Color32[] shownPixels;

		private void KeepGrass(Mesh mesh, int stride = BLADE_VERTICES)
		{
			Bounds bounds = mesh.bounds;
			grassPatches.Add(new GrassPatch { Mesh = mesh, Home = mesh.vertices, Stride = stride, Area = Rect.MinMaxRect(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z) });
		}

		// path 는 흙길 칸, bare 는 건물과 꾸밈 아래라 풀이 없는 칸
		public void SetCells(HashSet<Vector2Int> path, HashSet<Vector2Int> bare)
		{
			HashSet<Vector2Int> changed = new(path);
			changed.SymmetricExceptWith(pathCells);
			HashSet<Vector2Int> changedBare = new(bare);
			changedBare.SymmetricExceptWith(bareCells);
			changed.UnionWith(changedBare);
			if (changed.Count == 0)
				return;
			pathCells = path;
			bareCells = bare;
			Repaint(changed);
			FoldGrass(changed);
		}

		private void Repaint(HashSet<Vector2Int> changed)
		{
			shownPixels ??= (Color32[])controlPixels.Clone();
			float texel = paintArea.width / paintResolution;
			HashSet<int> pixels = new();
			foreach (Vector2Int cell in changed)
			{
				int fromColumn = Mathf.Max(0, Mathf.FloorToInt((cell.x - 1f - paintArea.xMin) / texel));
				int toColumn = Mathf.Min(paintResolution - 1, Mathf.CeilToInt((cell.x + 2f - paintArea.xMin) / texel));
				int fromRow = Mathf.Max(0, Mathf.FloorToInt((cell.y - 1f - paintArea.yMin) / texel));
				int toRow = Mathf.Min(paintResolution - 1, Mathf.CeilToInt((cell.y + 2f - paintArea.yMin) / texel));
				for (int row = fromRow; row <= toRow; row++)
				{
					for (int column = fromColumn; column <= toColumn; column++)
						pixels.Add(row * paintResolution + column);
				}
			}
			foreach (int pixel in pixels)
			{
				float x = paintArea.xMin + (pixel % paintResolution + 0.5f) * texel;
				float z = paintArea.yMin + (pixel / paintResolution + 0.5f) * texel;
				Color32 ground = controlPixels[pixel];
				float path = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(cellPathRange.x, cellPathRange.y, CellDistance(pathCells, x, z) + edgeNoise[pixel] * cellPathNoise));
				float worn = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(cellWornRange.x, cellWornRange.y, CellDistance(bareCells, x, z) + edgeNoise[pixel] * cellPathNoise))) * cellWorn * (1f - path);
				float keep = 1f - Mathf.Max(path, worn);
				shownPixels[pixel] = new Color32((byte)(ground.r * keep), (byte)Mathf.Min(255f, ground.g * keep + worn * (255f - ground.a)), (byte)Mathf.Min(255f, ground.b * keep + path * (255f - ground.a)), ground.a);
			}
			control.SetPixels32(shownPixels);
			control.Apply(false);
		}

		// 가장 가까운 칸 네모까지의 거리. 한 칸보다 멀면 1
		private static float CellDistance(HashSet<Vector2Int> cells, float x, float z)
		{
			int column = Mathf.FloorToInt(x);
			int row = Mathf.FloorToInt(z);
			float nearest = 1f;
			for (int i = -1; i <= 1; i++)
			{
				for (int j = -1; j <= 1; j++)
				{
					if (cells.Contains(new Vector2Int(column + i, row + j)) == false)
						continue;
					float outsideX = Mathf.Max(0f, Mathf.Max(column + i - x, x - (column + i + 1)));
					float outsideZ = Mathf.Max(0f, Mathf.Max(row + j - z, z - (row + j + 1)));
					nearest = Mathf.Min(nearest, new Vector2(outsideX, outsideZ).magnitude);
				}
			}
			return nearest;
		}

		private void FoldGrass(HashSet<Vector2Int> changed)
		{
			foreach (GrassPatch patch in grassPatches)
			{
				if (Touches(patch.Area, changed) == false)
					continue;
				Vector3[] shown = (Vector3[])patch.Home.Clone();
				for (int first = 0; first < shown.Length; first += patch.Stride)
				{
					Vector3 foot = (shown[first] + shown[first + 1]) * 0.5f;
					if (IsBare(foot.x, foot.z) == false)
						continue;
					for (int i = 0; i < patch.Stride; i++)
						shown[first + i] = foot;
				}
				patch.Mesh.vertices = shown;
			}
		}

		private bool IsBare(float x, float z) => CellDistance(bareCells, x, z) < grassMargin || CellDistance(pathCells, x, z) < grassMargin;

		private static bool Touches(Rect area, HashSet<Vector2Int> cells)
		{
			foreach (Vector2Int cell in cells)
			{
				if (cell.x + 1 >= area.xMin && cell.x <= area.xMax && cell.y + 1 >= area.yMin && cell.y <= area.yMax)
					return true;
			}
			return false;
		}
	}
}
