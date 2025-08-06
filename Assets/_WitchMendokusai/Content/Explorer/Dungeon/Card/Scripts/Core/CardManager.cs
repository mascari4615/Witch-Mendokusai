using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FMODUnity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;
using static WitchMendokusai.SOHelper;
using DG.Tweening;

namespace WitchMendokusai
{
	public enum CardPanelType
	{
		None = -1,
		SelectDeck = 0,
		SelectCard = 1,
	}

	public class CardManager : UIContentBase<CardPanelType>
	{
		private readonly Dictionary<int, UIDeck> deckUIDic = new();

		public override CardPanelType DefaultPanel => CardPanelType.None;

		// Level Up Stack
		private int levelUpStack = 0;

		// Data
		private readonly List<List<CardData>> cardDataBuffers = new(4) { new(), new(), new(), new() };
		private List<int> deckIdMapping = new() { 0, 1, 2, 3 };
		private int curDeckIndex;

		public override void Init()
		{
			Panels[CardPanelType.SelectDeck] = FindFirstObjectByType<UISelectDeck>(FindObjectsInactive.Include);
			Panels[CardPanelType.SelectCard] = FindFirstObjectByType<UISelectCard>(FindObjectsInactive.Include);

			UIDeck[] deckUIs = FindObjectsByType<UIDeck>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			foreach (UIDeck deckUI in deckUIs)
			{
				deckUIDic.Add(deckUI.EquipmentData.ID, deckUI);
				deckUI.Init();
				// deckUI.Init(cardSelectAction: (slot) => { SelectCard(slot.DataSO as CardData); });
			}
		}

		protected override void Start()
		{
			base.Start();
			GameEventManager.Instance.RegisterCallback(GameEventType.OnLevelUp, LevelUp);
		}

	
			// foreach (UIDeck deckUI in deckUIDic.Values)
			// 	deckUI.gameObject.SetActive(false);

		public void Reset()
		{
			SetPanel(CardPanelType.None);
			TimeManager.Instance.Resume(gameObject);

			// ClearCardEffect
			CardBuffer selectedCardBuffer = SOManager.Instance.SelectedCardBuffer;
			while (selectedCardBuffer.Data.Count > 0)
				selectedCardBuffer.Remove(selectedCardBuffer.Data[^1]);

			foreach (List<CardData> cardDataBuffer in cardDataBuffers)
				cardDataBuffer.Clear();

			// 덱 ID 매핑 초기화
			List<EquipmentData> equipments = DataManager.Instance.GetEquipmentData(DataManager.Instance.CurDollID);
			for (int i = 0; i < equipments.Count; i++)
			{
				if (equipments[i] == null)
					continue;
				cardDataBuffers[i].AddRange(equipments[i].EffectCards);
			}
		}

		private void ShuffleDeck()
		{
			List<EquipmentData> equipments = DataManager.Instance.GetEquipmentData(DataManager.Instance.CurDollID);
			deckIdMapping = deckIdMapping.OrderBy(m => Random.Range(0, 100)).ToList();
			(Panels[CardPanelType.SelectDeck] as UISelectDeck).SetDeckSelectButtons(equipments);
		}

		public void LevelUp()
		{
			levelUpStack++;
			if (levelUpStack > 1)
			{
				return;
			}

			StartCoroutine(StartSelectCard());
		}

		private IEnumerator StartSelectCard()
		{
			TimeManager.Instance.Pause(gameObject);
			yield return new WaitForSecondsRealtime(1f);
			ShuffleDeck();
			SetPanel(CardPanelType.SelectDeck);
		}

		public void SelectDeck(int selectIndex)
		{
			curDeckIndex = deckIdMapping[selectIndex];

			// 선택한 덱에서 카드 뽑기
			List<CardData> curDeckBuffer = cardDataBuffers[curDeckIndex];

			if (curDeckBuffer.Count == 0)
			{
				Debug.LogWarning("Not Enough Card Count");
				return;
			}

			List<CardData> randomCards = new();
			CardBuffer selectedCardBuffer = SOManager.Instance.SelectedCardBuffer;

			// HACK:
			int maxLoop = 100;
			while (randomCards.Count != 3)
			{
				if (--maxLoop < 0)
					break;

				int randomIndex = Random.Range(0, curDeckBuffer.Count);
				CardData randomCard = curDeckBuffer[randomIndex];

				if (randomCards.Contains(randomCard))
				{
					// Debug.LogWarning("Already Contains");
					continue;
				}

				if (randomCard.MaxStack == 0)
				{
					// Debug.LogWarning("MaxStack is 0");
					continue;
				}

				if (selectedCardBuffer.Data.Count > 0 &&
					selectedCardBuffer.Data.Where(m => m.ID == randomCard.ID).Count() >= randomCard.MaxStack)
				{
					// Debug.LogWarning($"MaxStack is Full {randomCard.ID} {randomCard.MaxStack}");
					continue;
				}

				randomCards.Add(randomCard);
			}

			(Panels[CardPanelType.SelectCard] as UISelectCard).SetCardSelectButtons(randomCards);

			SetPanel(CardPanelType.SelectCard);

			List<EquipmentData> equipmentData = DataManager.Instance.GetEquipmentData(DataManager.Instance.CurDollID);
			int equipmentID = equipmentData[curDeckIndex].ID;

			if (deckUIDic.TryGetValue(equipmentID, out UIDeck deckUI))
			{
				deckUI.SetCards(randomCards);
				deckUI.UpdateUI();
				deckUI.gameObject.SetActive(true);
			}
		}

		public void SelectCard(CardData card)
		{
			RuntimeManager.PlayOneShot("event:/SFX/UI/Click", transform.position);

			CardBuffer selectedCardBuffer = SOManager.Instance.SelectedCardBuffer;

			selectedCardBuffer.Add(card);

			int sameCardCount = selectedCardBuffer.Data.Where(m => m.ID == card.ID).Count();
			if (card.MaxStack == sameCardCount)
			{
				List<CardData> curDeckBuffer = cardDataBuffers[curDeckIndex];
				int cardIndex = curDeckBuffer.IndexOf(card);
				curDeckBuffer.RemoveAt(cardIndex);
			}

			levelUpStack--;

			if (levelUpStack > 0)
			{
				StartCoroutine(StartSelectCard());
			}
			else
			{
				SetPanel(CardPanelType.None);
				TimeManager.Instance.Resume(gameObject);
			}
		}
	}
}