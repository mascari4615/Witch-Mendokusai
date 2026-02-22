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
		[SerializeField] private Button resetButton;
		[SerializeField] private Button resetAllButton;
		[SerializeField] private TextMeshProUGUI priceText;
		[SerializeField] private TextMeshProUGUI curLevelText;

		private UIUpgradeGrid upgradeGridUI;
		private NPCObject npc;

		public override bool IsFullscreen => true;

		protected override void OnInit()
		{
			upgradeGridUI = GetComponentInChildren<UIUpgradeGrid>(true);

			// 임의로 모든 UpgradeData 불러오도록 설정
			upgradeGridUI.Init();
			upgradeGridUI.SetData(SOManager.Instance.DataSOs[typeof(UpgradeData)].Values.Cast<UpgradeData>().ToList());
			upgradeGridUI.UpdateUI();

			upgradeGridUI.OnSelectSlot += (slot, data) =>
			{
				UpdateUI();
			};

			buyButton.onClick.AddListener(() =>
			{
				if (upgradeGridUI.CurSlot != null && upgradeGridUI.CurSlot.DataSO is UpgradeData upgradeData)
				{
					GetUpgrade(upgradeData.ID);
				}
			});

			returnButton.onClick.AddListener(() =>
			{
				if (upgradeGridUI.CurSlot != null && upgradeGridUI.CurSlot.DataSO is UpgradeData upgradeData)
				{
					ReturnUpgrade(upgradeData.ID);
				}
			});

			resetButton.onClick.AddListener(() =>
			{
				if (upgradeGridUI.CurSlot != null && upgradeGridUI.CurSlot.DataSO is UpgradeData upgradeData)
				{
					ResetUpgrade(upgradeData.ID);
				}
			});

			resetAllButton.onClick.AddListener(() =>
			{
				ResetUpgradeAll();
			});
		}

		public override void SetNPC(NPCObject npc)
		{
			this.npc = npc;
			// upgradeGridUI.SetDataBuffer(npc.Data.UpgradeDataBuffers[0]);
		}

		public override void UpdateUI()
		{
			// shopImage.sprite = npc.Data.Sprite;

			UpgradeData upgradeData = upgradeGridUI.CurSlot.DataSO as UpgradeData;

			// 최대 레벨이때 buy 버튼, price 비활성화, 최소레벨일 때 return 버튼 비활성화
			if (upgradeData.CurLevel >= upgradeData.MaxLevel)
			{
				buyButton.interactable = false;
				priceText.text = "-";
				curLevelText.text = $"Lv. {upgradeData.CurLevel} (MAX)";
			}
			else
			{
				buyButton.interactable = true;
				priceText.text = upgradeData.PricePerLevel[upgradeData.CurLevel].ToString();
				curLevelText.text = $"Lv. {upgradeData.CurLevel} -> Lv. {upgradeData.CurLevel + 1}";
			}

			if (upgradeData.CurLevel <= 0)
			{
				returnButton.interactable = false;
			}
			else
			{
				returnButton.interactable = true;
			}

			upgradeGridUI.UpdateUI();
		}

		public void GetUpgrade(int upgradeID)
		{
			UpgradeData targetUpgrade = Get<UpgradeData>(upgradeID);
			if (targetUpgrade.TryUpgrade(out UpgradeFailReason reason, out int upgradePrice))
			{
				UpdateUI();
				UIManager.Instance.PopText($"- {upgradePrice}", TextType.Warning);
				return;
			}
			else
			{
				switch (reason)
				{
					case UpgradeFailReason.MaxLevel:
						UIManager.Instance.PopText("최대 레벨입니다. 더 이상 올릴 수 없습니다.", TextType.Warning);
						break;
					case UpgradeFailReason.InsufficientNyang:
						UIManager.Instance.PopText("냥 부족!", TextType.Warning);
						break;
				}
			}
		}

		public void ReturnUpgrade(int slotIndex)
		{
			UpgradeData targetUpgrade = Get<UpgradeData>(slotIndex);
			if (targetUpgrade.TryDowngrade(out DowngradeFailReason reason, out int refundedNyang))
			{
				UpdateUI();
				UIManager.Instance.PopText($"+ {refundedNyang}", TextType.Warning);
				return;
			}
			else
			{
				switch (reason)
				{
					case DowngradeFailReason.MinLevel:
						UIManager.Instance.PopText("최소 레벨입니다. 더 이상 내릴 수 없습니다.", TextType.Warning);
						break;
				}
			}
		}

		public void ResetUpgrade(int slotIndex)
		{
			UpgradeData targetUpgrade = Get<UpgradeData>(slotIndex);
			if (targetUpgrade.TryReset(out DowngradeFailReason reason, out int refundedNyang))
			{
				UpdateUI();
				UIManager.Instance.PopText($"+ {refundedNyang}", TextType.Warning);
				return;
			}
			else
			{
				switch (reason)
				{
					case DowngradeFailReason.MinLevel:
						UIManager.Instance.PopText("최소 레벨입니다. 더 이상 내릴 수 없습니다.", TextType.Warning);
						break;
				}
			}
		}

		public void ResetUpgradeAll()
		{
			int totalRefund = 0;
			ForEach<UpgradeData>(upgradeData =>
			{
				upgradeData.TryReset(out _, out int refundedNyang);
				totalRefund += refundedNyang;
			});

			if (totalRefund > 0)
			{
				UpdateUI();
				UIManager.Instance.PopText($"+ {totalRefund}", TextType.Warning);
			}
		}
	}
}