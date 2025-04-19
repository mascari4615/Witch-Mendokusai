using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class UIShop : UIPanel
	{
		[SerializeField] private Image shopImage;
		[SerializeField] private Image dollImage;

		private UIItemDataGrid shopInventoryUI;
		private UIItemGrid itemInventoryUI;

		private NPCObject npc;

		public override void Init()
		{
			shopInventoryUI = GetComponentInChildren<UIItemDataGrid>(true);
			itemInventoryUI = GetComponentInChildren<UIItemGrid>(true);

			shopInventoryUI.Init();
			shopInventoryUI.SetPriceType(PriceType.Buy);
			foreach (UISlot slot in shopInventoryUI.Slots)
			{
				slot.SetClickAction((slot) =>
				{
					shopInventoryUI.SelectSlot(slot.Index);
					BuyItem(slot.DataSO.ID);
				});
			}

			itemInventoryUI.Init();
			itemInventoryUI.SetPriceType(PriceType.Sell);
			foreach (UISlot slot in itemInventoryUI.Slots)
			{
				slot.SetClickAction((slot) =>
				{
					itemInventoryUI.SelectSlot(slot.Index);
					SellItem(slot.Index);
				});
			}
		}

		public override void SetNPC(NPCObject npc)
		{
			this.npc = npc;
			shopInventoryUI.SetDataBuffer(npc.Data.ItemDataBuffers[0]);
		}

		public override void UpdateUI()
		{
			shopImage.sprite = npc.Data.Sprite;
			dollImage.sprite = GetDoll(DataManager.Instance.CurDollID).Sprite;

			shopInventoryUI.UpdateUI();
			itemInventoryUI.UpdateUI();
		}

		public void BuyItem(int itemID)
		{
			ItemData itemData = GetItemData(itemID);
			if (itemData.PurchasePrice <= DataManager.Instance.GameStat[GameStatType.NYANG])
			{
				DataManager.Instance.GameStat[GameStatType.NYANG] -= itemData.PurchasePrice;
				SOManager.Instance.ItemInventory.Add(itemData);
				UpdateUI();

				UIManager.Instance.PopText($"- {itemData.PurchasePrice}", TextType.Warning);
			}
			else
			{
				UIManager.Instance.PopText("냥이 부족합니다.", TextType.Warning);
			}
		}

		public void SellItem(int slotIndex)
		{
			Item item = SOManager.Instance.ItemInventory.GetItem(slotIndex);
			if (item != null)
			{
				ItemData itemData = item.Data;
				DataManager.Instance.GameStat[GameStatType.NYANG] += itemData.SalePrice;
				SOManager.Instance.ItemInventory.Remove(slotIndex);
				UpdateUI();

				UIManager.Instance.PopText($"+ {itemData.SalePrice}", TextType.Warning);
			}
		}
	}
}