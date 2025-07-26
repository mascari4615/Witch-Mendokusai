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
	public class CardManager : MonoBehaviour
	{
		private enum CardUIState
		{
			Wait,
			SelectDeck,
			SelectCard,
		}

		// UI
		[SerializeField] private Transform selectDeckPanel;
		[SerializeField] private Transform selectDeckPanelContent;
		[SerializeField] private List<UISlot> deckSelectButtons;

		[SerializeField] private Transform deckPanel;
		[SerializeField] private Transform deckPanelContent;
		private readonly Dictionary<int, UIDeck> deckUIDic = new();
		[SerializeField] private List<UICardSlot> cardSelectButtons;

		// Level Up Stack
		private int levelUpStack = 0;

		// Data
		private CardUIState curState = CardUIState.Wait;
		private readonly List<List<CardData>> cardDataBuffers = new(4) { new(), new(), new(), new() };
		private List<int> deckIdMapping = new() { 0, 1, 2, 3 };
		private int curDeckIndex;

		private void Awake()
		{
			UIDeck[] deckUIs = FindObjectsByType<UIDeck>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			foreach (UIDeck deckUI in deckUIs)
			{
				deckUIDic.Add(deckUI.EquipmentData.ID, deckUI);
				deckUI.Init();
				// deckUI.Init(cardSelectAction: (slot) => { SelectCard(slot.DataSO as CardData); });
			}

			for (int i = 0; i < deckSelectButtons.Count; i++)
			{
				deckSelectButtons[i].Init();
				deckSelectButtons[i].SetSlotIndex(i);
				deckSelectButtons[i].SetClickAction((slot) => { SelectDeck(slot.Index); });
			}

			for (int i = 0; i < cardSelectButtons.Count; i++)
			{
				// cardSelectAction: (slot) => { SelectCard(cardSlots[i].Artifact as Card); }
				// 원래 위 코드를 썼는데, 클로저 문제로 인해 아래처럼 바꿈
				cardSelectButtons[i].Init();
				cardSelectButtons[i].SetClickAction((slot) => { SelectCard(slot.DataSO as CardData); });
			}

			SetState(CardUIState.Wait);
		}

		private void Start()
		{
			GameEventManager.Instance.RegisterCallback(GameEventType.OnLevelUp, LevelUp);
		}

		private void SetState(CardUIState state)
		{
			curState = state;

			switch (curState)
			{
				case CardUIState.Wait:
					TimeManager.Instance.Resume();
					break;
				case CardUIState.SelectDeck:
					TimeManager.Instance.Pause();
					break;
				case CardUIState.SelectCard:
					TimeManager.Instance.Pause();
					break;
				default:
					break;
			}

			selectDeckPanel.gameObject.SetActive(curState == CardUIState.SelectDeck);
			if (selectDeckPanel.gameObject.activeSelf)
			{
				selectDeckPanelContent.localScale = Vector3.zero;
				selectDeckPanelContent.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack).SetUpdate(true);

				// SelectDeckPanel이 켜지면 첫 번째 버튼 포커스
				deckSelectButtons[0].Select();
			}

			deckPanel.gameObject.SetActive(curState == CardUIState.SelectCard);
			if (deckPanel.gameObject.activeSelf)
			{
				deckPanelContent.localScale = Vector3.zero;
				deckPanelContent.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack).SetUpdate(true);

				// DeckPanel이 켜지면 첫 번째 카드 버튼 포커스
				cardSelectButtons[0].Select();
			}

			foreach (UIDeck deckUI in deckUIDic.Values)
				deckUI.gameObject.SetActive(false);
		}

		public void Init()
		{
			SetState(CardUIState.Wait);
			CardBuffer selectedCardBuffer = SOManager.Instance.SelectedCardBuffer;

			while (selectedCardBuffer.Data.Count > 0)
				selectedCardBuffer.Remove(selectedCardBuffer.Data[^1]);
			selectedCardBuffer.Clear();

			foreach (List<CardData> cardDataBuffer in cardDataBuffers)
				cardDataBuffer.Clear();

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
			for (int i = 0; i < deckSelectButtons.Count; i++)
				deckSelectButtons[i].SetSlot(equipments[deckIdMapping[i]]);
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
			TimeManager.Instance.Pause();
			yield return new WaitForSecondsRealtime(1f);
			ShuffleDeck();
			SetState(CardUIState.SelectDeck);
		}

		public void SelectDeck(int selectIndex)
		{
			curDeckIndex = deckIdMapping[selectIndex];

			// 선택한 덱에서 카드 뽑기
			List<CardData> curDeckBuffer = cardDataBuffers[curDeckIndex];

			if (curDeckBuffer.Count < 3)
			{
				Debug.LogError("Not Enough Card Count");
				return;
			}

			List<CardData> randomCards = new();
			CardBuffer selectedCardBuffer = SOManager.Instance.SelectedCardBuffer;

			// HACK:
			int maxLoop = 100;
			while (randomCards.Count < 3)
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

				cardSelectButtons[randomCards.Count].SetSlot(randomCard);
				randomCards.Add(randomCard);
			}

			// HACK:
			if (randomCards.Count < 3)
			{
				for (int i = randomCards.Count; i < 3; i++)
				{
					cardSelectButtons[i].SetSlot(null);
					randomCards.Add(null);
				}
			}

			SetState(CardUIState.SelectCard);

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

			List<CardData> curDeckBuffer = cardDataBuffers[curDeckIndex];
			selectedCardBuffer.Add(card);

			int sameCardCount = selectedCardBuffer.Data.Where(m => m.ID == card.ID).Count();
			if (card.MaxStack == sameCardCount)
			{
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
				SetState(CardUIState.Wait);
			}
		}

		public void ClearCardEffect()
		{
			CardBuffer selectedCardBuffer = SOManager.Instance.SelectedCardBuffer;
			while (selectedCardBuffer.Data.Count > 0)
				selectedCardBuffer.Remove(selectedCardBuffer.Data[^1]);
		}
	}
}