using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 방향 키 칸 이동. 진행 방향 거리와 수직 어긋남 합산이 가장 작은 칸
	public static class WMFocusNavigation
	{
		public static VisualElement FindNext(VisualElement current, IReadOnlyList<VisualElement> candidates, NavigationMoveEvent.Direction direction, float overlapTolerance, float acrossWeight)
		{
			Rect from = current.worldBound;
			VisualElement best = null;
			float bestScore = float.MaxValue;
			foreach (VisualElement candidate in candidates)
			{
				if (candidate == current)
					continue;
				Rect to = candidate.worldBound;
				float along;
				float across;
				switch (direction)
				{
					case NavigationMoveEvent.Direction.Up:
						along = from.yMin - to.yMax;
						across = Mathf.Abs(from.center.x - to.center.x);
						break;
					case NavigationMoveEvent.Direction.Down:
						along = to.yMin - from.yMax;
						across = Mathf.Abs(from.center.x - to.center.x);
						break;
					case NavigationMoveEvent.Direction.Left:
						along = from.xMin - to.xMax;
						across = Mathf.Abs(from.center.y - to.center.y);
						break;
					case NavigationMoveEvent.Direction.Right:
						along = to.xMin - from.xMax;
						across = Mathf.Abs(from.center.y - to.center.y);
						break;
					default:
						continue;
				}
				if (along < -overlapTolerance)
					continue;
				float score = along + across * acrossWeight;
				if (score < bestScore)
				{
					bestScore = score;
					best = candidate;
				}
			}
			return best;
		}
	}
}
