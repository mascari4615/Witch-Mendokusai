namespace WitchMendokusai
{
	/// <summary>
	/// 업적 하나가 방금 한 단계 올랐음. 여러 단계를 한 번에 건너뛰면 단계마다 하나씩 나감.
	/// record 가 아닌 이유: WM.Core 에는 IsExternalInit 이 없어 record 를 못 씀 (DomainSDK 조각만 가짐)
	/// </summary>
	public sealed class AchievementUnlockedEvent
	{
		public int AchievementId { get; }
		public int Tier { get; }

		public AchievementUnlockedEvent(int achievementId, int tier)
		{
			AchievementId = achievementId;
			Tier = tier;
		}

		public AchievementStep Step => new(AchievementId, Tier);
	}
}
