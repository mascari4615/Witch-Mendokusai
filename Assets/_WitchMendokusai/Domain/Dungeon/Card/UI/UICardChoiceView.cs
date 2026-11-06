using System.Collections.Generic;
using System.Linq;
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
		[SerializeField] private string rareLabel = "희귀";
		[SerializeField] private string epicLabel = "영웅";
		[SerializeField] private Color rareColor = new(0.373f, 0.831f, 1f, 1f);
		[SerializeField] private Color epicColor = new(0.949f, 0.886f, 0.478f, 1f);

		private readonly List<Image> pips = new();

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

			bool hasRarity = card.Rarity != CardRarity.Common;
			rarityRoot.SetActive(hasRarity);
			if (hasRarity)
			{
				rarityBack.color = card.Rarity == CardRarity.Epic ? epicColor : rareColor;
				rarityText.text = card.Rarity == CardRarity.Epic ? epicLabel : rareLabel;
			}

			int current = selectedCards.Count(selected => selected.ID == card.ID);
			stackText.text = $"강화 {current} → {Mathf.Min(current + 1, card.MaxStack)} / {card.MaxStack}회";
			UpdateMeter(card.MaxStack, current, style.Main);

			bool hasCondition = CardChoiceText.TryGetCondition(card, selectedCards, deckCards, out CardData advanced, out string condition);
			conditionRoot.SetActive(hasCondition);
			if (hasCondition)
			{
				conditionIcon.sprite = advanced.Sprite;
				conditionText.text = condition;
			}
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
