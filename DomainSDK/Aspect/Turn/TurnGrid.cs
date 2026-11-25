using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Aspect
{
	/// <summary>화면 기준 방향. 판은 정사각 격자를 45도 돌린 마름모</summary>
	public enum TurnSide
	{
		UpperRight = 0,
		LowerRight = 1,
		LowerLeft = 2,
		UpperLeft = 3,
	}

	/// <summary>번갈아 판의 격자. 칸 번호는 y * SIZE + x, 화면 자리는 (x - y, x + y)</summary>
	public static class TurnGrid
	{
		public const int SIZE = 5;
		public const int CELL_COUNT = SIZE * SIZE;

		/// <summary>왼쪽 위에서 시작하는 시계 방향</summary>
		public static readonly IReadOnlyList<TurnSide> CLOCKWISE = new[] { TurnSide.UpperLeft, TurnSide.UpperRight, TurnSide.LowerRight, TurnSide.LowerLeft };

		public static int X(int cell) => cell % SIZE;

		public static int Y(int cell) => cell / SIZE;

		public static bool Contains(int cell) => cell >= 0 && cell < CELL_COUNT;

		/// <summary>side 쪽으로 count 칸 옮긴 칸. 판 밖이면 false</summary>
		public static bool TryStep(int cell, TurnSide side, int count, out int target)
		{
			(int stepX, int stepY) = StepOf(side);
			int x = X(cell) + stepX * count;
			int y = Y(cell) + stepY * count;
			bool isInside = x >= 0 && y >= 0 && x < SIZE && y < SIZE;
			target = isInside ? y * SIZE + x : -1;
			return isInside;
		}

		public static IEnumerable<int> Neighbors(int cell)
		{
			foreach (TurnSide side in CLOCKWISE)
			{
				if (TryStep(cell, side, 1, out int neighbor))
				{
					yield return neighbor;
				}
			}
		}

		public static int Distance(int from, int to) => Math.Abs(X(from) - X(to)) + Math.Abs(Y(from) - Y(to));

		private static (int X, int Y) StepOf(TurnSide side) => side switch
		{
			TurnSide.UpperRight => (0, -1),
			TurnSide.LowerRight => (1, 0),
			TurnSide.LowerLeft => (0, 1),
			TurnSide.UpperLeft => (-1, 0),
			_ => throw new ArgumentOutOfRangeException(nameof(side), side, null),
		};
	}
}
