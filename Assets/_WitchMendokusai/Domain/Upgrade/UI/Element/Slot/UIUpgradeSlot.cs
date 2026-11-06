using TMPro;
using UnityEngine;

namespace WitchMendokusai
{
	public class UIUpgradeSlot : UISlot
	{
		[SerializeField] private SurvivalUITheme theme;
		[SerializeField] private UIRing ring;
		[SerializeField] private TextMeshProUGUI levelText;
		[SerializeField] private TextMeshProUGUI priceText;
		[SerializeField] private GameObject pendingBadge;
		[SerializeField] private GameObject selectedFrame;
		[SerializeField] private CanvasGroup iconGroup;
		[SerializeField] private float zeroLevelIconAlpha = 0.38f;

		public void SetSelected(bool isSelected) => selectedFrame.SetActive(isSelected);

		public void PlayBuy()
		{
			UIEffects.Punch(transform, theme.PunchAmount, theme.PunchDuration);
		}

		public override void UpdateUI()
		{
			base.UpdateUI();

			if (DataSO is not UpgradeData upgradeData)
				return;

			bool isMax = upgradeData.CurLevel >= upgradeData.MaxLevel;
			nameText.text = upgradeData.DisplayName;
			ring.color = isMax ? theme.MaxColor : theme.Style(upgradeData.Category).Main;
			ring.SetFill((float)upgradeData.CurLevel / upgradeData.MaxLevel);
			iconGroup.alpha = upgradeData.CurLevel == 0 ? zeroLevelIconAlpha : 1f;
			levelText.text = isMax ? "MAX" : $"{upgradeData.CurLevel}/{upgradeData.MaxLevel}";
			levelText.color = isMax ? theme.MaxColor : Color.white;
			priceText.text = isMax ? string.Empty : $"{upgradeData.PricePerLevel[upgradeData.CurLevel]}냥";
			pendingBadge.SetActive(upgradeData.EffectPending);
		}
	}
}
