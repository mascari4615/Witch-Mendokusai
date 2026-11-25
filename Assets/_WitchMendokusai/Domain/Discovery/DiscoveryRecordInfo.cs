namespace WitchMendokusai
{
	/// <summary>카드 한 장에 얹는 기록 상태. 단계 마름모, 아래 한 줄, 새 기록 표시</summary>
	public readonly struct DiscoveryRecordInfo
	{
		public readonly int Stage;
		public readonly int MaxStage;
		public readonly string Line;
		public readonly bool HasNews;

		public DiscoveryRecordInfo(int stage, int maxStage, string line, bool hasNews)
		{
			Stage = stage;
			MaxStage = maxStage;
			Line = line;
			HasNews = hasNews;
		}

		public bool IsComplete => MaxStage > 0 && Stage >= MaxStage;
	}

	/// <summary>항목마다 기록 단계가 있는 분류. 없는 분류는 카드에 그림과 이름만</summary>
	public interface IDiscoveryRecordProvider
	{
		DiscoveryRecordInfo RecordOf(EntryDescriptor entry);
	}
}
