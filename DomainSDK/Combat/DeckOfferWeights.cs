using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// 레벨업 덱 제시 가중치의 정본. 이번 판에 그 덱에서 받은 카드 수로 가중치, 빌드를 몇 계열로 모음
	public static class DeckOfferWeights
	{
		// 가중치를 정수 칸으로 바꾸는 배율. 0.001 단위까지 구분
		private const int WEIGHT_UNITS = 1000;

		// weightsByPicks[n] 은 카드를 n장 받은 덱의 가중치. 표보다 많이 받으면 마지막 값. 표가 비면 모두 1
		public static float Weight(IReadOnlyList<float> weightsByPicks, int pickCount)
		{
			if (weightsByPicks == null || weightsByPicks.Count == 0)
				return 1f;
			int index = Math.Min(Math.Max(pickCount, 0), weightsByPicks.Count - 1);
			return weightsByPicks[index];
		}

		// 가중치 비복원 추출로 순서 결정. 앞쪽부터 화면에 보임
		// 0 이하 가중치도 최소 칸 하나, 덱이 사라지지 않음
		// nextIndex: 0 이상 인자 미만의 난수
		public static List<int> WeightedOrder(IReadOnlyList<int> decks, Func<int, float> weightOf, Func<int, int> nextIndex)
		{
			List<int> remaining = new List<int>(decks);
			List<int> units = new List<int>(remaining.Count);
			foreach (int deck in remaining)
				units.Add(ToUnits(weightOf(deck)));

			List<int> ordered = new List<int>(remaining.Count);
			while (remaining.Count > 0)
			{
				int total = 0;
				foreach (int unit in units)
					total += unit;

				int roll = nextIndex(total);
				int picked = remaining.Count - 1;
				for (int i = 0; i < remaining.Count; i++)
				{
					roll -= units[i];
					if (roll < 0)
					{
						picked = i;
						break;
					}
				}

				ordered.Add(remaining[picked]);
				remaining.RemoveAt(picked);
				units.RemoveAt(picked);
			}
			return ordered;
		}

		private static int ToUnits(float weight)
		{
			if (float.IsNaN(weight) || weight <= 0f)
				return 1;
			double scaled = Math.Round((double)weight * WEIGHT_UNITS);
			return (int)Math.Min(Math.Max(scaled, 1d), int.MaxValue / 64d);
		}
	}
}
