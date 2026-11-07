using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// TMP 글자 왼쪽의 용어 아이콘. 스프라이트는 SurvivalUITheme.Terms 표, 색은 글자 색
	public class UITermIcon : MonoBehaviour
	{
		private TextMeshProUGUI label;
		private SurvivalUITheme theme;
		private RectTransform iconRect;
		private Image image;
		private string lastText;

		public static void Attach(TextMeshProUGUI label, SurvivalUITheme theme, SurvivalTerm term)
		{
			UITermIcon termIcon = label.GetComponent<UITermIcon>();
			if (termIcon == null)
			{
				termIcon = label.gameObject.AddComponent<UITermIcon>();
				termIcon.Build(label, theme);
			}
			termIcon.image.sprite = theme.TermIcon(term);
		}

		private void Build(TextMeshProUGUI targetLabel, SurvivalUITheme targetTheme)
		{
			label = targetLabel;
			theme = targetTheme;
			GameObject icon = new("[Image] TermIcon", typeof(RectTransform), typeof(Image));
			icon.layer = label.gameObject.layer;
			iconRect = icon.GetComponent<RectTransform>();
			iconRect.SetParent(label.transform, false);
			iconRect.anchorMin = label.rectTransform.pivot;
			iconRect.anchorMax = label.rectTransform.pivot;
			image = icon.GetComponent<Image>();
			image.raycastTarget = false;
			image.preserveAspect = true;
		}

		private void OnEnable() => lastText = null;

		private void LateUpdate()
		{
			string text = label.text;
			bool hasText = string.IsNullOrEmpty(text) == false;
			image.enabled = hasText;
			if (hasText == false)
				return;

			float size = label.fontSize * theme.TermIconSizeRatio;
			float gap = label.fontSize * theme.TermIconGapRatio;
			Vector4 margin = label.margin;
			if (Mathf.Approximately(margin.x, size + gap) == false)
			{
				margin.x = size + gap;
				label.margin = margin;
				lastText = null;
			}
			if (text != lastText)
			{
				lastText = text;
				label.ForceMeshUpdate();
			}

			Bounds bounds = label.textBounds;
			iconRect.sizeDelta = new Vector2(size, size);
			iconRect.anchoredPosition = new Vector2(bounds.min.x - gap - size * 0.5f, bounds.center.y);
			image.color = label.color;
		}
	}
}
