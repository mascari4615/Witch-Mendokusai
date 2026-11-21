using System.Collections.Generic;

namespace WitchMendokusai
{
	// 처치 시 상태 전염 규칙. 엔진 무관
	// - 상태 틱으로 죽은 유닛이 가까운 적에게 같은 상태를 퍼뜨림
	// - 연쇄는 두 상한으로 끊음. 세대 상한 (무기가 직접 건 상태가 0세대, 전염받으면 +1) 과 한 유닛이 받는 횟수 상한
	public static class StatusContagionRules
	{
		public static bool CanSpread(int generation, int maxGenerations) => maxGenerations > 0 && generation < maxGenerations;

		public static int NextGeneration(int generation) => generation + 1;

		public static bool CanReceive(int receivedCount, int maxReceives) => receivedCount < maxReceives;

		// 후보 중 반경 안이고 더 받을 수 있는 것을 가까운 순으로 maxTargets 개. 결과는 후보 번호
		public static void SelectTargets(
			IReadOnlyList<float> squaredDistances,
			IReadOnlyList<int> receivedCounts,
			float radius,
			int maxTargets,
			int maxReceives,
			List<int> result)
		{
			result.Clear();
			if (radius <= 0f || maxTargets <= 0 || squaredDistances == null)
				return;
			float radiusSquared = radius * radius;
			for (int i = 0; i < squaredDistances.Count; i++)
			{
				if (squaredDistances[i] > radiusSquared)
					continue;
				int received = receivedCounts != null && i < receivedCounts.Count ? receivedCounts[i] : 0;
				if (CanReceive(received, maxReceives) == false)
					continue;
				result.Add(i);
			}
			// 같은 거리는 번호 순으로 고정해 결과가 흔들리지 않게
			result.Sort((left, right) =>
			{
				int byDistance = squaredDistances[left].CompareTo(squaredDistances[right]);
				return byDistance != 0 ? byDistance : left.CompareTo(right);
			});
			if (result.Count > maxTargets)
				result.RemoveRange(maxTargets, result.Count - maxTargets);
		}
	}
}
