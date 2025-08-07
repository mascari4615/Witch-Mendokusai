using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace WitchMendokusai
{
	public class UISelectCard : UIPanel
	{
		[SerializeField] private Transform deckPanelContent;
		[SerializeField] private List<UICardSlot> cardSelectButtons;

		public override void UpdateUI()
		{
			deckPanelContent.localScale = Vector3.zero;
			deckPanelContent.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack).SetUpdate(true);

			// DeckPanel이 켜지면 첫 번째 카드 버튼 포커스
			cardSelectButtons[0].Select();
		}

		protected override void OnInit()
		{
			CardManager cardManager = Content as CardManager;
			for (int i = 0; i < cardSelectButtons.Count; i++)
			{
				// cardSelectAction: (slot) => { SelectCard(cardSlots[i].Artifact as Card); }
				// 원래 위 코드를 썼는데, 클로저 문제로 인해 아래처럼 바꿈
				cardSelectButtons[i].Init();
				cardSelectButtons[i].SetClickAction((slot) => { cardManager.SelectCard(slot.DataSO as CardData); });
			}
		}
		
		public void SetCardSelectButtons(List<CardData> cardDataList)
		{
			for (int i = 0; i < cardSelectButtons.Count; i++)
			{
				if (i < cardDataList.Count)
				{
					cardSelectButtons[i].SetSlot(cardDataList[i]);
					cardSelectButtons[i].UpdateUI();
				}
				else
				{
					cardSelectButtons[i].SetSlot(null);
				}
			}
		}
	}
}