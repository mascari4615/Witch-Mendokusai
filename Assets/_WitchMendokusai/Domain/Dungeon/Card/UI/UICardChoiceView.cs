using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UICardChoiceView : MonoBehaviour
	{
		[SerializeField] private UIChoiceCard choiceCard;
		[SerializeField] private Image face;
		[SerializeField] private Image seal;
		[SerializeField] private Sprite[] seals;
		[SerializeField] private GameObject symbolRoot;
		[SerializeField] private Sprite plain, sword, flame, stride;
		[SerializeField] private EquipmentData swordEquipment, flameEquipment;
		[SerializeField] private CardData strideCard;
		[SerializeField] private TextMeshProUGUI bandText, nameText, stackText;

		[SerializeField] private Transform pipParent;
		[SerializeField] private Image pipTemplate;
		[SerializeField] private Color pipEmpty = new(0.05f, 0.05f, 0.07f, 1f);
		[SerializeField] private Color pipNext = new(1f, 1f, 1f, 0.5f);
		[SerializeField] private int maxPips = 10;
		[SerializeField] private RectTransform barFill;
		[SerializeField] private RectTransform barNext;
		[SerializeField] private GameObject barRoot;
		private readonly List<Image> pips = new();

		public UIChoiceCard ChoiceCard => choiceCard;

		public void Set(CardData card, EquipmentData equipment, IReadOnlyList<CardData> selectedCards)
		{
			choiceCard.SetKind(card.UIKind);
			Sprite illustration = plain;
			if (card == strideCard)
				illustration = stride;
			else if (equipment == swordEquipment)
				illustration = sword;
			else if (equipment == flameEquipment)
				illustration = flame;
			face.sprite = illustration;
			symbolRoot.SetActive(illustration == plain);
			ShowTier(UICardRevealDirector.TierOf(card));
			bandText.text = equipment.Name;
			nameText.text = card.Name;
			int current = selectedCards.Count(selected => selected.ID == card.ID);
			UpdateMeter(card.MaxStack, current, choiceCard.Theme.Style(card.UIKind).Main);
			string action = current == 0 ? "획득" : "강화";
			stackText.text = $"{action}  {current} → {Mathf.Min(current + 1, card.MaxStack)} / {card.MaxStack}";
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

		public void ShowTier(CardRevealTier tier) => seal.sprite = seals[(int)tier];
	}
}
