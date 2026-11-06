using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace WitchMendokusai
{
	// 레벨업과 카드 선택 칸의 호버, 선택, 등장 연출
	public class UIChoiceCard : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
	{
		[SerializeField] private SurvivalUITheme theme;
		[SerializeField] private RectTransform lifted;
		[SerializeField] private Image outline;
		[SerializeField] private Image band;
		[SerializeField] private TextMeshProUGUI bandText;
		[SerializeField] private GameObject selectedFrame;
		[SerializeField] private GameObject sheen;
		[SerializeField] private UIBurst burst;
		[SerializeField] private Button button;

		private SurvivalUIKind kind;
		private bool isSelected;
		private Tween moveTween;

		public void SetKind(SurvivalUIKind newKind)
		{
			kind = newKind;
			ApplyState();
		}

		public void PlayAppear(int order)
		{
			UIEffects.PopIn(transform, theme.CardAppearDuration, order * theme.CardAppearInterval);
		}

		public void PlayConfirm()
		{
			burst.Play(theme.Style(kind).Main);
		}

		public void OnSelect(BaseEventData eventData)
		{
			isSelected = true;
			ApplyState();
			if (eventData != null)
				burst.Play(theme.Style(kind).Main, 8);
		}

		public void OnDeselect(BaseEventData eventData)
		{
			isSelected = false;
			ApplyState();
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			button.Select();
			Move(theme.HoverLift);
		}

		public void OnPointerExit(PointerEventData eventData) => Move(0f);

		private void Update()
		{
			if (isSelected)
				UIEffects.TickShaderTime();
		}

		private void OnDisable()
		{
			moveTween?.Kill();
			transform.DOKill();
			transform.localScale = Vector3.one;
			lifted.anchoredPosition = Vector2.zero;
			isSelected = false;
			ApplyState();
		}

		private void Move(float offset)
		{
			moveTween?.Kill();
			if (UIEffects.Enabled)
				moveTween = lifted.DOAnchorPosY(offset, theme.HoverDuration).SetUpdate(true);
			else
				lifted.anchoredPosition = new Vector2(0f, offset);
		}

		private void ApplyState()
		{
			SurvivalUITheme.KindStyle style = theme.Style(kind);
			outline.color = isSelected ? theme.SelectedLine : kind == SurvivalUIKind.Advanced ? style.Main : theme.IdleLine;
			band.color = isSelected ? style.Main : style.Dark;
			bandText.color = isSelected ? theme.SelectedBandText : theme.BandText;
			selectedFrame.SetActive(isSelected);
			sheen.SetActive(isSelected && UIEffects.Enabled);
		}
	}
}
