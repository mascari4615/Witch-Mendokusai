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

		public override bool IsFullscreen => true;

		public override void UpdateUI()
		{
			actions.Refresh();
			actions.SetUpTarget(deckSelectButtons[0].Selectable);
			ApplyNavigation();
			selectDeckPanelContent.localScale = Vector3.zero;
			selectDeckPanelContent.DOScale(Vector3.one, openAnimDuration).SetEase(Ease.OutBack).SetUpdate(true);

			// SelectDeckPanel이 켜지면 첫 번째 버튼 포커스
			deckSelectButtons[0].Select();
			for (int i = 0; i < deckSelectButtons.Count; i++)
				if (deckSelectButtons[i].gameObject.activeSelf)
					{
					UIChoiceCard choiceCard = deckSelectButtons[i].GetComponent<UIChoiceCard>();
					choiceCard.PlayAppear(i);
					choiceCard.PlayOpenBurst(i);
				}
		}

		protected override void OnInit()
		{
			CardManager cardManager = PanelGroup as CardManager;
			actions.Init(cardManager);
			for (int i = 0; i < deckSelectButtons.Count; i++)
			{
				deckSelectButtons[i].Init();
				deckSelectButtons[i].SetSlotIndex(i);
				deckSelectButtons[i].SetClickAction((slot) => { cardManager.SelectDeck(slot.Index); });
			}
		}

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

		public void SetDeckSelectButtons(List<EquipmentData> equipmentList, IReadOnlyList<CardData> selectedCards)
		{
			for (int i = 0; i < deckSelectButtons.Count; i++)
			{
				deckSelectButtons[i].transform.parent.gameObject.SetActive(i < equipmentList.Count);
				deckSelectButtons[i].SetActive(i < equipmentList.Count);
				if (i < equipmentList.Count)
				{
					deckSelectButtons[i].SetSlot(equipmentList[i]);
					deckSelectButtons[i].UpdateUI();
					deckSelectButtons[i].GetComponent<UIDeckChoiceView>().Set(equipmentList[i], selectedCards);
				}
				else
				{
					deckSelectButtons[i].SetSlot(null);
				}
			}
		}
	}
}