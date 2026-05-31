using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// TASK-WM-169 Phase 1 — per-Building 보관 인벤토리 POCO. 기존 Inventory(ScriptableObject)는
	// 공유 자산이라 인스턴스당 한 개만 존재 → 상자별 내용물에는 부적합. ChestInventory 는 IInventory
	// 정본 surface 만 구현하는 평범 객체로, ChestBuildingObject 가 BuildingObject.RuntimeData(JSON)
	// 슬롯에 직렬화·복원한다(FarmRuntimeData 와 같은 RuntimeData bridge 패턴).
	//
	// Inventory.Add 의 countable/non-countable 알고리즘 동일 의미로 옮긴다(둘이 의미 다르면 상자가
	// 메인 인벤과 다르게 동작 = 사용자 혼란). 미래 두 구현 합치려면 static 헬퍼로 추출 가능.
	public class ChestInventory : IInventory
	{
		private const int NONE = -1;

		public int Capacity { get; }
		public List<Item> Slots { get; }

		public event Action OnDataChanged = delegate { };

		public ChestInventory(int capacity)
		{
			Capacity = capacity;
			Slots = new List<Item>(capacity);
			for (int i = 0; i < capacity; i++)
			{
				Slots.Add(null);
			}
		}

		private int FindEmptySlotIndex(int startIndex = 0)
		{
			for (int i = startIndex; i < Capacity; i++)
			{
				if (Slots[i] == null)
				{
					return i;
				}
			}
			return NONE;
		}

		public int FindItemIndex(int targetID, int startIndex = 0)
		{
			for (int i = startIndex; i < Capacity; i++)
			{
				Item current = Slots[i];
				if (current == null)
				{
					continue;
				}
				if (current.Data.ID == targetID)
				{
					return i;
				}
			}
			return NONE;
		}

		public int FindItemIndex(IItemData target, int startIndex = 0)
		{
			for (int i = startIndex; i < Capacity; i++)
			{
				Item current = Slots[i];
				if (current == null)
				{
					continue;
				}
				if (current.Data == target)
				{
					return i;
				}
			}
			return NONE;
		}

		public int Add(IItemData itemData, int amount = 1)
		{
			if (itemData.IsCountable)
			{
				bool findNextCountable = true;
				int index = -1;

				while (amount > 0)
				{
					if (findNextCountable)
					{
						index = FindItemIndex(itemData, index + 1);

						if (index != NONE)
						{
							if (Slots[index].IsMax)
							{
								continue;
							}
							else
							{
								amount = Slots[index].AddAmountAndGetExcess(amount);
							}
						}
						else
						{
							findNextCountable = false;
						}
					}
					else
					{
						index = FindEmptySlotIndex(index + 1);
						if (index == NONE)
						{
							break;
						}

						Item newItem = itemData.CreateItem();
						newItem.SetAmount(amount);
						Slots[index] = newItem;
						amount = (amount > itemData.MaxAmount) ? (amount - itemData.MaxAmount) : 0;
					}
				}
			}
			else
			{
				int index;

				if (amount == 1)
				{
					index = FindEmptySlotIndex();
					if (index != NONE)
					{
						Slots[index] = itemData.CreateItem();
						amount = 0;
					}
				}

				index = -1;
				for (; amount > 0; amount--)
				{
					index = FindEmptySlotIndex(index + 1);
					if (index == NONE)
					{
						break;
					}

					Slots[index] = itemData.CreateItem();
				}
			}

			OnDataChanged.Invoke();
			return amount;
		}

		public void Remove(int index, int amount = 1)
		{
			if (IsValidIndex(index) == false)
			{
				return;
			}

			Item item = Slots[index];
			if (item == null)
			{
				return;
			}

			if (item.Data.IsCountable)
			{
				item.SetAmount(item.Amount - amount);
				if (item.IsEmpty)
				{
					Slots[index] = null;
				}
			}
			else
			{
				Slots[index] = null;
			}

			OnDataChanged.Invoke();
		}

		public Item GetItem(int index)
		{
			if (IsValidIndex(index) == false)
			{
				return null;
			}
			return Slots[index];
		}

		public Item GetItem(Guid? guid)
		{
			if (guid == null)
			{
				return null;
			}

			foreach (Item item in Slots)
			{
				if (item == null)
				{
					continue;
				}
				if (item.Guid == guid)
				{
					return item;
				}
			}
			return null;
		}

		public void SetItem(int index, Item item)
		{
			if (IsValidIndex(index) == false)
			{
				return;
			}
			Slots[index] = item;
			OnDataChanged.Invoke();
		}

		public void SetItemAmount(int index, int amount)
		{
			if (IsValidIndex(index) == false)
			{
				return;
			}
			if (Slots[index] != null)
			{
				Slots[index].SetAmount(amount);
			}
			OnDataChanged.Invoke();
		}

		public int GetItemAmount(int itemID)
		{
			int amount = 0;
			foreach (Item item in Slots)
			{
				if (item == null)
				{
					continue;
				}
				if (item.Data.ID == itemID)
				{
					amount += item.Amount;
				}
			}
			return amount;
		}

		public IItemData GetItemData(int index)
		{
			if (IsValidIndex(index) == false)
			{
				return null;
			}
			return Slots[index]?.Data;
		}

		private bool IsValidIndex(int index) => index >= 0 && index < Capacity;
	}
}
