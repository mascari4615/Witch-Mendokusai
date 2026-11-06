using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class UIUpgrade : UIPanel
	{
		[SerializeField] private SurvivalUITheme theme;
		[SerializeField] private Button buyButton;
		[SerializeField] private Button returnButton;
		[SerializeField] private Button resetButton;
		[SerializeField] private Button resetAllButton;
		[SerializeField] private TextMeshProUGUI priceText;
		[SerializeField] private TextMeshProUGUI returnText;
		[SerializeField] private TextMeshProUGUI resetText;
		[SerializeField] private TextMeshProUGUI curLevelText;
		[SerializeField] private TextMeshProUGUI groupText;
		[SerializeField] private UIRing detailRing;
		[SerializeField] private Image detailIcon;
		[SerializeField] private TextMeshProUGUI detailName;
		[SerializeField] private TextMeshProUGUI detailDescription;
		[SerializeField] private TextMeshProUGUI nowValueText;
		[SerializeField] private TextMeshProUGUI nextValueText;
		[SerializeField] private GameObject pendingBadge;
		[SerializeField] private TextMeshProUGUI walletText;
		[SerializeField] private GameObject confirmBox;
		[SerializeField] private TextMeshProUGUI confirmSumText;
		[SerializeField] private Button confirmYesButton;
		[SerializeField] private Button confirmNoButton;
		[SerializeField] private UIBurst buyBurst;

		private UIUpgradeGrid upgradeGridUI;
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
			upgradeGridUI = GetComponentInChildren<UIUpgradeGrid>(true);

			upgradeGridUI.Init();
			List<UpgradeData> upgrades = soManager.DataSOs[typeof(UpgradeData)].Values.Cast<UpgradeData>()
				.OrderBy(upgrade => upgrade.Category).ThenBy(upgrade => upgrade.DisplayOrder).ToList();
			upgradeGridUI.SetData(upgrades);
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

			resetAllButton.onClick.AddListener(OpenResetAllConfirm);
			confirmNoButton.onClick.AddListener(() => confirmBox.SetActive(false));
			confirmYesButton.onClick.AddListener(() =>
			{
				confirmBox.SetActive(false);
				ResetUpgradeAll();
			});
			confirmBox.SetActive(false);
		}

		public override void SetNPC(NPCObject npc)
		{
			this.npc = npc;
		}

		protected override void OnOpen()
		{
			UpdateUI();
		}

		public override void UpdateUI()
		{
			walletText.text = $"{dataManager.GameStat[GameStatType.NYANG]:N0}냥";
			for (int i = 0; i < upgradeGridUI.Slots.Count; i++)
				(upgradeGridUI.Slots[i] as UIUpgradeSlot).SetSelected(i == upgradeGridUI.CurSlotIndex);

			if (upgradeGridUI.CurSlot == null || upgradeGridUI.CurSlot.DataSO is not UpgradeData upgradeData)
			{
				buyButton.interactable = false;
				returnButton.interactable = false;
				resetButton.interactable = false;
				priceText.text = "-";
				curLevelText.text = "-";
				upgradeGridUI.UpdateUI();
				return;
			}

			bool isMax = upgradeData.CurLevel >= upgradeData.MaxLevel;
			bool hasLevel = upgradeData.CurLevel > 0;
			Color categoryColor = theme.Style(upgradeData.Category).Main;

			groupText.text = $"< {theme.Style(upgradeData.Category).DisplayName} >";
			detailRing.color = isMax ? theme.MaxColor : categoryColor;
			detailRing.SetFill((float)upgradeData.CurLevel / upgradeData.MaxLevel, isMax ? 0f : (float)(upgradeData.CurLevel + 1) / upgradeData.MaxLevel);
			detailIcon.sprite = upgradeData.Sprite;
			detailName.text = upgradeData.DisplayName;
			detailDescription.text = upgradeData.DisplayDescription;
			pendingBadge.SetActive(upgradeData.EffectPending);

			nowValueText.text = $"+{upgradeData.TotalValue}";
			nextValueText.text = isMax ? "-" : $"+{upgradeData.TotalValue + (int)upgradeData.ValuePerLevel[upgradeData.CurLevel]}";

			buyButton.interactable = isMax == false;
			priceText.text = isMax ? "-" : $"강화  {upgradeData.PricePerLevel[upgradeData.CurLevel]}냥";
			curLevelText.text = isMax ? $"Lv. {upgradeData.CurLevel} (MAX)" : $"Lv. {upgradeData.CurLevel} → Lv. {upgradeData.CurLevel + 1}";

			returnButton.interactable = hasLevel;
			returnText.text = hasLevel ? $"환불 +{upgradeData.PricePerLevel[upgradeData.CurLevel - 1]}" : "환불";
			resetButton.interactable = hasLevel;
			resetText.text = hasLevel ? $"이 항목 초기화 +{upgradeData.PricePerLevel.Take(upgradeData.CurLevel).Sum()}" : "이 항목 초기화";

			upgradeGridUI.UpdateUI();
		}

		private void OpenResetAllConfirm()
		{
			int total = 0;
			ForEach<UpgradeData>(upgradeData => total += upgradeData.PricePerLevel.Take(upgradeData.CurLevel).Sum());
			confirmSumText.text = $"모든 강화를 초기화하고 쓴 냥을 돌려받을까요?\n돌려받는 냥 {total:N0}냥";
			confirmBox.SetActive(true);
		}

		public void GetUpgrade(int upgradeID)
		{
			UpgradeData targetUpgrade = Get<UpgradeData>(upgradeID);
			if (targetUpgrade.TryUpgrade(dataManager.GameStat, out UpgradeFailReason reason, out int upgradePrice))
			{
				UpdateUI();
				uiManager.PopText($"- {upgradePrice}", TextType.Warning);
				(upgradeGridUI.CurSlot as UIUpgradeSlot).PlayBuy();
				UIEffects.Punch(detailRing.transform, theme.PunchAmount, theme.PunchDuration);
				buyBurst.Play(targetUpgrade.CurLevel >= targetUpgrade.MaxLevel ? theme.MaxColor : theme.Style(targetUpgrade.Category).Main);
				return;
			}
			else
			{
				switch (reason)
				{
					case UpgradeFailReason.MaxLevel:
						uiManager.PopText("최대 레벨입니다. 더 이상 올릴 수 없습니다.", TextType.Warning);
						break;
					case UpgradeFailReason.InsufficientNyang:
						uiManager.PopText("냥 부족!", TextType.Warning);
						break;
				}
			}
		}

		public void ReturnUpgrade(int slotIndex)
		{
			UpgradeData targetUpgrade = Get<UpgradeData>(slotIndex);
			if (targetUpgrade.TryDowngrade(dataManager.GameStat, out DowngradeFailReason reason, out int refundedNyang))
			{
				UpdateUI();
				uiManager.PopText($"+ {refundedNyang}", TextType.Warning);
				return;
			}
			else
			{
				switch (reason)
				{
					case DowngradeFailReason.MinLevel:
						uiManager.PopText("최소 레벨입니다. 더 이상 내릴 수 없습니다.", TextType.Warning);
						break;
				}
			}
		}

		public void ResetUpgrade(int slotIndex)
		{
			UpgradeData targetUpgrade = Get<UpgradeData>(slotIndex);
			if (targetUpgrade.TryReset(dataManager.GameStat, out DowngradeFailReason reason, out int refundedNyang))
			{
				UpdateUI();
				uiManager.PopText($"+ {refundedNyang}", TextType.Warning);
				return;
			}
			else
			{
				switch (reason)
				{
					case DowngradeFailReason.MinLevel:
						uiManager.PopText("최소 레벨입니다. 더 이상 내릴 수 없습니다.", TextType.Warning);
						break;
				}
			}
		}

		public void ResetUpgradeAll()
		{
			int totalRefund = 0;
			ForEach<UpgradeData>(upgradeData =>
			{
				upgradeData.TryReset(dataManager.GameStat, out _, out int refundedNyang);
				totalRefund += refundedNyang;
			});

			if (totalRefund > 0)
			{
				UpdateUI();
				uiManager.PopText($"+ {totalRefund}", TextType.Warning);
			}
		}
	}
}
