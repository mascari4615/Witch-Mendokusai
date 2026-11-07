using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	public readonly struct SkillTreeCell
	{
		public readonly float Column;
		public readonly int Row;

		public SkillTreeCell(float column, int row)
		{
			Column = column;
			Row = row;
		}
	}

	// 노드 자동 배치와 방향키 이동 대상 계산
	public static class SkillTreeLayout
	{
		public static Dictionary<string, SkillTreeCell> Compute(SkillTree tree, int maxPerRow)
		{
			Dictionary<SkillTreeNode, int> depths = new();
			foreach (SkillTreeNode node in tree.Nodes)
				DepthOf(node, depths);

			Dictionary<string, SkillTreeCell> cells = new();
			int row = 0;
			foreach (IGrouping<int, SkillTreeNode> layer in tree.Nodes.GroupBy(node => depths[node]).OrderBy(group => group.Key))
			{
				List<SkillTreeNode> ordered = layer.OrderBy(node => ParentColumn(node, cells)).ToList();
				for (int start = 0; start < ordered.Count; start += maxPerRow)
				{
					List<SkillTreeNode> chunk = ordered.Skip(start).Take(maxPerRow).ToList();
					for (int i = 0; i < chunk.Count; i++)
						cells.Add(chunk[i].Id, new SkillTreeCell(i - (chunk.Count - 1) / 2f, row));
					row++;
				}
			}
			return cells;
		}

		public static int RowCount(Dictionary<string, SkillTreeCell> cells) => cells.Count == 0 ? 0 : cells.Values.Max(cell => cell.Row) + 1;

		// 방향에 노드가 없으면 null
		public static string Next(Dictionary<string, SkillTreeCell> cells, string from, Vector2Int direction)
		{
			SkillTreeCell origin = cells[from];
			string best = null;
			int bestRowGap = int.MaxValue;
			float bestColumnGap = float.MaxValue;
			foreach (KeyValuePair<string, SkillTreeCell> pair in cells)
			{
				if (pair.Key == from)
					continue;

				float columnDelta = pair.Value.Column - origin.Column;
				int rowDelta = pair.Value.Row - origin.Row;
				float along = direction.x != 0 ? columnDelta * direction.x : rowDelta * -direction.y;
				if (along <= 0f)
					continue;

				int rowGap = Mathf.Abs(rowDelta);
				float columnGap = Mathf.Abs(columnDelta);
				if (rowGap < bestRowGap || (rowGap == bestRowGap && columnGap < bestColumnGap))
				{
					bestRowGap = rowGap;
					bestColumnGap = columnGap;
					best = pair.Key;
				}
			}
			return best;
		}

		private static int DepthOf(SkillTreeNode node, Dictionary<SkillTreeNode, int> depths)
		{
			if (depths.TryGetValue(node, out int depth))
				return depth;

			depth = 0;
			foreach (SkillTreeRequirement requirement in node.Requirements.Where(requirement => requirement.IsExternal == false))
				depth = Mathf.Max(depth, DepthOf(requirement.Node, depths) + 1);
			depths.Add(node, depth);
			return depth;
		}

		private static float ParentColumn(SkillTreeNode node, Dictionary<string, SkillTreeCell> cells)
		{
			List<SkillTreeNode> parents = node.Requirements.Where(requirement => requirement.IsExternal == false).Select(requirement => requirement.Node).ToList();
			return parents.Count == 0 ? 0f : parents.Average(parent => cells[parent.Id].Column);
		}
	}
}
