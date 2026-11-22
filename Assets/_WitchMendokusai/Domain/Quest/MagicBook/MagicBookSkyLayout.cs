using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// 마도서 하늘의 별 자리 계산. 한 장의 퀘스트들을 연결 깊이 (가운데에서 먼 정도) 로 줄 세우고,
	/// 갈래는 옆으로 펼친 뒤 장마다 정한 각도로 돌림. 손으로 놓은 자리 (EditorPosition) 는 쓰지 않음:
	/// 퀘스트를 더하거나 잇기만 바꿔도 하늘이 저절로 다시 짜이게
	/// </summary>
	public static class MagicBookSkyLayout
	{
		public readonly struct Options
		{
			public readonly Vector2 Origin;
			public readonly float AngleDegrees;
			public readonly float DepthStep;
			public readonly float BranchStep;
			public readonly float FirstOffset;
			public readonly float Jitter;
			public readonly int Seed;

			public Options(Vector2 origin, float angleDegrees, float depthStep, float branchStep, float firstOffset, float jitter, int seed)
			{
				Origin = origin;
				AngleDegrees = angleDegrees;
				DepthStep = depthStep;
				BranchStep = branchStep;
				FirstOffset = firstOffset;
				Jitter = jitter;
				Seed = seed;
			}
		}

		/// <summary>노드 Id 마다 하늘 좌표. 연결에 고리가 있어도 먼저 놓인 자리를 지킴</summary>
		public static Dictionary<string, Vector2> Place(IReadOnlyList<NodeBase> nodes, IReadOnlyList<NodeConnection> connections, Options options)
		{
			Dictionary<string, List<string>> childrenById = new();
			HashSet<string> hasParent = new();
			foreach (NodeBase node in nodes)
			{
				childrenById[node.Id] = new List<string>();
			}
			foreach (NodeConnection connection in connections)
			{
				if (childrenById.TryGetValue(connection.SourceNodeId, out List<string> children) == false || childrenById.ContainsKey(connection.TargetNodeId) == false)
				{
					continue;
				}
				if (children.Contains(connection.TargetNodeId) == false)
				{
					children.Add(connection.TargetNodeId);
				}
				hasParent.Add(connection.TargetNodeId);
			}

			// (깊이, 갈래 칸). 잎은 차례로 한 칸씩, 부모는 자식들 가운데
			Dictionary<string, Vector2> grid = new();
			int nextRow = 0;
			foreach (NodeBase node in nodes)
			{
				if (hasParent.Contains(node.Id) == false)
				{
					PlaceInGrid(node.Id, 0, childrenById, grid, ref nextRow);
				}
			}
			// 모두가 부모를 가진 고리만 남은 노드
			foreach (NodeBase node in nodes)
			{
				if (grid.ContainsKey(node.Id) == false)
				{
					PlaceInGrid(node.Id, 0, childrenById, grid, ref nextRow);
				}
			}

			float middleRow = (nextRow - 1) * 0.5f;
			float radians = options.AngleDegrees * Mathf.Deg2Rad;
			float cos = Mathf.Cos(radians);
			float sin = Mathf.Sin(radians);
			Dictionary<string, Vector2> result = new();
			int index = 0;
			foreach (NodeBase node in nodes)
			{
				Vector2 cell = grid[node.Id];
				float along = options.FirstOffset + cell.x * options.DepthStep;
				float across = (cell.y - middleRow) * options.BranchStep;
				// 줄이 자로 잰 듯 서지 않게 살짝 흔듦. 같은 장은 늘 같은 모양
				Vector2 jitter = new Vector2(Mathf.Sin(options.Seed + index * 2.3f), Mathf.Cos(options.Seed + index * 1.7f)) * options.Jitter;
				index++;
				result[node.Id] = options.Origin + new Vector2(along * cos - across * sin, along * sin + across * cos) + jitter;
			}
			return result;
		}

		private static float PlaceInGrid(string id, int depth, Dictionary<string, List<string>> childrenById, Dictionary<string, Vector2> grid, ref int nextRow)
		{
			if (grid.TryGetValue(id, out Vector2 placed))
			{
				return placed.y;
			}
			// 고리에서 다시 들어오면 멈추도록 먼저 자리를 잡아 둠
			grid[id] = new Vector2(depth, nextRow);

			List<string> children = childrenById[id];
			float row;
			if (children.Count == 0)
			{
				row = nextRow;
				nextRow++;
			}
			else
			{
				float minRow = float.MaxValue;
				float maxRow = float.MinValue;
				foreach (string childId in children)
				{
					float childRow = PlaceInGrid(childId, depth + 1, childrenById, grid, ref nextRow);
					minRow = Mathf.Min(minRow, childRow);
					maxRow = Mathf.Max(maxRow, childRow);
				}
				row = (minRow + maxRow) * 0.5f;
			}
			grid[id] = new Vector2(depth, row);
			return row;
		}
	}
}
