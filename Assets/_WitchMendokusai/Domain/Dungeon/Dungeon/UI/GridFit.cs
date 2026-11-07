using UnityEngine;

namespace WitchMendokusai
{
	// 칸 수와 영역 기준 최대 배율의 열 수와 배율. 넘치지 않으면 배율 1
	public readonly struct GridFit
	{
		public int Columns { get; }
		public float Scale { get; }

		private GridFit(int columns, float scale)
		{
			Columns = columns;
			Scale = scale;
		}

		public static GridFit Compute(int count, Vector2 area, Vector2 cell, Vector2 spacing)
		{
			int baseColumns = Mathf.Max(1, Mathf.FloorToInt((area.x + spacing.x) / (cell.x + spacing.x)));
			GridFit best = new(baseColumns, 0f);
			for (int columns = baseColumns; columns <= Mathf.Max(baseColumns, count); columns++)
			{
				int rows = Mathf.Max(1, Mathf.CeilToInt((float)count / columns));
				float width = columns * cell.x + (columns - 1) * spacing.x;
				float height = rows * cell.y + (rows - 1) * spacing.y;
				float scale = Mathf.Min(1f, area.x / width, area.y / height);
				if (scale > best.Scale)
					best = new GridFit(columns, scale);
			}
			return best;
		}
	}
}
