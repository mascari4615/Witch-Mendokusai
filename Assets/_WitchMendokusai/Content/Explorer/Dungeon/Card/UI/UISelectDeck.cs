using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace WitchMendokusai
{
	public class UISelectDeck : UIPanel
	{
		[SerializeField] private Transform selectDeckPanelContent;
		[SerializeField] private List<UISlot> deckSelectButtons;

		public override void UpdateUI()
		{
			selectDeckPanelContent.localScale = Vector3.zero;
			selectDeckPanelContent.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack).SetUpdate(true);

			// SelectDeckPanel이 켜지면 첫 번째 버튼 포커스
			deckSelectButtons[0].Select();
		}

		protected override void OnInit()
		{
			CardManager cardManager = Content as CardManager;
			for (int i = 0; i < deckSelectButtons.Count; i++)
			{
				deckSelectButtons[i].Init();
				deckSelectButtons[i].SetSlotIndex(i);
				deckSelectButtons[i].SetClickAction((slot) => { cardManager.SelectDeck(slot.Index); });
			}
		}

		public void SetDeckSelectButtons(List<EquipmentData> equipmentList)
		{
			for (int i = 0; i < deckSelectButtons.Count; i++)
			{
				if (i < equipmentList.Count)
				{
					deckSelectButtons[i].SetSlot(equipmentList[i]);
				}
				else
				{
					deckSelectButtons[i].SetSlot(null);
				}
			}
		}
	}
}