using System;
using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Discovery;

namespace WitchMendokusai
{
	/// <summary>재는 값에서 단계를 고르는 규칙. 엔진 없음</summary>
	public static class AchievementTiers
	{
		public const int MAX_TIER_COUNT = 3;

		private static readonly string[] TIER_NAMES = { "동", "은", "금" };

		/// <summary>단계 이름 (동, 은, 금). 단계가 하나뿐인 업적은 이름 없음 (빈 글자)</summary>
		public static string NameOf(int tier, int maxTier)
		{
			if (maxTier <= 1 || tier <= 0)
			{
				return string.Empty;
			}
			return TIER_NAMES[tier - 1];
		}

		/// <summary>값이 닿은 가장 높은 단계. 아무것도 못 닿으면 0</summary>
		public static int Resolve(int value, IReadOnlyList<int> targets) => DiscoveryStages.Resolve(value, targets);

		/// <summary>옛 세이브에서 옮길 단계가 그 업적의 단계 안인지 (1 ~ 단계 수)</summary>
		public static void ValidateLegacyTier(string owner, int legacyTier, int maxTier)
		{
			if (legacyTier < 1 || legacyTier > maxTier)
			{
				throw new ArgumentException($"{owner}: 옛 세이브 단계 {legacyTier} 는 1~{maxTier} 밖");
			}
		}

		/// <summary>목표값 표가 1~3개, 0보다 크고, 오름차순인지. 틀리면 자산 이름과 함께 던짐 (조용히 0단계로 남지 않게)</summary>
		public static void Validate(string owner, IReadOnlyList<int> targets)
		{
			if (targets == null || targets.Count == 0 || targets.Count > MAX_TIER_COUNT)
			{
				throw new ArgumentException($"{owner}: 단계 목표값은 1~{MAX_TIER_COUNT}개");
			}
			DiscoveryStages.Validate(owner, targets);
		}
	}
}
