using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 설정 창 키보드 초점 이동 (표 줄과 열, 버튼 줄)
	public partial class UIDungeonConstraintPopup
	{
		// 표 안은 줄과 열, 맨 아래 줄에서 아래로 가면 버튼 줄. Tab 은 순서대로. 끝에서 멈춤
		private void OnNavigationMove(NavigationMoveEvent evt)
		{
			if (confirm.IsOpen)
				return;
			int next = focusedOrder;
			bool onFoot = focusedOrder >= cells.Count;
			Cell current = onFoot ? null : cells[focusedOrder];
			switch (evt.direction)
			{
				case NavigationMoveEvent.Direction.Previous:
					next = Mathf.Max(0, focusedOrder - 1);
					break;
				case NavigationMoveEvent.Direction.Next:
					next = Mathf.Min(cells.Count + footButtons.Count - 1, focusedOrder + 1);
					break;
				case NavigationMoveEvent.Direction.Left:
					next = onFoot ? Mathf.Max(cells.Count, focusedOrder - 1) : OrderOf(FindInRow(current.Row, current.Column, -1)) ?? focusedOrder;
					break;
				case NavigationMoveEvent.Direction.Right:
					next = onFoot ? Mathf.Min(cells.Count + footButtons.Count - 1, focusedOrder + 1) : OrderOf(FindInRow(current.Row, current.Column, 1)) ?? focusedOrder;
					break;
				case NavigationMoveEvent.Direction.Up:
					next = onFoot ? (lastCell != null ? cells.IndexOf(lastCell) : focusedOrder) : OrderOf(FindInColumn(current.Row, current.Column, -1)) ?? focusedOrder;
					break;
				case NavigationMoveEvent.Direction.Down:
					next = onFoot ? focusedOrder : OrderOf(FindInColumn(current.Row, current.Column, 1)) ?? cells.Count;
					break;
				default:
					return;
			}
			// 창 밖 (뒤 입장 창) 으로 초점이 새지 않게 모든 이동을 여기서 끝냄
			evt.StopPropagation();
			focusController?.IgnoreEvent(evt);
			if (next != focusedOrder)
				FocusOrder(next, true);
		}

		private int? OrderOf(Cell cell) => cell == null ? null : cells.IndexOf(cell);

		private Cell FindInRow(int row, int column, int step)
		{
			for (int scanColumn = column + step; scanColumn >= 0 && scanColumn < grid.GetLength(1); scanColumn += step)
			{
				if (grid[row, scanColumn] != null)
					return grid[row, scanColumn];
			}
			return null;
		}

		// 위아래 줄에서 같은 열, 없으면 가장 가까운 열
		private Cell FindInColumn(int row, int column, int step)
		{
			for (int scanRow = row + step; scanRow >= 0 && scanRow < grid.GetLength(0); scanRow += step)
			{
				Cell best = null;
				int bestDistance = int.MaxValue;
				for (int scanColumn = 0; scanColumn < grid.GetLength(1); scanColumn++)
				{
					if (grid[scanRow, scanColumn] == null)
						continue;
					int distance = Mathf.Abs(scanColumn - column);
					if (distance < bestDistance)
					{
						best = grid[scanRow, scanColumn];
						bestDistance = distance;
					}
				}
				if (best != null)
					return best;
			}
			return null;
		}

		private void FocusOrder(int order, bool withSound)
		{
			order = Mathf.Clamp(order, 0, cells.Count + footButtons.Count - 1);
			focusedOrder = order;
			Button target = order < cells.Count ? cells[order].Tile : footButtons[order - cells.Count];
			if (withSound)
				WMFocusNavigation.MoveFocus(target);
			else
				target.Focus();
		}
	}
}
