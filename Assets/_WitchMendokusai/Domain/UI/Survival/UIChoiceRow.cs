using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 대화 선택지와 NPC 메뉴 칸. 선택 시 색 띠 채움과 검은 글자
	public class UIChoiceRow : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
	{
		[SerializeField] private SurvivalUITheme theme;
		[SerializeField] private SurvivalUIKind kind = SurvivalUIKind.Count;
		[SerializeField] private Image band;
		[SerializeField] private TextMeshProUGUI label;
		[SerializeField] private Selectable selectable;
		[SerializeField] private float appearOffset = 22f;
		[SerializeField] private float appearDuration = 0.22f;
		[SerializeField] private float confirmBrightness = 2.2f;
		[SerializeField] private float confirmDuration = 0.2f;

		private RectTransform content;
		private bool isSelected;

		public Selectable Selectable => selectable;

		public void SetKind(SurvivalUIKind newKind)
		{
			kind = newKind;
			Apply();
		}

		public void SetLabel(string text) => label.text = text;

		public void PlayAppear(int order)
		{
			if (UIEffects.Enabled == false)
				return;
			if (content == null)
				content = (RectTransform)band.transform;
			float delay = order * theme.CardAppearInterval;
			Vector2 origin = content.anchoredPosition;
			content.DOKill();
			content.anchoredPosition = origin + new Vector2(appearOffset, 0f);
			content.DOAnchorPos(origin, appearDuration).SetDelay(delay).SetUpdate(true).SetTarget(content);
		}

		public void PlayConfirm()
		{
			if (UIEffects.Enabled == false)
				return;
			Color main = theme.Style(kind).Main;
			band.DOKill();
			band.color = new Color(main.r * confirmBrightness, main.g * confirmBrightness, main.b * confirmBrightness, main.a);
			band.DOColor(main, confirmDuration).SetUpdate(true).SetTarget(band);
		}

		public void OnSelect(BaseEventData eventData)
		{
			isSelected = true;
			Apply();
		}

		public void OnDeselect(BaseEventData eventData)
		{
			isSelected = false;
			Apply();
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			if (selectable.IsInteractable())
				selectable.Select();
		}

		private void OnEnable() => Apply();

		private void OnDisable()
		{
			band.DOKill();
			isSelected = false;
		}

		private void Apply()
		{
			SurvivalUITheme.KindStyle style = theme.Style(kind);
			band.color = isSelected ? style.Main : style.Dark;
			label.color = isSelected ? theme.SelectedBandText : theme.BandText;
		}
	}
}
