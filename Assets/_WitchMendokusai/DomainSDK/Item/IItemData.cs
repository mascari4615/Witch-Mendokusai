namespace WitchMendokusai
{
	public interface IItemData
	{
		int ID { get; }
		int MaxAmount { get; }
		ItemType Type { get; }
		ItemGrade Grade { get; }
		bool IsCountable => MaxAmount != 1;
	}
}
