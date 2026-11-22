using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// 레벨업 덱 제시 가중치의 정본. 이번 판에 그 덱에서 받은 카드 수로 가중치, 빌드를 몇 계열로 모음
	public static class DeckOfferWeights
	{
		// weightsByPicks[n] 은 카드를 n장 받은 덱의 가중치. 표보다 많이 받으면 마지막 값. 표가 비면 모두 1
		public static float Weight(IReadOnlyList<float> weightsByPicks, int pickCount)
		{
			if (weightsByPicks == null || weightsByPicks.Count == 0)
				return 1f;
			int index = Math.Min(Math.Max(pickCount, 0), weightsByPicks.Count - 1);
			return weightsByPicks[index];
		}

		// 가중치 비복원 추출로 순서 결정. 앞쪽부터 화면에 보임
		// 0 이하 가중치는 양수 덱이 다 나온 뒤 맨 뒤, 그들끼리는 균등. 덱이 목록에서 빠지지는 않음
		// nextIndex: 0 이상 인자 미만의 난수
		public static List<int> WeightedOrder(IReadOnlyList<int> decks, Func<int, float> weightOf, Func<int, int> nextIndex)
		{
			List<int> remaining = new List<int>(decks);
			List<double> weights = new List<double>(remaining.Count);
			foreach (int deck in remaining)
				weights.Add(Sanitize(weightOf(deck)));

			List<int> ordered = new List<int>(remaining.Count);
			while (remaining.Count > 0)
			{
				int picked = PickIndex(weights, nextIndex);
				ordered.Add(remaining[picked]);
				remaining.RemoveAt(picked);
				weights.RemoveAt(picked);
			}
			return ordered;
		}

		private static int PickIndex(List<double> weights, Func<int, int> nextIndex)
		{
			double total = 0d;
			foreach (double weight in weights)
				total += weight;
			if (total <= 0d)
				return nextIndex(weights.Count);

			double roll = nextIndex(int.MaxValue) / (double)int.MaxValue * total;
			for (int i = 0; i < weights.Count; i++)
			{
				roll -= weights[i];
				if (roll < 0d)
					return i;
			}
			// 부동소수 오차로 끝까지 온 경우, 양수 가중치 중 마지막
			return weights.FindLastIndex(weight => weight > 0d);
		}

		private static double Sanitize(float weight) => float.IsNaN(weight) || float.IsInfinity(weight) || weight <= 0f ? 0d : weight;
	}
}
