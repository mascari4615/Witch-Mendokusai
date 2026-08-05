using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 코어까지 가는 길 안내판(TASK-WM-194) — 판 전체 칸마다 「코어 쪽으로 가려면 어디로」를 미리 계산해 둔다.
	///
	/// ★ 왜 필요한가: 지형(암반)이 생기는 순간, 목표를 향해 직선으로 걷던 마수는 벽에 얼굴을 박고 멈춘다.
	///   그러면 웨이브가 영원히 안 끝나는 그 사고(이미 한 번 겪음)가 지형을 넣자마자 되돌아온다.
	///   길목이 재미의 핵심이므로, 길목을 만들려면 길을 *돌아갈 줄 아는* 이동이 선행 조건이다.
	///
	/// ★ 왜 흐름장인가: 마수 각자가 경로를 찾으면 N마리 × 매 프레임이 된다. 코어에서 한 번 BFS 로 퍼뜨려
	///   칸마다 방향을 굳혀두면, 마수는 자기 칸의 화살표를 읽기만 하면 된다(마릿수와 무관한 비용).
	///   웨이브마다 판이 안 바뀌므로 매치당 1회 계산으로 충분하다.
	///
	/// 대각선 이동 허용 — 단, 두 직교 이웃이 모두 막혀 있으면 벽 모서리를 뚫는 셈이라 금지한다.
	/// 순수 정적(Unity 씬 0, RNG 0) — EditMode 로 전량 검증.
	/// </summary>
	public sealed class TowerDefenseFlowField
	{
		private const int UNREACHABLE = int.MaxValue;

		private readonly int width;
		private readonly int length;
		private readonly int[] distance;      // 코어까지 걸음 수(격자 기준). UNREACHABLE = 못 감.
		private readonly Vector2Int[] nextStep; // 이 칸에서 다음에 밟을 칸.

		public Vector2Int GoalCell { get; }

		/// <summary> 코어(goal)에서 BFS 로 퍼뜨려 만든다. isBlocked = 그 칸이 통행 불가인지. </summary>
		public TowerDefenseFlowField(int width, int length, Vector2Int goalCell, System.Func<Vector2Int, bool> isBlocked)
			: this(width, length, new[] { goalCell }, isBlocked)
		{
		}

		/// <summary>
		/// 목표가 여럿일 때(코어 + 전초기지) — 모든 목표에서 동시에 퍼뜨리면 각 칸은 *가장 가까운* 목표로 흐른다.
		/// 마수가 저절로 분산되고, 전초기지를 세우는 순간 「지킬 곳이 하나 더」가 된다.
		/// </summary>
		public TowerDefenseFlowField(int width, int length, IReadOnlyList<Vector2Int> goalCells, System.Func<Vector2Int, bool> isBlocked)
		{
			Vector2Int goalCell = goalCells != null && goalCells.Count > 0 ? goalCells[0] : Vector2Int.zero;
			this.width = width < 1 ? 1 : width;
			this.length = length < 1 ? 1 : length;
			GoalCell = goalCell;

			int cellCount = this.width * this.length;
			distance = new int[cellCount];
			nextStep = new Vector2Int[cellCount];
			for (int index = 0; index < cellCount; index++)
			{
				distance[index] = UNREACHABLE;
				nextStep[index] = goalCell;
			}

			Queue<Vector2Int> frontier = new();
			if (goalCells != null)
			{
				foreach (Vector2Int goal in goalCells)
				{
					if (IsInside(goal) == false)
						continue;
					distance[ToIndex(goal)] = 0;
					nextStep[ToIndex(goal)] = goal;
					frontier.Enqueue(goal);
				}
			}

			if (frontier.Count == 0)
				return;

			while (frontier.Count > 0)
			{
				Vector2Int current = frontier.Dequeue();
				int currentDistance = distance[ToIndex(current)];

				for (int direction = 0; direction < Neighbors.Length; direction++)
				{
					Vector2Int neighbor = current + Neighbors[direction];
					if (IsInside(neighbor) == false)
						continue;
					if (isBlocked != null && isBlocked(neighbor))
						continue;
					if (IsDiagonalCornerCut(neighbor, current, isBlocked))
						continue;

					int neighborIndex = ToIndex(neighbor);
					if (distance[neighborIndex] != UNREACHABLE)
						continue;

					distance[neighborIndex] = currentDistance + 1;
					nextStep[neighborIndex] = current; // 퍼져 나온 쪽 = 코어로 가는 쪽.
					frontier.Enqueue(neighbor);
				}
			}
		}

		/// <summary> 그 칸에서 코어까지 갈 수 있는지. </summary>
		public bool IsReachable(Vector2Int cell)
		{
			return IsInside(cell) && distance[ToIndex(cell)] != UNREACHABLE;
		}

		/// <summary> 코어까지 걸음 수(못 가면 -1). 진단·검증용. </summary>
		public int DistanceTo(Vector2Int cell)
		{
			if (IsReachable(cell) == false)
				return -1;
			return distance[ToIndex(cell)];
		}

		/// <summary> 이 칸에서 다음에 밟을 칸(못 가면 false). </summary>
		public bool TryGetNextCell(Vector2Int cell, out Vector2Int next)
		{
			next = GoalCell;
			if (IsReachable(cell) == false)
				return false;
			next = nextStep[ToIndex(cell)];
			return true;
		}

		/// <summary>
		/// 같은 값의 「제일 가까운 이웃」이 여럿일 때 그중 하나를 <paramref name="pick"/>(0~1)로 고른다.
		///
		/// ★ 왜 필요한가 (사용자 실측: "여전히 거의 한 줄. 길찾기 알고리즘 좀 씁시다"):
		///   격자에서는 최단 경로가 *여러 개*인 것이 보통이다. 그런데 미리 계산해 둔 다음 칸은 그중
		///   딱 하나만 기억하므로, 사방에서 출발해도 전부 같은 칸들을 밟아 한 줄이 된다.
		///   길찾기가 틀린 게 아니라 **여러 최단 경로 중 하나만 쓰고 있던 것**이다.
		///   개체마다 다른 값을 주면 같은 최단 거리를 유지한 채 서로 다른 길로 흩어진다 —
		///   길이 짧아지지도 않고, 벽도 그대로 돈다. 넓은 면으로 밀려오는 그림이 여기서 나온다.
		/// </summary>
		public bool TryGetNextCell(Vector2Int cell, float pick, out Vector2Int next)
		{
			next = GoalCell;
			if (IsReachable(cell) == false)
				return false;

			int here = distance[ToIndex(cell)];
			int best = int.MaxValue;
			int count = 0;
			for (int i = 0; i < Neighbors.Length; i++)
			{
				Vector2Int neighbor = cell + Neighbors[i];
				if (IsInside(neighbor) == false)
					continue;
				int value = distance[ToIndex(neighbor)];
				if (value < 0 || value >= here)
					continue; // 목표에서 멀어지는 칸은 후보가 아니다.
				if (value < best)
				{
					best = value;
					count = 1;
				}
				else if (value == best)
				{
					count++;
				}
			}

			if (count == 0)
				return TryGetNextCell(cell, out next);

			int wanted = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(pick) * count), 0, count - 1);
			int seen = 0;
			for (int i = 0; i < Neighbors.Length; i++)
			{
				Vector2Int neighbor = cell + Neighbors[i];
				if (IsInside(neighbor) == false)
					continue;
				if (distance[ToIndex(neighbor)] != best)
					continue;
				if (seen == wanted)
				{
					next = neighbor;
					return true;
				}
				seen++;
			}

			return TryGetNextCell(cell, out next);
		}

		private bool IsInside(Vector2Int cell)
		{
			return cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < length;
		}

		private int ToIndex(Vector2Int cell)
		{
			return cell.y * width + cell.x;
		}

		// 대각선으로 건널 때 양옆이 둘 다 벽이면 벽 모서리를 뚫고 지나가는 그림이 된다 — 물리로는 낄 자리.
		private static bool IsDiagonalCornerCut(Vector2Int from, Vector2Int to, System.Func<Vector2Int, bool> isBlocked)
		{
			int deltaX = to.x - from.x;
			int deltaY = to.y - from.y;
			if (deltaX == 0 || deltaY == 0)
				return false;
			if (isBlocked == null)
				return false;

			return isBlocked(new Vector2Int(from.x + deltaX, from.y)) && isBlocked(new Vector2Int(from.x, from.y + deltaY));
		}

		private static readonly Vector2Int[] Neighbors =
		{
			new Vector2Int(1, 0),
			new Vector2Int(-1, 0),
			new Vector2Int(0, 1),
			new Vector2Int(0, -1),
			new Vector2Int(1, 1),
			new Vector2Int(1, -1),
			new Vector2Int(-1, 1),
			new Vector2Int(-1, -1),
		};
	}
}
