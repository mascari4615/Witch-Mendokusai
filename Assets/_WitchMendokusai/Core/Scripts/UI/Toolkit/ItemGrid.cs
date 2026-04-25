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

		private void Refresh()
		{
			if (inventory == null)
				return;

			for (int i = 0; i < slots.Count; i++)
				slots[i].Bind(inventory.GetItem(i));
		}
	}
}
