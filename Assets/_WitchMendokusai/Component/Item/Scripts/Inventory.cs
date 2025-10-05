using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using static WitchMendokusai.SOHelper;

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

	[CreateAssetMenu(fileName = nameof(Inventory), menuName = "WM/DataBuffer/" + nameof(Item))]
	public class Inventory : DataBufferSO<Item>, ISavable<List<InventorySlotSaveData>>, ISerializationCallbackReceiver
	{
		private const int NONE = -1;
		private const int DefaultCapacity = 30;
		public int Capacity { get; private set; } = DefaultCapacity;

		private int FindEmptySlotIndex(int startIndex = 0)
		{
			for (int i = startIndex; i < Capacity; i++)
			{
				if (Data[i] == null)
					return i;
			}
			return NONE;
		}

		public int FindItemIndex(int targetID, int startIndex = 0)
		{
			for (int i = startIndex; i < Capacity; i++)
			{
				Item cur = Data[i];
				if (cur == null)
					continue;

				if (cur.Data.ID == targetID)
					return i;
			}

			// Debug.LogWarning("Item not found");
			return NONE;
		}

		public int FindItemIndex(ItemData target, int startIndex = 0)
		{
			for (int i = startIndex; i < Capacity; i++)
			{
				Item cur = Data[i];
				if (cur == null)
					continue;

				if (cur.Data == target)
					return i;
			}

			// Debug.LogWarning("Item not found");
			return NONE;
		}

		/// <summary> 인벤토리에 아이템 추가
		/// <para/> 넣는 데 실패한 잉여 아이템 개수 리턴
		/// <para/> 리턴이 0이면 넣는데 모두 성공했다는 의미
		/// </summary>
		public int Add(ItemData itemData, int amount = 1)
		{
			// 1. 수량이 있는 아이템
			if (itemData.IsCountable)
			{
				bool findNextCountable = true;
				int index = -1;

				while (amount > 0)
				{
					// 1-1. 추가할 아이템을 이미 가지고 있는지 확인, 개수 여유 있는지 검사
					if (findNextCountable)
					{
						index = FindItemIndex(itemData, index + 1);

						// 기존 아이템 슬롯을 찾은 경우, 양 증가시키고 초과량 존재 시 amount에 초기화
						if (index != NONE)
						{
							if (Data[index].IsMax)
							{
								continue;
							}
							else
							{
								amount = Data[index].AddAmountAndGetExcess(amount);
								UpdateSlot(index);
							}
						}
						// 개수 여유있는 기존 아이템 슬롯이 더이상 없다고 판단될 경우, 빈 슬롯부터 탐색 시작
						else
						{
							findNextCountable = false;
						}
					}
					// 1-2. 빈 슬롯 탐색
					else
					{
						index = FindEmptySlotIndex(index + 1);

						// 빈 슬롯조차 없는 경우 종료
						if (index == NONE)
						{
							break;
						}
						// 빈 슬롯 발견 시, 슬롯에 아이템 추가 및 잉여량 계산
						else
						{
							// 새로운 아이템 생성
							Item newItem = itemData.CreateItem();
							newItem.SetAmount(amount);

							// 슬롯에 추가
							Data[index] = newItem;

							// 남은 개수 계산
							amount = (amount > itemData.MaxAmount) ? (amount - itemData.MaxAmount) : 0;

							UpdateSlot(index);
						}
					}
				}
			}
			// 2. 수량이 없는 아이템
			else
			{
				int index;

				// 2-1. 1개만 넣는 경우, 간단히 수행
				if (amount == 1)
				{
					index = FindEmptySlotIndex();
					if (index != NONE)
					{
						// 아이템을 생성하여 슬롯에 추가
						Data[index] = itemData.CreateItem();
						amount = 0;

						UpdateSlot(index);
					}
				}

				// 2-2. 2개 이상의 수량 없는 아이템을 동시에 추가하는 경우
				index = -1;
				for (; amount > 0; amount--)
				{
					// 아이템 넣은 인덱스의 다음 인덱스부터 슬롯 탐색
					index = FindEmptySlotIndex(index + 1);

					// 다 넣지 못한 경우 루프 종료
					if (index == NONE)
					{
						break;
					}

					// 아이템을 생성하여 슬롯에 추가
					Data[index] = itemData.CreateItem();

					UpdateSlot(index);
				}
			}

			SOManager.Instance.LastEquippedItem.RuntimeValue = itemData;
			return amount;
		}

		public void Remove(int index, int amount = 1)
		{
			if (index < 0 || index >= Capacity)
				return;

			Item item = Data[index];
			if (item == null)
				return;

			if (item.Data.IsCountable)
			{
				item.SetAmount(item.Amount - amount);
				if (item.IsEmpty)
					Data[index] = null;
			}
			else
			{
				Data[index] = null;
			}

			UpdateSlot(index);
		}

		private bool IsValidIndex(int index)
		{
			return index >= 0 && index < Capacity;
		}

		public ItemData GetItemData(int index)
		{
			if (!IsValidIndex(index)) return null;
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