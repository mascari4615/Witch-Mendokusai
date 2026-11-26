using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Aspect
{
	/// <summary>인형이 자리를 고르는 버릇. 종류와 인형의 짝은 임시 (2026-11-25)</summary>
	public enum TurnHabitType
	{
		/// <summary>방금 놓은 칸의 옆, 왼쪽 위부터 시계 방향으로 첫 빈칸</summary>
		ClockwiseNear = 0,
		/// <summary>방금 놓은 칸의 옆 중 아직 안 이어진 그려진 칸에 가장 가까운 빈칸</summary>
		TowardAnchor = 1,
		/// <summary>한 칸을 사이에 둔 자리, 왼쪽 위부터 시계 방향으로 첫 빈칸</summary>
		ClockwiseSkip = 2,
	}

	/// <summary>버릇 하나. 새 버릇은 클래스 하나와 <see cref="TurnHabits"/> 한 줄</summary>
	public interface ITurnHabit
	{
		/// <summary>인형이 놓을 칸. 놓을 빈칸이 없으면 false (쉼)</summary>
		bool TryChoose(TurnBoard board, IReadOnlyDictionary<int, int> cells, int lastCell, out int chosen);
	}

	public static class TurnHabits
	{
		private static readonly Dictionary<TurnHabitType, ITurnHabit> HABITS = new Dictionary<TurnHabitType, ITurnHabit>
		{
			{ TurnHabitType.ClockwiseNear, new ClockwiseHabit(1) },
			{ TurnHabitType.TowardAnchor, new TowardAnchorHabit() },
			{ TurnHabitType.ClockwiseSkip, new ClockwiseHabit(2) },
		};

		public static ITurnHabit Get(TurnHabitType type) => HABITS[type];
	}

	/// <summary>방금 놓은 칸에서 정한 거리만큼 떨어진 네 자리를 시계 방향으로 봄</summary>
	public sealed class ClockwiseHabit : ITurnHabit
	{
		private readonly int reach;

		public ClockwiseHabit(int reach)
		{
			this.reach = reach;
		}

		public bool TryChoose(TurnBoard board, IReadOnlyDictionary<int, int> cells, int lastCell, out int chosen)
		{
			foreach (TurnSide side in TurnGrid.CLOCKWISE)
			{
				if (TurnGrid.TryStep(lastCell, side, reach, out chosen) && board.IsEmpty(chosen, cells))
				{
					return true;
				}
			}
			chosen = -1;
			return false;
		}
	}

	/// <summary>방금 놓은 칸의 옆 중 남은 그려진 칸에 가장 가까운 곳. 같으면 시계 방향으로 먼저</summary>
	public sealed class TowardAnchorHabit : ITurnHabit
	{
		public bool TryChoose(TurnBoard board, IReadOnlyDictionary<int, int> cells, int lastCell, out int chosen)
		{
			List<int> goals = GoalsLeft(board, cells, lastCell);
			chosen = -1;
			int nearest = int.MaxValue;
			foreach (TurnSide side in TurnGrid.CLOCKWISE)
			{
				if (TurnGrid.TryStep(lastCell, side, 1, out int cell) == false || board.IsEmpty(cell, cells) == false)
				{
					continue;
				}
				int distance = DistanceToNearest(cell, goals);
				if (distance < nearest)
				{
					nearest = distance;
					chosen = cell;
				}
			}
			return chosen >= 0;
		}

		private static List<int> GoalsLeft(TurnBoard board, IReadOnlyDictionary<int, int> cells, int lastCell)
		{
			HashSet<int> group = board.GroupFrom(lastCell, cells);
			List<int> goals = new List<int>();
			foreach (int anchor in board.Anchors.Keys)
			{
				if (group.Contains(anchor) == false)
				{
					goals.Add(anchor);
				}
			}
			return goals;
		}

		private static int DistanceToNearest(int cell, List<int> goals)
		{
			int nearest = int.MaxValue;
			foreach (int goal in goals)
			{
				nearest = Math.Min(nearest, TurnGrid.Distance(cell, goal));
			}
			return goals.Count == 0 ? 0 : nearest;
		}
	}
}
