using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class UIUpgrade : UIPanel
	{
		[SerializeField] private Button buyButton;
		[SerializeField] private Button returnButton;
		[SerializeField] private TextMeshProUGUI priceText;
		[SerializeField] private TextMeshProUGUI curLevelText;

		private UIUpgradeGrid upgradeGridUI;

		private NPCObject npc;

		protected override void OnInit()
		{
			upgradeGridUI = GetComponentInChildren<UIUpgradeGrid>(true);

			// 임의로 모든 UpgradeData 불러오도록 설정
			upgradeGridUI.SetData(SOManager.Instance.DataSOs[typeof(UpgradeData)].Values.Cast<UpgradeData>().ToList());

			upgradeGridUI.OnSelectSlot += (slot, data) =>
			{
				UpgradeData upgradeData = data;
				priceText.text = upgradeData.PricePerLevel[upgradeData.CurLevel].ToString();
				curLevelText.text = $"Lv. {upgradeData.CurLevel} -> Lv. {upgradeData.CurLevel + 1}";

				// 최대 레벨이때 buy 버튼, price 비활성화, 최소레벨일 때 return 버튼 비활성화
				if (upgradeData.CurLevel >= upgradeData.MaxLevel)
				{
					buyButton.interactable = false;
					priceText.text = "-";
				}
				else
				{
					buyButton.interactable = true;
				}

				if (upgradeData.CurLevel <= 0)
				{
					returnButton.interactable = false;
				}
				else
				{
					returnButton.interactable = true;
				}
			};
		}

		public override void SetNPC(NPCObject npc)
		{
			this.npc = npc;
			// upgradeGridUI.SetDataBuffer(npc.Data.UpgradeDataBuffers[0]);
		}

		public override void UpdateUI()
		{
			// shopImage.sprite = npc.Data.Sprite;

			upgradeGridUI.UpdateUI();
		}

		public void GetUpgrade(int upgradeID)
		{
			UpgradeType upgradeType = (UpgradeType)GetItemData(upgradeID).Type;
			
			ItemData itemData = GetItemData(upgradeID);
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

		public void ReturnUpgrade(int slotIndex)
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