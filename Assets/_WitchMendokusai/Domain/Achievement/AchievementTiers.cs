using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>재는 값에서 단계를 고르는 규칙. 엔진 없음</summary>
	public static class AchievementTiers
	{
		public const int MAX_TIER_COUNT = 3;

		/// <summary>값이 닿은 가장 높은 단계. 아무것도 못 닿으면 0</summary>
		public static int Resolve(int value, IReadOnlyList<int> targets)
		{
			int tier = 0;
			for (int i = 0; i < targets.Count; i++)
			{
				if (value < targets[i])
				{
					break;
				}
				tier = i + 1;
			}
			return tier;
		}

		/// <summary>목표값 표가 1~3개, 0보다 크고, 오름차순인지. 틀리면 자산 이름과 함께 던짐 (조용히 0단계로 남지 않게)</summary>
		public static void Validate(string owner, IReadOnlyList<int> targets)
		{
			if (targets == null || targets.Count == 0 || targets.Count > MAX_TIER_COUNT)
			{
				throw new ArgumentException($"{owner}: 단계 목표값은 1~{MAX_TIER_COUNT}개");
			}
			for (int i = 0; i < targets.Count; i++)
			{
				if (targets[i] <= 0)
				{
					throw new ArgumentException($"{owner}: 단계 목표값은 0보다 커야 함 ({targets[i]})");
				}
				if (i > 0 && targets[i] <= targets[i - 1])
				{
					throw new ArgumentException($"{owner}: 단계 목표값은 오름차순 ({targets[i - 1]} 다음 {targets[i]})");
				}
			}
		}
	}
}
