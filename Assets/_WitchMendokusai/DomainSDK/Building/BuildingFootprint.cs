using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	/// <summary>
	/// 건물이 <b>몇 칸을 깔고 앉는가</b> (TASK-WM-215).
	///
	/// 여러 칸짜리 건물은 기준 칸(pivot)에서 <b>-X, +Z</b> 방향으로 퍼진다.
	/// 이 규칙이 갈리면 서버는 「빈 자리」라고 하고 화면은 「겹친다」고 한다 —
	/// 공유 건설(여럿이 같이 짓기)에서 곧바로 사고가 된다.
	/// </summary>
	public static class BuildingFootprint
	{
		/// <summary>기준 칸과 크기로 차지하는 칸 목록을 만든다.</summary>
		public static List<Vector3Int> Cells(Vector3Int pivot, Vector2Int size)
		{
			List<Vector3Int> cells = new List<Vector3Int>();

			for (int x = 0; x < size.x; x++)
			{
				for (int z = 0; z < size.y; z++)
				{
					cells.Add(pivot + new Vector3Int(-x, 0, z));
				}
			}

			return cells;
		}

		/// <summary>차지할 칸 중 하나라도 이미 누가 쓰고 있으면 못 짓는다.</summary>
		public static bool IsBlocked(Vector3Int pivot, Vector2Int size, ICollection<Vector3Int> occupiedCells)
		{
			if (occupiedCells == null)
				return false;

			List<Vector3Int> cells = Cells(pivot, size);
			for (int i = 0; i < cells.Count; i++)
			{
				if (occupiedCells.Contains(cells[i]))
					return true;
			}

			return false;
		}
	}
}
