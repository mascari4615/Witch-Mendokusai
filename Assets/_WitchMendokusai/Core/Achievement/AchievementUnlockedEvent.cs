namespace WitchMendokusai
{
	/// <summary>업적 하나가 방금 한 단계 올랐음. 여러 단계를 한 번에 건너뛰면 단계마다 하나씩 나감</summary>
	public record AchievementUnlockedEvent(int AchievementId, int Tier)
	{
		public AchievementStep Step => new(AchievementId, Tier);
	}
}
