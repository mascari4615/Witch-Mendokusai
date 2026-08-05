using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// Json.NET 전용 저장 DTO — Unity 직렬화 대상이 아니라 [Serializable] 을 달지 않는다(GameData 주석 참조).
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

	// Json.NET 전용 저장 DTO — Unity 직렬화 대상이 아니라 [Serializable] 을 달지 않는다(GameData 주석 참조).
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
