using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	public class UIItemDataGrid : UIDataGrid<ItemData>
	{
		[SerializeField] private Transform filtersParent;
		[SerializeField] private ItemType filter = ItemType.None;

		public override void Init()
		{
			base.Init();

			if (filtersParent != null)
			{
				UISlot[] fillerButtons = filtersParent.GetComponentsInChildren<UISlot>(true);
				for (int i = 0; i < fillerButtons.Length; i++)
				{
					fillerButtons[i].SetSlotIndex(i);
					fillerButtons[i].SetClickAction((slot) => {SetFilter((ItemType)(slot.Index - 1));});
				}
			}
		}

		public void UpdateSlotUI(int index, Item item)
		{
			if (item != null)
			{
				Slots[index].SetSlot(item.Data, item.Amount);
			}
			else
			{
				Slots[index].SetSlot(null);
			}
		}

		public void SetFilter(ItemType newFilter)
		{
			filter = newFilter;
			SetFilterFunc((itemData) => filter == ItemType.None || itemData.Type == filter);
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