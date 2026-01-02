using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	public class UIItemGrid : UIDataGrid<Item>
	{
		[SerializeField] private Transform filtersParent;
		[SerializeField] private ItemType filter = ItemType.None;

		public override void Init()
		{
			base.Init();

			Inventory inventory = DataBufferSO as Inventory;
			inventory.RegisterUI(this);

			foreach (UIItemSlot slot in Slots.Cast<UIItemSlot>())
				slot.SetUIItemGrid(this);

			if (filtersParent != null)
			{
				UISlot[] fillerButtons = filtersParent.GetComponentsInChildren<UISlot>(true);
				for (int i = 0; i < fillerButtons.Length; i++)
				{
					// None 포함
					if (i <= (int)ItemType.Count)
					{
						fillerButtons[i].Init();
						fillerButtons[i].SetSlotIndex(i);
						fillerButtons[i].SetClickAction((slot) => { SetFilter((ItemType)(slot.Index - 1)); });

						fillerButtons[i].gameObject.SetActive(true);
					}
					else
					{
						fillerButtons[i].gameObject.SetActive(false);
					}
				}
			}
		}

		public override void UpdateUI()
		{
			Inventory inventory = DataBufferSO as Inventory;

			base.UpdateUI();

			for (int i = 0; i < Slots.Count; i++)
			{
				if (inventory.Capacity <= i)
				{
					Slots[i].gameObject.SetActive(false);
					continue;
				}
			}
		}

		protected override void SetSlotData(int index, Item item)
		{
			if (item == null)
			{
				Slots[index].SetSlot(null);
				return;
			} 

			UIItemSlot slot = Slots[index] as UIItemSlot;

			slot.SetSlot(item.Data, item.Amount);
			slot.canHold = filter == ItemType.None;
		}

		public void SetFilter(ItemType newFilter)
		{
			filter = newFilter;
			SetFilterFunc((item) => (filter == ItemType.None) || (item.Data.Type == filter));
			UpdateUI();
		}

		public void SetPriceType(PriceType priceType)
		{
			foreach (UIItemSlot slot in Slots.Cast<UIItemSlot>())
				slot.SetPriceType(priceType);
			UpdateUI();
		}
	}
}