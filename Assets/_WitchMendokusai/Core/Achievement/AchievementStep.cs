using System;

namespace WitchMendokusai
{
	/// <summary>
	/// 업적 하나의 한 단계. 장부와 플랫폼 연결표가 함께 쓰는 열쇠.
	/// 단계는 1부터. 3단계에 닿았으면 1, 2, 3 단계가 모두 달성된 것
	/// </summary>
	[Serializable]
	public readonly struct AchievementStep : IEquatable<AchievementStep>
	{
		public int AchievementId { get; }
		public int Tier { get; }

		public AchievementStep(int achievementId, int tier)
		{
			AchievementId = achievementId;
			Tier = tier;
		}

		public bool Equals(AchievementStep other) => AchievementId == other.AchievementId && Tier == other.Tier;

		public override bool Equals(object obj) => obj is AchievementStep other && Equals(other);

		// HashCode.Combine 은 .NET Standard 2.1 (이 프로젝트는 2.0 고정)
		public override int GetHashCode() => (AchievementId * 397) ^ Tier;

		public override string ToString() => AchievementId + "." + Tier;
	}
}
