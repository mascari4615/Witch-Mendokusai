using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UISelectDeck : UIPanel
	{
		[SerializeField] private Transform selectDeckPanelContent;
		[SerializeField] private List<UISlot> deckSelectButtons;
		// 패널이 튀어나오는 시간.
		[SerializeField] private float openAnimDuration = 0.5f;
		[SerializeField] private UICardChoiceActions actions;

		private UICardFocusSound focusSound;
		private UICardRevealDirector reveal;
		private readonly List<CardRevealEntry> revealEntries = new();
		private readonly HashSet<UISlot> newDeckSlots = new();

		public override bool IsFullscreen => true;
		public int ChoiceCount => deckSelectButtons.Count;

		public override void UpdateUI()
		{
			focusSound.Silence();
			actions.Refresh();
			actions.SetUpTarget(deckSelectButtons[0].Selectable);
			ApplyNavigation();

			// 연출을 끈 설정이면 예전 튀어나오기
			if (UIEffects.Enabled == false)
			{
				selectDeckPanelContent.localScale = Vector3.zero;
				selectDeckPanelContent.DOScale(Vector3.one, openAnimDuration).SetEase(Ease.OutBack).SetUpdate(true);
			}
			else
				PlayReveal();
			deckSelectButtons[0].Select();
		}

		private void PlayReveal()
		{
			revealEntries.Clear();
			UIChoiceCard firstCard = null;
			foreach (UISlot slot in deckSelectButtons)
			{
				if (slot.gameObject.activeInHierarchy == false || slot.DataSO == null)
					continue;
				// 새 장비는 희귀로 예고
				CardRevealTier tier = newDeckSlots.Contains(slot) ? CardRevealTier.Rare : CardRevealTier.Common;
				revealEntries.Add(new CardRevealEntry((RectTransform)slot.transform, tier));
				firstCard ??= slot.GetComponent<UIDeckChoiceView>().ChoiceCard;
			}
			reveal.Play(firstCard.Theme.Reveal.SelectDeck, revealEntries);
		}

		protected override void OnInit()
		{
			CardManager cardManager = PanelGroup as CardManager;
			focusSound = UICardFocusSound.AttachTo(gameObject);
			reveal = UICardRevealDirector.AttachTo(gameObject, (RectTransform)transform, selectDeckPanelContent);
			actions.Init(cardManager);
			actions.SetInputGate(reveal.ConsumeBlockedChoice);
			for (int i = 0; i < deckSelectButtons.Count; i++)
			{
				deckSelectButtons[i].Init();
				deckSelectButtons[i].SetSlotIndex(i);
				deckSelectButtons[i].SetClickAction((slot) =>
				{
					if (reveal.ConsumeBlockedChoice())
						return;
					cardManager.SelectDeck(slot.Index);
				});
			}
		}

		private void OnEnable() => UIHideWhileChatting.Request(this, true);

		private void OnDisable() => UIHideWhileChatting.Request(this, false);

		private void ApplyNavigation()
		{
			List<UISlot> visible = deckSelectButtons.FindAll(button => button.gameObject.activeInHierarchy);
			for (int i = 0; i < visible.Count; i++)
			{
				visible[i].SetNavigation(new Navigation
				{
					mode = Navigation.Mode.Explicit,
					selectOnLeft = i > 0 ? visible[i - 1].Selectable : null,
					selectOnRight = i < visible.Count - 1 ? visible[i + 1].Selectable : null,
					selectOnDown = actions.FirstEnabled,
				});
			}
		}

		public void SetDeckSelectButtons(List<EquipmentData> equipmentList, IReadOnlyList<CardData> selectedCards, ICollection<EquipmentData> newEquipments, SkillTreeCatalog skillTrees)
		{
			reveal.Cancel();
			newDeckSlots.Clear();
			for (int i = 0; i < deckSelectButtons.Count; i++)
			{
				if (i < equipmentList.Count && newEquipments.Contains(equipmentList[i]))
					newDeckSlots.Add(deckSelectButtons[i]);
				deckSelectButtons[i].transform.parent.gameObject.SetActive(i < equipmentList.Count);
				deckSelectButtons[i].SetActive(i < equipmentList.Count);
				if (i < equipmentList.Count)
				{
					deckSelectButtons[i].SetSlot(equipmentList[i]);
					deckSelectButtons[i].UpdateUI();
					deckSelectButtons[i].GetComponent<UIDeckChoiceView>().Set(equipmentList[i], skillTrees.CardsFor(equipmentList[i]), selectedCards, newEquipments.Contains(equipmentList[i]));
				}
				else
				{
					deckSelectButtons[i].SetSlot(null);
				}
			}
		}
	}
}