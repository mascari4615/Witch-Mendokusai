using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	[UxmlElement]
	public partial class ItemGrid : VisualElement
	{
		public const string USS_CLASS = "wm-item-grid";

		private Inventory inventory;
		private readonly List<ItemSlot> slots = new();
		private ItemType filter = ItemType.None;

		public ItemGrid()
		{
			AddToClassList(USS_CLASS);
		}

		public void Bind(Inventory newInventory)
		{
			if (inventory != null)
				inventory.OnDataChanged -= Refresh;

			inventory = newInventory;

			if (inventory == null)
			{
				BuildSlots(0);
				return;
			}

			BuildSlots(inventory.Capacity);

			foreach (ItemSlot slot in slots)
				slot.SetInventory(inventory);

			inventory.OnDataChanged += Refresh;
			Refresh();
		}

		public void Unbind()
		{
			if (inventory != null)
				inventory.OnDataChanged -= Refresh;
			inventory = null;
		}

		private void BuildSlots(int count)
		{
			while (slots.Count > count)
			{
				ItemSlot lastSlot = slots[^1];
				Remove(lastSlot);
				slots.RemoveAt(slots.Count - 1);
			}

			while (slots.Count < count)
			{
				ItemSlot newSlot = new();
				newSlot.SetIndex(slots.Count);
				Add(newSlot);
				slots.Add(newSlot);
			}
		}

		public void SetFilter(ItemType newFilter)
		{
			filter = newFilter;
			Refresh();
		}

		private void Refresh()
		{
			if (inventory == null)
				return;

			for (int i = 0; i < slots.Count; i++)
			{
				Item item = inventory.GetItem(i);
				slots[i].Bind(item);
				slots[i].style.display = ShouldShow(item) ? DisplayStyle.Flex : DisplayStyle.None;
			}
		}

		private bool ShouldShow(Item item)
		{
			if (filter == ItemType.None)
				return true;
			if (item == null || item.Data == null)
				return false;
			return item.Data.Type == filter;
		}
	}
}
