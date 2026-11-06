using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UIUpgradeSlot : UISlot, IPointerEnterHandler, IPointerExitHandler
	{
		[SerializeField] private Image hoverLine;
		[SerializeField] private float hoverLift = 4f;
		private Vector2 restPosition;
		private bool hasRestPosition;
		private Tween hoverTween;
		[SerializeField] private SurvivalUITheme theme;
		[SerializeField] private UIRing ring;
		[SerializeField] private TextMeshProUGUI levelText;
		[SerializeField] private TextMeshProUGUI priceText;
		[SerializeField] private GameObject pendingBadge;
		[SerializeField] private GameObject selectedFrame;
		[SerializeField] private CanvasGroup iconGroup;
		[SerializeField] private float zeroLevelIconAlpha = 0.38f;

		public void SetSelected(bool isSelected) => selectedFrame.SetActive(isSelected);

		public void PlayAppear(int order)
		{
			UIEffects.PopIn(transform, theme.CardAppearDuration, order * theme.ResultEntryInterval, theme.SlotAppearStartScale);
		}

		public void OnPointerEnter(PointerEventData eventData) => SetHover(true);

		public void OnPointerExit(PointerEventData eventData) => SetHover(false);

		private void OnDisable()
		{
			hoverTween?.Kill();
			hoverLine.gameObject.SetActive(false);
			transform.DOKill();
			transform.localScale = Vector3.one;
			if (hasRestPosition)
				((RectTransform)transform).anchoredPosition = restPosition;
		}

		private void SetHover(bool isHover)
		{
			RectTransform rect = (RectTransform)transform;
			if (hasRestPosition == false)
			{
				restPosition = rect.anchoredPosition;
				hasRestPosition = true;
			}
			hoverLine.gameObject.SetActive(isHover);
			hoverTween?.Kill();
			Vector2 target = restPosition + (isHover ? Vector2.up * hoverLift : Vector2.zero);
			if (UIEffects.Enabled)
				hoverTween = rect.DOAnchorPos(target, theme.HoverDuration).SetUpdate(true);
			else
				rect.anchoredPosition = target;
		}

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
			hoverLine.color = ring.color;
			ring.SetFill((float)upgradeData.CurLevel / upgradeData.MaxLevel);
			iconGroup.alpha = upgradeData.CurLevel == 0 ? zeroLevelIconAlpha : 1f;
			levelText.text = isMax ? "MAX" : $"{upgradeData.CurLevel}/{upgradeData.MaxLevel}";
			levelText.color = isMax ? theme.MaxColor : Color.white;
			priceText.text = isMax ? string.Empty : $"{upgradeData.PricePerLevel[upgradeData.CurLevel]}냥";
			pendingBadge.SetActive(upgradeData.EffectPending);
		}
	}
}
