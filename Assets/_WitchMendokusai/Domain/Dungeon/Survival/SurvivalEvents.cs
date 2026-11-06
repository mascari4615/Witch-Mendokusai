namespace WitchMendokusai
{
	public struct SurvivalEventAnnouncedEvent
	{
		public SurvivalEventType Type;
		public float StartTime;
		public float LeadSeconds;
		public Monster Monster;
		public int Count;
	}

	public struct SurvivalEventStartedEvent
	{
		public SurvivalEventType Type;
		public Monster Monster;
		public int Count;
	}

	public struct EliteKilledEvent
	{
		public Monster Monster;
		public int ExpValue;
	}

	public struct BossSpawnedEvent
	{
		public Monster Monster;
	}

	// 고를 카드가 없어 레벨업이 회복이나 냥으로 바뀐 순간
	public struct LevelUpConvertedEvent
	{
		public int Heal;
		public int Nyang;
	}
}
