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

		public void ShowTier(CardRevealTier tier) => seal.sprite = seals[(int)tier];
	}
}
