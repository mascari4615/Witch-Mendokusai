using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary> 칸 하나의 시야 상태 — 문명식 3단계. </summary>
	public enum TowerDefenseVisionState
	{
		Unseen = 0,   // 한 번도 안 가봤다 — 지형조차 모른다.
		Explored = 1, // 밝혔었다 — 지형은 기억하지만 지금 무슨 일이 벌어지는지는 모른다.
		Visible = 2,  // 지금 보인다.
	}

	/// <summary>
	/// 시야(TASK-WM-194) — 「안 가본 곳 / 갔지만 지금 안 보임 / 지금 보임」 3단계.
	///
	/// ★ 왜 전투에도 거는가: 보기만 예쁘게 가리면 규칙은 그대로라 판단이 안 바뀐다. 안 보이는 마수를
	///   포탑이 못 쏘게 해야 「시야를 넓히는 것」 자체가 배치 판단이 된다(사용자 선택). 개척이 곧
	///   시야 확장이 되어 먼 노드로 나갈 이유가 하나 더 생긴다.
	///
	/// 시야원(源) = 내 건물들. 건물은 움직이지 않으므로 *지어질 때만* 다시 계산하면 된다(매 프레임 X).
	/// 밝혀진 기억(Explored)은 절대 되돌아가지 않는다 — 한 번 안 것을 잊는 게임은 억울하다.
	/// 순수 클래스(씬·RNG 0) — EditMode 로 전량 검증.
	/// </summary>
	public sealed class TowerDefenseVision
	{
		private readonly int width;
		private readonly int length;
		private readonly bool[] explored;
		private readonly bool[] visible;

		public int Width => width;
		public int Length => length;

		public TowerDefenseVision(int width, int length)
		{
			this.width = width < 1 ? 1 : width;
			this.length = length < 1 ? 1 : length;
			explored = new bool[this.width * this.length];
			visible = new bool[this.width * this.length];
		}

		/// <summary> 시야원 하나 — 위치(칸)와 반경(칸). </summary>
		public readonly struct Source
		{
			public readonly Vector2Int Cell;
			public readonly float Radius;

			public Source(Vector2Int cell, float radius)
			{
				Cell = cell;
				Radius = radius;
			}
		}

		/// <summary> 지금 보이는 범위를 다시 계산한다. Explored 는 누적(한 번 밝힌 곳은 계속 기억). </summary>
		/// <summary>
		/// 창이 자랐을 때 *밝힌 기록*을 새 크기로 옮긴다 — 새로 구우면 가봤던 곳이 통째로 어두워진다.
		/// 새 띠는 당연히 안 가본 곳이므로 그대로 둔다.
		/// </summary>
		public void CopyExploredFrom(TowerDefenseVision older)
		{
			if (older == null)
				return;

			int copyWidth = Mathf.Min(width, older.width);
			int copyLength = Mathf.Min(length, older.length);
			for (int x = 0; x < copyWidth; x++)
			{
				for (int y = 0; y < copyLength; y++)
					explored[y * width + x] = older.explored[y * older.width + x];
			}
		}

		public void Recompute(IReadOnlyList<Source> sources)
		{
			for (int index = 0; index < visible.Length; index++)
				visible[index] = false;

			if (sources == null)
				return;

			foreach (Source source in sources)
			{
				if (source.Radius <= 0f)
					continue;

				int radius = Mathf.CeilToInt(source.Radius);
				float radiusSqr = source.Radius * source.Radius;

				for (int offsetY = -radius; offsetY <= radius; offsetY++)
				{
					for (int offsetX = -radius; offsetX <= radius; offsetX++)
					{
						if (offsetX * offsetX + offsetY * offsetY > radiusSqr)
							continue;

						Vector2Int cell = new Vector2Int(source.Cell.x + offsetX, source.Cell.y + offsetY);
						if (IsInside(cell) == false)
							continue;

						int index = ToIndex(cell);
						visible[index] = true;
						explored[index] = true;
					}
				}
			}
		}

		public bool IsInside(Vector2Int cell)
		{
			return cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < length;
		}

		/// <summary> 판 밖은 「안 보임」으로 본다 — 밖에서 벌어지는 일을 알 수는 없다. </summary>
		public bool IsVisible(Vector2Int cell)
		{
			return IsInside(cell) && visible[ToIndex(cell)];
		}

		public bool IsExplored(Vector2Int cell)
		{
			return IsInside(cell) && explored[ToIndex(cell)];
		}

		public TowerDefenseVisionState StateAt(Vector2Int cell)
		{
			if (IsInside(cell) == false)
				return TowerDefenseVisionState.Unseen;
			int index = ToIndex(cell);
			if (visible[index])
				return TowerDefenseVisionState.Visible;
			return explored[index] ? TowerDefenseVisionState.Explored : TowerDefenseVisionState.Unseen;
		}

		private int ToIndex(Vector2Int cell)
		{
			return cell.y * width + cell.x;
		}
	}
}
