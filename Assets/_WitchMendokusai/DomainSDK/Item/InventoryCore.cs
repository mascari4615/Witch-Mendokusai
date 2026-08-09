using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 가방의 <b>규칙</b> — 어디에 들어가고, 얼마가 넘치고, 빼면 무엇이 남나 (TASK-WM-215).
	///
	/// 왜 떼어냈나: 이 규칙이 유니티 에셋(ScriptableObject) 안에 있으면 헤드리스 서버가
	/// 같은 가방을 굴릴 수 없다. 칸 목록은 <b>빌려 쓴다</b> — 에셋이 들고 있는 그 목록을 그대로 만지므로
	/// 사본이 생기지 않는다(사본이 생기면 화면과 서버가 다른 가방을 보게 된다).
	///
	/// 화면 갱신·전역 상태 같은 바깥일은 <see cref="SlotChanged"/> 로 알리기만 한다.
	/// </summary>
	public sealed class InventoryCore
	{
		public const int NONE = -1;

		private readonly IList<Item> slots;

		/// <summary>칸 하나가 바뀌었다(넣기·빼기·덮어쓰기). 인자는 칸 번호.</summary>
		public event Action<int> SlotChanged = delegate { };

		public InventoryCore(IList<Item> slots, int capacity)
		{
			this.slots = slots ?? throw new ArgumentNullException(nameof(slots));
			Capacity = capacity;
		}

		public int Capacity { get; private set; }

		public void SetCapacity(int capacity) => Capacity = capacity;

		public bool IsValidIndex(int index) => index >= 0 && index < Capacity;

		public int FindEmptySlotIndex(int startIndex = 0)
		{
			for (int i = startIndex; i < Capacity; i++)
			{
				if (slots[i] == null)
					return i;
			}

			return NONE;
		}

		public int FindItemIndex(int targetId, int startIndex = 0)
		{
			for (int i = startIndex; i < Capacity; i++)
			{
				Item current = slots[i];
				if (current == null)
					continue;

				if (current.Data.ID == targetId)
					return i;
			}

			return NONE;
		}

		public int FindItemIndex(IItemData target, int startIndex = 0)
		{
			for (int i = startIndex; i < Capacity; i++)
			{
				Item current = slots[i];
				if (current == null)
					continue;

				if (current.Data == target)
					return i;
			}

			return NONE;
		}

		/// <summary>
		/// 넣는다. <b>못 넣고 남은 개수</b>를 돌려준다(0 이면 전부 들어갔다).
		/// 수량이 쌓이는 아이템은 이미 있는 칸부터 채우고, 남으면 빈 칸으로 간다.
		/// </summary>
		public int Add(IItemData itemData, int amount = 1)
		{
			if (itemData == null)
				return amount;

			if (itemData.IsCountable)
			{
				return AddCountable(itemData, amount);
			}

			return AddSingles(itemData, amount);
		}

		private int AddCountable(IItemData itemData, int amount)
		{
			bool findNextExisting = true;
			int index = -1;

			while (amount > 0)
			{
				if (findNextExisting)
				{
					index = FindItemIndex(itemData, index + 1);

					if (index != NONE)
					{
						if (slots[index].IsMax)
							continue;

						amount = slots[index].AddAmountAndGetExcess(amount);
						SlotChanged(index);
					}
					else
					{
						findNextExisting = false;
					}
				}
				else
				{
					index = FindEmptySlotIndex(index + 1);
					if (index == NONE)
						break;

					Item newItem = itemData.CreateItem();
					newItem.SetAmount(amount);
					slots[index] = newItem;

					amount = amount > itemData.MaxAmount ? amount - itemData.MaxAmount : 0;
					SlotChanged(index);
				}
			}

			return amount;
		}

		private int AddSingles(IItemData itemData, int amount)
		{
			int index = -1;
			for (; amount > 0; amount--)
			{
				index = FindEmptySlotIndex(index + 1);
				if (index == NONE)
					break;

				slots[index] = itemData.CreateItem();
				SlotChanged(index);
			}

			return amount;
		}

		public void Remove(int index, int amount = 1)
		{
			if (IsValidIndex(index) == false)
				return;

			Item item = slots[index];
			if (item == null)
				return;

			if (item.Data.IsCountable)
			{
				item.SetAmount(item.Amount - amount);
				if (item.IsEmpty)
					slots[index] = null;
			}
			else
			{
				slots[index] = null;
			}

			SlotChanged(index);
		}

		/// <summary>그 종류를 다 합쳐 몇 개나 가지고 있나 — 「만들 수 있나」를 묻는 자리.</summary>
		public int CountById(int itemId)
		{
			int total = 0;
			for (int i = 0; i < Capacity; i++)
			{
				Item item = slots[i];
				if (item == null)
					continue;

				if (item.Data.ID == itemId)
					total += item.Amount;
			}

			return total;
		}

		/// <summary>
		/// 그 종류를 <paramref name="amount"/> 개만큼 쓴다(제작 재료 소모).
		/// <b>못 쓰고 남은 개수</b>를 돌려준다 — 0 이면 다 썼다.
		///
		/// ★ 모자라면 있는 만큼만 쓰고 남은 수를 알려준다. 옛 구현은 없는 재료를 찾으면
		///   빈 칸을 붙잡고 <b>영영 돌았다</b>(부르기 전에 확인했겠거니 하고 있었다).
		/// </summary>
		public int Consume(int itemId, int amount)
		{
			while (amount > 0)
			{
				int index = FindItemIndex(itemId);
				if (index == NONE)
					break;

				Item item = slots[index];
				if (item == null)
					break;

				int slotAmount = item.Amount;
				if (slotAmount > amount)
				{
					item.SetAmount(slotAmount - amount);
					SlotChanged(index);
					return 0;
				}

				Remove(index, slotAmount);
				amount -= slotAmount;
			}

			return amount;
		}

		public IItemData GetItemData(int index) => IsValidIndex(index) ? slots[index]?.Data : null;

		public Item GetItem(int index) => IsValidIndex(index) ? slots[index] : null;

		public Item GetItem(Guid? guid)
		{
			if (guid == null)
				return null;

			for (int i = 0; i < slots.Count; i++)
			{
				Item item = slots[i];
				if (item == null)
					continue;

				if (item.Guid == guid)
					return item;
			}

			return null;
		}

		/// <summary>칸에 직접 놓는다. 잘못된 칸이면 <b>조용히 넘기지 않고</b> 호스트에 알린다.</summary>
		public void SetItem(int index, Item item)
		{
			if (IsValidIndex(index) == false)
			{
				SdkLog.Warning($"{nameof(InventoryCore)}: 없는 칸 {index} 에 넣으려 했다");
				return;
			}

			slots[index] = item;
			SlotChanged(index);
		}
	}
}
