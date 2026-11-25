using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>불 하나를 세울 자리. 가운데와 폭 (미터, 바닥 평면)</summary>
	public readonly struct GoopFireSpot
	{
		public readonly Vector2 Center;
		public readonly float Width;

		public GoopFireSpot(Vector2 center, float width)
		{
			Center = center;
			Width = width;
		}
	}

	/// <summary>불 자리를 나누는 기준</summary>
	public readonly struct GoopFireRules
	{
		public readonly float CellSize;
		public readonly float SplitAspect;
		public readonly float MinFill;
		public readonly float MinWidth;
		public readonly float MaxWidth;
		public readonly int MaxSpots;

		public GoopFireRules(float cellSize, float splitAspect, float minFill, float minWidth, float maxWidth, int maxSpots)
		{
			CellSize = cellSize;
			SplitAspect = splitAspect;
			MinFill = minFill;
			MinWidth = minWidth;
			MaxWidth = maxWidth;
			MaxSpots = maxSpots;
		}
	}

	/// <summary>
	/// 타는 칸을 이웃끼리 (대각 포함) 묶어 불 자리로 바꿈. 그림과 무관한 계산만
	/// - 덩이가 넓거나 길쭉하거나 속이 비었으면 (휜 줄, 고리) 긴 축의 가운데에서 반으로 나누기를 되풀이. 동그란 덩이는 불 하나
	/// - 자리가 상한보다 많으면 주인공에게 가까운 것부터
	/// </summary>
	public sealed class GoopFireSpots
	{
		private readonly HashSet<Vector2Int> remaining = new();
		private readonly Stack<Vector2Int> stack = new();
		private readonly List<Vector2> points = new();

		public void Build(List<Vector2Int> burningCells, in GoopFireRules rules, Vector2 focus, List<GoopFireSpot> spots)
		{
			spots.Clear();
			remaining.Clear();
			foreach (Vector2Int cell in burningCells)
				remaining.Add(cell);
			foreach (Vector2Int cell in burningCells)
			{
				if (remaining.Remove(cell) == false)
					continue;
				CollectClump(cell, rules.CellSize);
				Divide(0, points.Count, rules, spots);
			}
			if (spots.Count <= rules.MaxSpots)
				return;
			spots.Sort((left, right) => (left.Center - focus).sqrMagnitude.CompareTo((right.Center - focus).sqrMagnitude));
			spots.RemoveRange(rules.MaxSpots, spots.Count - rules.MaxSpots);
		}

		private void CollectClump(Vector2Int start, float cellSize)
		{
			points.Clear();
			stack.Clear();
			stack.Push(start);
			while (stack.Count > 0)
			{
				Vector2Int cell = stack.Pop();
				points.Add(new Vector2((cell.x + .5f) * cellSize, (cell.y + .5f) * cellSize));
				for (int x = -1; x <= 1; x++)
				{
					for (int y = -1; y <= 1; y++)
					{
						Vector2Int next = cell + new Vector2Int(x, y);
						if (remaining.Remove(next))
							stack.Push(next);
					}
				}
			}
		}

		// points 의 [from, to) 를 불 하나로 두거나, 긴 축의 가운데에서 둘로 갈라 되풀이
		private void Divide(int from, int to, in GoopFireRules rules, List<GoopFireSpot> spots)
		{
			Vector2 center = CenterOf(from, to);
			Vector2 longAxis = LongAxisOf(from, to, center);
			MeasureAlong(from, to, longAxis, center, out float longMin, out float longMax);
			MeasureAlong(from, to, new Vector2(-longAxis.y, longAxis.x), center, out float shortMin, out float shortMax);
			float longExtent = longMax - longMin + rules.CellSize;
			float shortExtent = shortMax - shortMin + rules.CellSize;
			float filled = (to - from) * rules.CellSize * rules.CellSize / (longExtent * shortExtent);

			bool wide = longExtent > rules.MaxWidth;
			bool uneven = longExtent > Mathf.Max(shortExtent, rules.MinWidth) * rules.SplitAspect || filled < rules.MinFill;
			bool divisible = longExtent >= rules.MinWidth * 2f;
			if (divisible == false || (wide == false && uneven == false))
			{
				spots.Add(new GoopFireSpot(center, Mathf.Min(longExtent, rules.MaxWidth)));
				return;
			}
			int middle = Partition(from, to, longAxis, Vector2.Dot(center, longAxis) + (longMin + longMax) * .5f);
			Divide(from, middle, rules, spots);
			Divide(middle, to, rules, spots);
		}

		private Vector2 CenterOf(int from, int to)
		{
			Vector2 sum = Vector2.zero;
			for (int i = from; i < to; i++)
				sum += points[i];
			return sum / (to - from);
		}

		// 공분산의 큰 쪽 축
		private Vector2 LongAxisOf(int from, int to, Vector2 center)
		{
			float spreadX = 0f;
			float spreadY = 0f;
			float spreadXY = 0f;
			for (int i = from; i < to; i++)
			{
				Vector2 offset = points[i] - center;
				spreadX += offset.x * offset.x;
				spreadY += offset.y * offset.y;
				spreadXY += offset.x * offset.y;
			}
			float angle = .5f * Mathf.Atan2(2f * spreadXY, spreadX - spreadY);
			return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
		}

		// 축 위 자리가 split 보다 작은 점을 앞으로 모음. 양 끝이 split 의 다른 쪽이라 둘 다 비지 않음
		private int Partition(int from, int to, Vector2 axis, float split)
		{
			int middle = from;
			for (int i = from; i < to; i++)
			{
				if (Vector2.Dot(points[i], axis) >= split)
					continue;
				(points[middle], points[i]) = (points[i], points[middle]);
				middle++;
			}
			return middle;
		}

		private void MeasureAlong(int from, int to, Vector2 axis, Vector2 center, out float min, out float max)
		{
			min = float.PositiveInfinity;
			max = float.NegativeInfinity;
			for (int i = from; i < to; i++)
			{
				float along = Vector2.Dot(points[i] - center, axis);
				min = Mathf.Min(min, along);
				max = Mathf.Max(max, along);
			}
		}
	}
}
