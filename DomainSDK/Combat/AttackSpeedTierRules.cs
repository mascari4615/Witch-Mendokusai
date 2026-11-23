using System;

namespace WitchMendokusai
{
	// 알리사 빛의 검의 공속 단계 정본. 베기 간격이 문턱을 넘을 때마다 검기 모양이 한 단계씩 바뀜
	// 1 두 갈래, 2 크기 증가, 3 끝에서 터짐 (사용자 결정 2026-11-22 화력 최종 카드)
	public static class AttackSpeedTierRules
	{
		// thresholds 는 단계 1 부터의 간격 문턱 (초), 내림차순. 간격이 문턱 이하이면 그 단계
		public static int Tier(float intervalSeconds, float[] thresholds)
		{
			int tier = 0;
			for (int i = 0; i < thresholds.Length; i++)
			{
				if (intervalSeconds <= thresholds[i])
					tier = i + 1;
			}
			return tier;
		}

		// 검기 길이 배율. 길이 카드 (스택당 10%) 와 최상 단계의 화면 끝 검기 (천상 마무리)
		public static float LengthMultiplier(int lengthStacks, float perStack, bool reachScreenEdge, float edgeMultiplier, int tier, int topTier)
		{
			float multiplier = 1f + Math.Max(0, lengthStacks) * perStack;
			if (reachScreenEdge && tier >= topTier)
				multiplier = Math.Max(multiplier, edgeMultiplier);
			return multiplier;
		}

		// 시간 한정 가속 (축복). 지금 살아 있는 쌓임 수를 상한으로 자름
		public static int ClampStacks(int liveStacks, int cap) => Math.Max(0, Math.Min(liveStacks, cap));
	}
}
