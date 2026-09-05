using UnityEngine;
using UnityEngine.UI;
using VContainer;
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

		private UIManager uiManager;
		private SOManager soManager;
		private DataManager dataManager;

		public override bool IsFullscreen => true;

		[Inject]
		public void Construct(UIManager uiManager, SOManager soManager, DataManager dataManager)
		{
			this.uiManager = uiManager;
			this.soManager = soManager;
			this.dataManager = dataManager;
		}

		protected override void OnInit()
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
			dollImage.sprite = GetDoll(dataManager.CurDollID).Sprite;

			shopInventoryUI.UpdateUI();
			itemInventoryUI.UpdateUI();
		}

		public void BuyItem(int itemID)
		{
			ItemData itemData = GetItemData(itemID);
			if (itemData.PurchasePrice <= dataManager.GameStat[GameStatType.NYANG])
			{
				dataManager.GameStat[GameStatType.NYANG] -= itemData.PurchasePrice;
				soManager.ItemInventory.Add(itemData);
				UpdateUI();

				uiManager.PopText($"- {itemData.PurchasePrice}", TextType.Warning);
			}
			else
			{
				uiManager.PopText("냥이 부족합니다.", TextType.Warning);
			}
		}

		public void SellItem(int slotIndex)
		{
			Item item = soManager.ItemInventory.GetItem(slotIndex);
			if (item != null)
			{
				ItemData itemData = (ItemData)item.Data;
				dataManager.GameStat[GameStatType.NYANG] += itemData.SalePrice;
				soManager.ItemInventory.Remove(slotIndex);
				UpdateUI();

				uiManager.PopText($"+ {itemData.SalePrice}", TextType.Warning);
			}
		}
	}
}