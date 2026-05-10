using System;

namespace WitchMendokusai
{
	/// <summary>
	/// Item inventory abstract — DomainSDK 영역. mod/UGC 가 native Inventory 와 같은 surface 로
	/// 자체 IInventory 구현 가능. Inventory (Component) 가 표준 구현.
	/// EquipmentType / InventorySlotSaveData 등 Component 의존 surface 는 IInventory 안 박지 않음
	/// (호출처가 Inventory type 직접 사용).
	/// </summary>
	public interface IInventory
	{
		int Capacity { get; }
		event Action OnDataChanged;

		int Add(IItemData itemData, int amount = 1);
		void Remove(int index, int amount = 1);

		Item GetItem(int index);
		Item GetItem(Guid? guid);
		void SetItem(int index, Item item);
		void SetItemAmount(int index, int amount);

		int GetItemAmount(int itemID);
		int FindItemIndex(int targetID, int startIndex = 0);
		int FindItemIndex(IItemData target, int startIndex = 0);

		IItemData GetItemData(int index);
	}
}
