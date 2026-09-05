using System;

namespace WitchMendokusai
{
	public class Item
	{
		public Guid? Guid { get; private set; } = null;
		public IItemData Data { get; private set; } = null;
		public int MaxAmount => Data.MaxAmount;
		public int Amount { get; protected set; } = 0;

		public Item(Guid? guid, IItemData data, int amount = 1)
		{
			Guid = guid;
			Data = data;
			SetAmount(amount);
		}

		public void SetAmount(int amount)
		{
			Amount = Math.Clamp(amount, 0, MaxAmount);
		}

		/// <summary> 개수 추가 및 최대치 초과량 반환(초과량 없을 경우 0) </summary>
		public int AddAmountAndGetExcess(int amount)
		{
			int nextAmount = Amount + amount;
			SetAmount(nextAmount);

			return (nextAmount > MaxAmount) ? (nextAmount - MaxAmount) : 0;
		}

		public bool IsMax => Amount >= Data.MaxAmount;
		public bool IsEmpty => Amount <= 0;
	}
}
