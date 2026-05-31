using System;
using System.Collections.Generic;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	// TASK-WM-169 Phase 1 — ChestInventory 의 JsonUtility 친화 직렬화 DTO. BuildingInstanceData
	// RuntimeData(string) 슬롯에 저장되어 GridData.Save/Load 와 함께 영속된다(FarmRuntimeData 패턴).
	//
	// 왜 InventorySlotSaveData 재사용 안 하나: 그 struct 는 Guid? 필드를 가지는데 JsonUtility 가
	// Nullable<T> 직렬화를 지원하지 않음. 여기서는 Guid 를 string 으로 평탄화(빈 문자열 = 신규 GUID).
	[Serializable]
	public struct ChestSlotSaveData
	{
		public int SlotIndex;
		public string GuidString;
		public int ItemID;
		public int Amount;

		public ChestSlotSaveData(int slotIndex, Item item)
		{
			SlotIndex = slotIndex;
			GuidString = item.Guid.HasValue ? item.Guid.Value.ToString() : string.Empty;
			ItemID = item.Data.ID;
			Amount = item.Amount;
		}

		public Item ToItem()
		{
			Guid? guid = string.IsNullOrEmpty(GuidString) ? null : Guid.Parse(GuidString);
			return new Item(guid, GetItemData(ItemID), Amount);
		}
	}

	[Serializable]
	public struct ChestSaveData
	{
		public int Capacity;
		public List<ChestSlotSaveData> Slots;

		public static ChestSaveData FromChest(ChestInventory chest)
		{
			List<ChestSlotSaveData> slots = new();
			for (int i = 0; i < chest.Slots.Count; i++)
			{
				Item item = chest.Slots[i];
				if (item == null)
				{
					continue;
				}
				slots.Add(new ChestSlotSaveData(i, item));
			}
			return new ChestSaveData
			{
				Capacity = chest.Capacity,
				Slots = slots
			};
		}

		public void ApplyTo(ChestInventory chest)
		{
			for (int i = 0; i < chest.Capacity; i++)
			{
				chest.Slots[i] = null;
			}

			if (Slots == null)
			{
				return;
			}

			foreach (ChestSlotSaveData slot in Slots)
			{
				if (slot.SlotIndex < 0 || slot.SlotIndex >= chest.Capacity)
				{
					continue;
				}
				chest.Slots[slot.SlotIndex] = slot.ToItem();
			}
		}

		public string ToJson() => JsonUtility.ToJson(this);

		public static ChestSaveData FromJson(string json)
		{
			if (string.IsNullOrEmpty(json))
			{
				return new ChestSaveData { Capacity = 0, Slots = new List<ChestSlotSaveData>() };
			}
			return JsonUtility.FromJson<ChestSaveData>(json);
		}
	}
}
