using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	// Json.NET 전용 저장 DTO — Unity 직렬화 대상이 아니라 [Serializable] 을 달지 않는다(GameData 주석 참조).
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

	[CreateAssetMenu(fileName = nameof(Inventory), menuName = "WM/DataBuffer/" + nameof(Item))]
	public class Inventory : DataBufferSO<Item>, IInventory, ISavable<List<InventorySlotSaveData>>, ISerializationCallbackReceiver
	{
		private const int NONE = -1;
		protected virtual int DefaultCapacity => 30;
		public int Capacity { get; protected set; }

		// 가방 규칙 본체는 판정 층에 있다 (TASK-WM-215) — 여기선 에셋·화면 쪽만 맡는다.
		// Data 는 불러오기·역직렬화 때 **통째로 갈린다**. 그래서 목록이 바뀌었으면 다시 묶어 준다
		// (안 그러면 규칙이 옛 목록을 만지고, 화면은 새 목록을 봐서 서로 다른 가방이 된다).
		private InventoryCore boundCore;
		private List<Item> boundSlots;
		private int boundCapacity;

		protected InventoryCore Core
		{
			get
			{
				if (boundCore == null || ReferenceEquals(boundSlots, Data) == false || boundCapacity != Capacity)
				{
					boundCore = new InventoryCore(Data, Capacity);
					boundCore.SlotChanged += UpdateSlot;
					boundSlots = Data;
					boundCapacity = Capacity;
				}

				return boundCore;
			}
		}

		private int FindEmptySlotIndex(int startIndex = 0) => Core.FindEmptySlotIndex(startIndex);

		public int FindItemIndex(int targetID, int startIndex = 0) => Core.FindItemIndex(targetID, startIndex);

		public int FindItemIndex(IItemData target, int startIndex = 0) => Core.FindItemIndex(target, startIndex);

		/// <summary> 인벤토리에 아이템 추가
		/// <para/> 넣는 데 실패한 잉여 아이템 개수 리턴
		/// <para/> 리턴이 0이면 넣는데 모두 성공했다는 의미
		/// </summary>
		public int Add(IItemData itemData, int amount = 1)
		{
			int excess = Core.Add(itemData, amount);
			OnItemAdded((ItemData)itemData);
			return excess;
		}

		// Add 직후 호출. 기본 = '마지막 장착 아이템' 전역 갱신(플레이어 인벤토리 용도).
		// 보관 상자 등 비-플레이어 per-instance 인벤토리는 override 로 무력화 (TASK-WM-169).
		protected virtual void OnItemAdded(ItemData itemData)
		{
			SOManager.Instance.LastEquippedItem.RuntimeValue = itemData;
		}

		public void Remove(int index, int amount = 1) => Core.Remove(index, amount);

		private bool IsValidIndex(int index)
		{
			return index >= 0 && index < Capacity;
		}

		public IItemData GetItemData(int index)
		{
			if (IsValidIndex(index) == false) return null;
			return Data[index]?.Data;
		}

		public Item GetItem(Guid? guid)
		{
			if (guid == null)
				return null;

			foreach (Item item in Data)
			{
				if (item == null)
					continue;

				if (item.Guid == guid)
					return item;
			}

			return null;
		}

		public Item GetItem(int index)
		{
			if (IsValidIndex(index) == false)
				return null;
			return Data[index] ?? null;
		}

		public void SetItem(int index, Item item)
		{
			if (IsValidIndex(index) == false)
			{
				Debug.Log($"{name} : Invalid index {index}");
				return;
			}

			Data[index] = item;
			UpdateSlot(index);
		}

		public void SetItemAmount(int index, int amount)
		{
			if (IsValidIndex(index) == false)
			{
				Debug.Log($"{name} : Invalid index {index}");
				return;
			}

			if (Data[index] != null)
				Data[index].SetAmount(amount);
			UpdateSlot(index);
		}

		public int GetItemAmount(int itemID)
		{
			int amount = 0;
			foreach (Item item in Data)
			{
				if (item == null)
					continue;
				if (item.Data.ID == itemID)
					amount += item.Amount;
			}
			return amount;
		}

		public bool HasEquipment(EquipmentType equipmentType)
		{
			foreach (Item item in Data)
			{
				if (item != null && item.Data is EquipmentData eqData && eqData.EquipmentType == equipmentType)
					return true;
			}
			return false;
		}

		private void UpdateSlot(params int[] indices)
		{
			foreach (int i in indices)
				UpdateSlot(i);
			UpdateUI();
		}

		public void UpdateSlot(int index)
		{
			// Debug.Log($"{name} : {nameof(UpdateSlot)}({index})");

			if (IsValidIndex(index) == false)
			{
				Debug.Log($"{name} : Invalid index {index}");
				return;
			}

			if (Data[index] != null)
				if (Data[index].Data.IsCountable)
					if (Data[index].IsEmpty)
					{
						Debug.Log($"{name} : {index} is empty");
						Data[index] = null;
					}

			UpdateUI();
		}

		public void Load(List<InventorySlotSaveData> savedItems)
		{
			Data = Enumerable.Repeat<Item>(null, Capacity = DefaultCapacity).ToList();

			foreach (InventorySlotSaveData itemData in savedItems)
			{
				Data[itemData.slotIndex] = new Item(
					itemData.Guid,
					SOHelper.GetItemData(itemData.itemID),
					itemData.itemAmount);
			}
		}

		public List<InventorySlotSaveData> Save()
		{
			List<InventorySlotSaveData> InventoryData = new(Capacity);
			for (int i = 0; i < Data.Count; i++)
			{
				if (Data[i] == null)
					continue;
				InventoryData.Add(new InventorySlotSaveData(i, Data[i]));
			}
			// Debug.Log($"InventoryData.Count: {InventoryData.Count}");
			return InventoryData;
		}

		public override void OnAfterDeserialize()
		{
			Data = Enumerable.Repeat<Item>(null, Capacity = DefaultCapacity).ToList();
		}
	}
}