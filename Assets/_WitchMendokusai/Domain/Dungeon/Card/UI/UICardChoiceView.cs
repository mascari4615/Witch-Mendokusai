using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 카드 선택 칸: 종류 색, 장비 띠, 강화 칸, 조건
	public class UICardChoiceView : MonoBehaviour
	{
		[SerializeField] private SurvivalUITheme theme;
		[SerializeField] private UIChoiceCard choiceCard;
		[SerializeField] private Image diamond;
		[SerializeField] private Image diamondLine;
		[SerializeField] private Image tile;
		[SerializeField] private Image bandIcon;
		[SerializeField] private TextMeshProUGUI bandText;
		[SerializeField] private TextMeshProUGUI nameText;
		[SerializeField] private float nameFontSize = 28f;
		[SerializeField] private TextMeshProUGUI stackText;
		[SerializeField] private Transform pipParent;
		[SerializeField] private Image pipTemplate;
		[SerializeField] private Color pipEmpty = new(0.05f, 0.05f, 0.07f, 1f);
		[SerializeField] private Color pipNext = new(1f, 1f, 1f, 0.5f);
		[SerializeField] private int maxPips = 10;
		[SerializeField] private RectTransform barFill;
		[SerializeField] private RectTransform barNext;
		[SerializeField] private GameObject barRoot;
		[SerializeField] private GameObject conditionRoot;
		[SerializeField] private Image conditionIcon;
		[SerializeField] private TextMeshProUGUI conditionText;
		[SerializeField] private GameObject rarityRoot;
		[SerializeField] private Image rarityBack;
		[SerializeField] private TextMeshProUGUI rarityText;
		[SerializeField] private Sprite[] seals;
		[SerializeField] private string evolutionLabel = "진화";
		[SerializeField] private float evolutionBadgeGap = 4f;

		private readonly List<Image> pips = new();
		private GameObject evolutionBadge;

		public UIChoiceCard ChoiceCard => choiceCard;

		public void Set(CardData card, EquipmentData equipment, IReadOnlyList<CardData> selectedCards, IReadOnlyList<CardData> deckCards)
		{
			SurvivalUITheme.KindStyle style = theme.Style(card.UIKind);
			choiceCard.SetKind(card.UIKind);
			diamond.color = style.Dark;
			diamondLine.color = style.Main;
			tile.sprite = style.Tile;
			bandIcon.sprite = equipment.Sprite;
			bandText.text = equipment.Name;

			nameText.fontSize = nameFontSize;
			nameText.text = card.Name;
			UIEffects.AvoidOrphan(nameText, theme.MinCardNameFontSize);

			SetEvolutionBadge(card);
			ShowTier(UICardRevealDirector.TierOf(card));

			int current = selectedCards.Count(selected => selected.ID == card.ID);
			string action = current == 0 ? "획득" : "강화";
			stackText.text = $"{action} {current} → {Mathf.Min(current + 1, card.MaxStack)} / {card.MaxStack}회";
			UpdateMeter(card.MaxStack, current, style.Main);

			bool hasCondition = CardChoiceText.TryGetCondition(card, selectedCards, deckCards, out CardData advanced, out string condition);
			conditionRoot.SetActive(hasCondition);
			if (hasCondition)
			{
				conditionIcon.sprite = advanced.Sprite;
				conditionText.text = condition;
			}
		}

		// 진화 표식. 희귀도 표식의 사본을 그 아래에 배치, 첫 Set에서 UITermIcon 부착 전에 복제
		private void SetEvolutionBadge(CardData card)
		{
			if (evolutionBadge == null)
			{
				evolutionBadge = Instantiate(rarityRoot, rarityRoot.transform.parent);
				evolutionBadge.transform.SetSiblingIndex(rarityRoot.transform.GetSiblingIndex() + 1);
				RectTransform source = rarityRoot.GetComponent<RectTransform>();
				evolutionBadge.GetComponent<RectTransform>().anchoredPosition = source.anchoredPosition - new Vector2(0f, source.sizeDelta.y + evolutionBadgeGap);
				evolutionBadge.GetComponent<Image>().color = theme.Style(SurvivalUIKind.Advanced).Main;
				TextMeshProUGUI label = evolutionBadge.GetComponentInChildren<TextMeshProUGUI>(true);
				label.text = evolutionLabel;
				UITermIcon.Attach(label, theme, SurvivalTerm.Evolution);
			}
			evolutionBadge.SetActive(card.UIKind == SurvivalUIKind.Advanced);
		}

		public void ShowReceipt(CardData card, int count, Canvas canvas)
		{
			TextMeshProUGUI label = Instantiate(stackText, canvas.transform, false);
			label.gameObject.SetActive(true);
			label.raycastTarget = false;
			label.enableAutoSizing = true;
			label.fontSizeMin = 16f;
			label.fontSizeMax = 28f;
			label.alignment = TextAlignmentOptions.Right;
			label.outlineWidth = 0.24f;
			label.outlineColor = new Color32(29, 25, 27, 255);
			label.text = count == 1 ? $"{card.Name} 획득  ×1" : $"{card.Name} 강화  ×{count - 1} → ×{count}";
			RectTransform rect = label.rectTransform;
			rect.anchorMin = rect.anchorMax = new Vector2(0.96f, 0.88f);
			rect.pivot = Vector2.right;
			rect.sizeDelta = new Vector2(410f, 50f);
			rect.anchoredPosition = Vector2.zero;
			label.gameObject.AddComponent<CanvasGroup>().DOFade(0f, 0.3f).SetDelay(1.05f).SetUpdate(true)
				.SetLink(label.gameObject).OnComplete(() => Destroy(label.gameObject));
		}

		public void ShowTier(CardRevealTier tier)
		{
			rarityRoot.SetActive(true);
			rarityText.gameObject.SetActive(false);
			rarityBack.type = Image.Type.Simple;
			rarityBack.preserveAspect = true;
			rarityBack.color = Color.white;
			rarityBack.sprite = seals[(int)tier];
		}

		private void UpdateMeter(int maxStack, int current, Color fillColor)
		{
			bool usePips = maxStack <= maxPips;
			pipParent.gameObject.SetActive(usePips);
			barRoot.SetActive(usePips == false);
			pipTemplate.gameObject.SetActive(false);

			if (usePips == false)
			{
				barFill.anchorMax = new Vector2((float)current / maxStack, 1f);
				float next = (float)(current + 1) / maxStack;
				barNext.anchorMin = new Vector2(next, 0f);
				barNext.anchorMax = new Vector2(next, 1f);
				return;
			}

			while (pips.Count < maxStack)
				pips.Add(Instantiate(pipTemplate, pipParent));
			for (int i = 0; i < pips.Count; i++)
			{
				bool visible = i < maxStack;
				pips[i].gameObject.SetActive(visible);
				if (visible)
					pips[i].color = i < current ? fillColor : i == current ? pipNext : pipEmpty;
			}
		}
	}
}
