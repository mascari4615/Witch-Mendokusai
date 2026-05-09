using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	[Serializable]
	public struct DollEquipmentSlotSaveData
	{
		public int SlotIndex;
		public Guid? Guid;
		public int ItemID;

		public DollEquipmentSlotSaveData(int slotIndex, Item item)
		{
			SlotIndex = slotIndex;
			Guid = item.Guid;
			ItemID = item.Data.ID;
		}
	}

	[Serializable]
	public struct DollSaveData
	{
		public int DollID;
		public int Level;
		public int Exp;
		public List<DollEquipmentSlotSaveData> Equipment;

		public DollSaveData(int dollID, int dollLevel, int dollExp, List<DollEquipmentSlotSaveData> equipment)
		{
			DollID = dollID;
			Level = dollLevel;
			Exp = dollExp;
			Equipment = equipment;
		}
	}
}
