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

			// 주운 사실이 세계까지 간다 (TASK-WM-218) — 안 알리면 다시 들어왔을 때 없다.
			// 실제로 들어간 만큼만 알린다(넘친 건 안 들어간 것이다).
			if (ReportsToWorld && applyingWorldBag == false)
				WorldBagBridge.Gathered(itemData.ID, amount - excess);

			return excess;
		}

		/// <summary>
		/// 이 가방이 <b>내 가방</b>인가 (TASK-WM-218). 보관 상자처럼 per-instance 인 가방은
		/// 세계에 알리지 않는다 — 알리면 상자에 넣은 것이 내 것으로 둔갑한다.
		/// </summary>
		protected virtual bool ReportsToWorld => true;

		// 세계 값으로 맞추는 동안은 세계에 되알리지 않는다 — 안 그러면 둘이 무한히 오간다.
		private bool applyingWorldBag;

		/// <summary>
		/// 세계가 아는 내 가방으로 <b>맞춘다</b> (TASK-WM-218). 세계가 주인이므로 화면이 따라간다.
		/// 부족한 만큼만 채우고 남는 만큼만 뺀다 — 통째로 비우고 다시 채우면 칸 배치가 매번 뒤집힌다.
		/// </summary>
		public void ApplyWorldCounts(IReadOnlyList<int> itemIds, IReadOnlyList<int> amounts, Func<int, IItemData> lookup)
		{
			if (itemIds == null || amounts == null || lookup == null)
				return;

			// 무엇을 얼마나 만질지는 판정 층이 정한다(BagReconcile) — 여기서는 그 답대로만 한다.
			Dictionary<int, int> target = new Dictionary<int, int>();
			for (int i = 0; i < itemIds.Count && i < amounts.Count; i++)
			{
				if (lookup(itemIds[i]) == null)
					continue; // 게임이 모르는 물건 — 화면에 만들 수 없다.

				target[itemIds[i]] = amounts[i];
			}

			Dictionary<int, int> current = new Dictionary<int, int>();
			foreach (KeyValuePair<int, int> want in target)
				current[want.Key] = Core.CountById(want.Key);

			applyingWorldBag = true;
			try
			{
				List<BagAdjustment> plan = BagReconcile.Plan(current, target);
				for (int i = 0; i < plan.Count; i++)
				{
					if (plan[i].Add > 0)
						Core.Add(lookup(plan[i].ItemId), plan[i].Add);
					else if (plan[i].Remove > 0)
						Core.Consume(plan[i].ItemId, plan[i].Remove);
				}
			}
			finally
			{
				applyingWorldBag = false;
			}
		}

		/// <summary>아이템이 들어온 직후. 플레이어 가방은 SOManager 가 구독해 마지막 장착 아이템을 바꿈 (RootLifetimeScope 가 이음)</summary>
		public event Action<ItemData> ItemAdded = delegate { };

		// 보관 상자처럼 플레이어 것이 아닌 인벤토리는 override 로 무력화 (TASK-WM-169).
		protected virtual void OnItemAdded(ItemData itemData)
		{
			ItemAdded.Invoke(itemData);
		}

		public void Remove(int index, int amount = 1) => Core.Remove(index, amount);

		/// <summary>제작 재료처럼 「그 종류를 이만큼」 쓴다. 못 쓰고 남은 개수를 돌려준다.</summary>
		public int Consume(int itemID, int amount)
		{
			int missing = Core.Consume(itemID, amount);

			// 쓴 것도 세계에 알린다 (TASK-WM-218) — 줍기만 알리면 세계의 가방은 불어나기만 한다.
			// 실제로 쓴 만큼만(못 쓴 건 안 쓴 것이다).
			if (ReportsToWorld && applyingWorldBag == false)
				WorldBagBridge.Consumed(itemID, amount - missing);

			return missing;
		}

		/// <summary>흩어진 칸을 다 합쳐 몇 개 가지고 있나.</summary>
		public int CountByID(int itemID) => Core.CountById(itemID);

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