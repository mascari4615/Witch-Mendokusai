using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Aspect
{
	/// <summary>방향 키</summary>
	public enum BoardArrow
	{
		Left = 0,
		Right = 1,
		Up = 2,
		Down = 3,
	}

	/// <summary>
	/// 판 안의 방향 키 이동. 이웃 칸의 사각 경계가 서로 겹쳐 화면 자리로는 옆 줄을 못 찾음.
	/// 격자로 정한 후보를 가까운 순서로 내고, 부른 쪽이 실제로 있는 첫 칸을 고름 (찢어진 칸은 건너뜀)
	/// - 왼쪽과 오른쪽: 같은 가로줄
	/// - 아래: 오른쪽 아래, 왼쪽 아래, 두 줄 아래. 위: 왼쪽 위, 오른쪽 위, 두 줄 위 (아래로 갔다 위로 오면 제자리)
	/// </summary>
	public static class BoardArrows
	{
		public static IEnumerable<HexCoord> Hex(HexCoord from, BoardArrow arrow, int radius)
		{
			switch (arrow)
			{
				case BoardArrow.Left:
				case BoardArrow.Right:
					int step = arrow == BoardArrow.Right ? 1 : -1;
					for (int i = 1; i <= radius * 2; i++)
					{
						yield return new HexCoord(from.Q + step * i, from.R);
					}
					break;
				case BoardArrow.Down:
					yield return new HexCoord(from.Q, from.R + 1);
					yield return new HexCoord(from.Q - 1, from.R + 1);
					yield return new HexCoord(from.Q - 1, from.R + 2);
					break;
				case BoardArrow.Up:
					yield return new HexCoord(from.Q, from.R - 1);
					yield return new HexCoord(from.Q + 1, from.R - 1);
					yield return new HexCoord(from.Q + 1, from.R - 2);
					break;
			}
		}

		public static IEnumerable<int> Diamond(int from, BoardArrow arrow)
		{
			foreach ((int stepX, int stepY) in DiamondSteps(arrow))
			{
				int x = TurnGrid.X(from) + stepX;
				int y = TurnGrid.Y(from) + stepY;
				if (x >= 0 && y >= 0 && x < TurnGrid.SIZE && y < TurnGrid.SIZE)
				{
					yield return y * TurnGrid.SIZE + x;
				}
			}
		}

		private static IEnumerable<(int X, int Y)> DiamondSteps(BoardArrow arrow)
		{
			switch (arrow)
			{
				case BoardArrow.Left:
				case BoardArrow.Right:
					int step = arrow == BoardArrow.Right ? 1 : -1;
					for (int i = 1; i < TurnGrid.SIZE; i++)
					{
						yield return (step * i, -step * i);
					}
					break;
				case BoardArrow.Down:
					yield return (1, 0);
					yield return (0, 1);
					yield return (1, 1);
					break;
				case BoardArrow.Up:
					yield return (-1, 0);
					yield return (0, -1);
					yield return (-1, -1);
					break;
			}
		}
	}
}
