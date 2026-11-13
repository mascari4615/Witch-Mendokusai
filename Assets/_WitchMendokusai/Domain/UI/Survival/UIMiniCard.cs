using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 작은 카드 칸. 레벨업, 덱 탭, 결과 공용
	public class UIMiniCard : MonoBehaviour
	{
		[SerializeField] private SurvivalUITheme theme;
		[SerializeField] private Image background;
		[SerializeField] private Image outline;
		[SerializeField] private Image icon;
		[SerializeField] private TextMeshProUGUI amountText;
		[SerializeField] private TextMeshProUGUI nameText;
		[SerializeField] private CanvasGroup offGroup;
		[SerializeField] private float offAlpha = 0.28f;
		[SerializeField] private Color offBackground = new(0.08f, 0.07f, 0.1f, 1f);
		[SerializeField] private Color offOutline = new(0.31f, 0.3f, 0.34f, 1f);

		private float nameFontSize = -1f;

		public void SetCard(CardData card, int count)
		{
			SurvivalUITheme.KindStyle style = theme.Style(card.UIKind);
			bool hasCount = count > 0;
			background.color = hasCount ? style.Dark : offBackground;
			outline.color = hasCount ? style.Main : offOutline;
			icon.sprite = card.Sprite;
			offGroup.alpha = hasCount ? 1f : offAlpha;
			amountText.text = hasCount ? $"x{count}" : string.Empty;
			if (nameText != null)
				SetName(card.Name);
		}

		public void SetItem(ItemData item, int count)
		{
			background.color = offBackground;
			outline.color = offOutline;
			icon.sprite = item.Sprite;
			offGroup.alpha = 1f;
			amountText.text = count.ToString();
			if (nameText != null)
				SetName(item.Name);
		}

		// 한글은 글자마다 끊겨 "증가" 가 "증 / 가" 로 갈림. 마지막 줄 한 글자면 마지막 공백에서 줄바꿈
		private void SetName(string displayName)
		{
			if (nameFontSize < 0f)
				nameFontSize = nameText.fontSize;
			nameText.fontSize = nameFontSize;
			nameText.text = displayName;
			UIEffects.AvoidOrphan(nameText, theme.MinCardNameFontSize);
		}
	}
}
