using System;

namespace WitchMendokusai
{
	[Serializable]
	public struct InventorySlotSaveData
	{
		public int slotIndex;
		public Guid? Guid;
		public int itemID;
		public int itemAmount;

		public InventorySlotSaveData(int slotIndex, Item item)
		{
			this.slotIndex = slotIndex;
			Guid = item.Guid;
			itemID = item.Data.ID;
			itemAmount = item.Amount;
		}
	}
}