using System;

namespace WitchMendokusai
{
	public interface IItemData
	{
		int ID { get; }
		int MaxAmount { get; }
		ItemType Type { get; }
		ItemGrade Grade { get; }
		bool IsCountable => MaxAmount != 1;
		Item CreateItem() => new(Guid.NewGuid(), this, 1);
	}
}
