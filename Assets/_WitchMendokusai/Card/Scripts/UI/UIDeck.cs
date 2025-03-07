using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UIDeck : MonoBehaviour, IUI
	{
		[field: SerializeField] public EquipmentData EquipmentData { get; private set; }
		private List<UICardSlot> cardSlots;
		private List<CardData> cards;

		public void Init(Action<UISlot> cardSelectAction)
		{
			cardSlots = GetComponentsInChildren<UICardSlot>(true).ToList();

			for (int i = 0; i < cardSlots.Count; i++)
			{
				cardSlots[i].Init();
				// cardSlots[i].SetClickAction(cardSelectAction);
			}
		}

		public void SetCards(List<CardData> cards) => this.cards = cards;
		public void UpdateUI()
		{
			// HashSet : 고유한 값만 저장하는 자료구조
			// Convert cards to a HashSet for faster lookup
			HashSet<int> cardIds = new(cards.Select(m => m.ID));

			foreach (UICardSlot cardSlot in cardSlots)
			{
				bool isTargetCard = cardIds.Contains(cardSlot.DataSO.ID);
				cardSlot.SetDisable(!isTargetCard);
			}
		}
	}
}