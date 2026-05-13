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
using VContainer;

namespace WitchMendokusai
{
	public enum CardPanelType
	{
		None = -1,
		SelectDeck = 0,
		SelectCard = 1,
	}

	public class CardManager : UIPanelGroup<CardPanelType>
	{
		private readonly Dictionary<int, UIDeck> deckUIDic = new();

		public override bool CanBeClosedByCancelInput => false;
		public override CardPanelType DefaultPanel => CardPanelType.None;

		private GameEventManager gameEventManager;
		private TimeManager timeManager;
		private SOManager soManager;
		private DataManager dataManager;

		[Inject]
		public void Construct(GameEventManager gameEventManager, TimeManager timeManager, SOManager soManager, DataManager dataManager)
		{
			this.gameEventManager = gameEventManager;
			this.timeManager = timeManager;
			this.soManager = soManager;
			this.dataManager = dataManager;
		}

		// Level Up Stack
		private int levelUpStack = 0;

		// Data
		private readonly List<List<CardData>> cardDataBuffers = new(4) { new(), new(), new(), new() };
		private List<int> deckIdMapping = new() { 0, 1, 2, 3 };
		private int curDeckIndex;

		public override void Init()
		{
			UISelectDeck selectDeckPanel = FindAnyObjectByType<UISelectDeck>(FindObjectsInactive.Include);
			UISelectCard selectCardPanel = FindAnyObjectByType<UISelectCard>(FindObjectsInactive.Include);

			if (selectDeckPanel == null)
				Debug.LogError($"[{nameof(CardManager)}] {nameof(UISelectDeck)} 미발견 — 씬 hierarchy 에 UISelectDeck 컴포넌트 가진 GameObject 없음. SetPanel(SelectDeck) 시 NPE.");
			if (selectCardPanel == null)
				Debug.LogError($"[{nameof(CardManager)}] {nameof(UISelectCard)} 미발견 — 씬 hierarchy 에 UISelectCard 컴포넌트 가진 GameObject 없음. SetPanel(SelectCard) 시 NPE.");

			Panels[CardPanelType.SelectDeck] = selectDeckPanel;
			Panels[CardPanelType.SelectCard] = selectCardPanel;

			UIDeck[] deckUIs = FindObjectsByType<UIDeck>(FindObjectsInactive.Include);
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
			gameEventManager.RegisterCallback(GameEventType.OnLevelUp, LevelUp);
		}

	
			// foreach (UIDeck deckUI in deckUIDic.Values)
			// 	deckUI.gameObject.SetActive(false);

		public void Reset()
		{
			SetPanel(CardPanelType.None);
			timeManager.Resume(gameObject);

			// ClearCardEffect
			CardBuffer selectedCardBuffer = soManager.SelectedCardBuffer;
			while (selectedCardBuffer.Data.Count > 0)
				selectedCardBuffer.Remove(selectedCardBuffer.Data[^1]);

			foreach (List<CardData> cardDataBuffer in cardDataBuffers)
				cardDataBuffer.Clear();

			// 덱 ID 매핑 초기화
			List<EquipmentData> equipments = dataManager.GetEquipmentData(dataManager.CurDollID);
			for (int i = 0; i < equipments.Count; i++)
			{
				if (equipments[i] == null)
					continue;
				cardDataBuffers[i].AddRange(equipments[i].EffectCards);
			}
		}

		private void ShuffleDeck()
		{
			List<EquipmentData> equipments = dataManager.GetEquipmentData(dataManager.CurDollID);

			deckIdMapping = deckIdMapping.OrderBy(m => Random.Range(0, 100)).ToList();
		
			List<EquipmentData> shuffledEquipments = new(deckIdMapping.Count);
			foreach (int index in deckIdMapping)
				shuffledEquipments.Add(equipments[index]);
		
			(Panels[CardPanelType.SelectDeck] as UISelectDeck).SetDeckSelectButtons(shuffledEquipments);
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
			timeManager.Pause(gameObject);
			yield return new WaitForSecondsRealtime(1f);
			
			// 선택한 덱에서 카드 뽑기
			List<CardData> curDeckBuffer = cardDataBuffers[curDeckIndex];

			if (curDeckBuffer.Count == 0)
			{
				Debug.LogWarning("Not Enough Card Count");
				timeManager.Resume(gameObject);
				yield break;
			}

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
			CardBuffer selectedCardBuffer = soManager.SelectedCardBuffer;

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

			List<EquipmentData> equipmentData = dataManager.GetEquipmentData(dataManager.CurDollID);
			int equipmentID = equipmentData[curDeckIndex].ID;

			foreach (UIDeck deckUI in deckUIDic.Values)
				deckUI.gameObject.SetActive(false);

			if (deckUIDic.TryGetValue(equipmentID, out UIDeck targetDeckUI))
			{
				targetDeckUI.SetCards(randomCards);
				targetDeckUI.UpdateUI();
				targetDeckUI.gameObject.SetActive(true);
			}
		}

		public void SelectCard(CardData card)
		{
			RuntimeManager.PlayOneShot("event:/SFX/UI/Click", transform.position);

			if (card == null)
			{
				Debug.LogWarning("Card is null");
				return;
			}

			CardBuffer selectedCardBuffer = soManager.SelectedCardBuffer;

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
				timeManager.Resume(gameObject);
			}
		}
	}
}