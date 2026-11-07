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

	public partial class CardManager : UIPanelGroup<CardPanelType>
	{
		private readonly Dictionary<int, UIDeck> deckUIDic = new();

		public override bool CanBeClosedByCancelInput => false;
		public override CardPanelType DefaultPanel => CardPanelType.None;

		private GameEventManager gameEventManager;
		private TimeManager timeManager;
		private SOManager soManager;
		private DataManager dataManager;
		private PlayerProvider playerProvider;
		private CardAwakeningEffect awakeningEffect;
		private ResourceManager resourceManager;
		private GameManager gameManager;

		private RunLoadout Loadout => dataManager.RunLoadout;

		[Inject]
		public void Construct(GameEventManager gameEventManager, TimeManager timeManager, SOManager soManager, DataManager dataManager, UIManager uiManager, PlayerProvider playerProvider, ResourceManager resourceManager, GameManager gameManager)
		{
			this.gameManager = gameManager;
			this.resourceManager = resourceManager;
			this.gameEventManager = gameEventManager;
			this.timeManager = timeManager;
			this.soManager = soManager;
			this.dataManager = dataManager;
			this.playerProvider = playerProvider;
			SetUIManager(uiManager);
		}

		// Level Up Stack
		private int levelUpStack = 0;

		// Data
		private readonly List<List<CardData>> cardDataBuffers = new(4) { new(), new(), new(), new() };
		private readonly List<RunDeckOffer> deckOffers = new();
		private readonly List<CardData> offeredCards = new();
		private int curDeckIndex;
		[SerializeField] private float selectionDelay = 1f;
		[SerializeField] private int cardsPerChoice = 3;
		[SerializeField, Min(0f), Tooltip("선택 직후 무적 초")] private float postSelectionInvincibility = 1f;
		[SerializeField, Min(0), Tooltip("고를 카드가 없을 때 체력이 닳아 있으면 레벨업 하나당 회복량")] private int maxedLevelUpHeal = 5;
		[SerializeField, Min(0), Tooltip("고를 카드가 없고 체력이 가득이면 레벨업 하나당 냥")] private int maxedLevelUpNyang = 10;
		[SerializeField, Min(0f)] private float commonWeight = 1f;
		[SerializeField, Min(0f)] private float rareWeight = 0.5f;
		[SerializeField, Min(0f)] private float epicWeight = 0.2f;

		// TASK-WM-115 R1 — 카드 선택 UI = 던전전용 (사용자 확정: World 부재=정상).
		// 구: Init(World Awake) 이 eager Find → 미발견 LogError + Panels 에 null 저장
		//   → UIPanelGroup.Start 가 Panels.Values 순회 null.Init() NRE (#2/#3/#5).
		// 근본: eager → lazy. 사용 시점(던전 카드 흐름)에 Find→register→per-panel 셋업.
		// World 부재는 정상이라 무음(LogError X, null 저장 X). 던전인데 진짜 없으면
		// Panels[..] 직접 인덱스(line 118/199)가 자연 FastFail (방어코드 X).
		public override void Init()
		{
			// init-order-ok: UIDeck = 씬배치 정적 UI(동적 스폰 X), Awake-invoked Init 시점 존재 보장. cross-manager Start-order 의존 아님 = 마스킹체인 클래스 외 (WM-118 적용외).
			UIDeck[] deckUIs = FindObjectsByType<UIDeck>(FindObjectsInactive.Include); // init-order-ok
			foreach (UIDeck deckUI in deckUIs)
			{
				deckUIDic.Add(deckUI.EquipmentData.ID, deckUI);
				deckUI.Init();
			}
		}

		// 던전 카드 흐름 진입 시 패널 lazy 확정. UIPanelGroup.Start 의 per-panel
		// 셋업(Init(this)+SetActive(false))을 lazy-add 패널에 복제. 멱등.
		private void EnsureCardPanels()
		{
			if (Panels.ContainsKey(CardPanelType.SelectDeck) == false)
			{
				UISelectDeck selectDeckPanel = FindAnyObjectByType<UISelectDeck>(FindObjectsInactive.Include);
				if (selectDeckPanel != null)
				{
					Panels[CardPanelType.SelectDeck] = selectDeckPanel;
					selectDeckPanel.Init(this);
					selectDeckPanel.SetActive(false);
				}
			}

			if (Panels.ContainsKey(CardPanelType.SelectCard) == false)
			{
				UISelectCard selectCardPanel = FindAnyObjectByType<UISelectCard>(FindObjectsInactive.Include);
				if (selectCardPanel != null)
				{
					Panels[CardPanelType.SelectCard] = selectCardPanel;
					selectCardPanel.Init(this);
					selectCardPanel.SetActive(false);
				}
			}
		}

		protected override void Start()
		{
			base.Start();
			gameEventManager.RegisterCallback(GameEventType.OnLevelUp, LevelUp);
			EventBusBridge.Subscribe<ChestPickedUpEvent>(HandleChestPickedUp);
		}

		private void OnDisable() => CancelSelection();

		private void OnDestroy()
		{
			gameEventManager?.UnregisterCallback(GameEventType.OnLevelUp, LevelUp);
			EventBusBridge.Unsubscribe<ChestPickedUpEvent>(HandleChestPickedUp);
		}

		public void CancelSelection()
		{
			if (awakeningEffect != null)
			{
				Destroy(awakeningEffect.gameObject);
			}
			StopAllCoroutines();
			ClearOpenChest();
			levelUpStack = 0;
			curDeckIndex = 0;
			deckOffers.Clear();
			offeredCards.Clear();
			SetPanel(CardPanelType.None);
			if (timeManager != null)
			{
				timeManager.Resume(gameObject);
			}
			foreach (UIDeck deckUI in deckUIDic.Values)
			{
				if (deckUI != null)
					deckUI.gameObject.SetActive(false);
			}
		}

		public void Reset()
		{
			CancelSelection();
			ResetRunChoices();

			// ClearCardEffect
			CardBuffer selectedCardBuffer = soManager.SelectedCardBuffer;
			while (selectedCardBuffer.Data.Count > 0)
				selectedCardBuffer.Remove(selectedCardBuffer.Data[^1]);

			foreach (List<CardData> cardDataBuffer in cardDataBuffers)
				cardDataBuffer.Clear();

			// 덱 ID 매핑 초기화
			List<EquipmentData> equipments = Loadout.Equipments();
			while (cardDataBuffers.Count < equipments.Count)
			{
				cardDataBuffers.Add(new List<CardData>());
			}
			for (int i = 0; i < equipments.Count; i++)
			{
				if (equipments[i] == null)
					continue;
				cardDataBuffers[i].AddRange(equipments[i].EffectCards);
			}
		}

		private List<CardData> AvailableCards(int deckIndex)
		{
			return cardDataBuffers[deckIndex]
				.Where(card => card != null && card.MaxStack > 0 && bannedCardIds.Contains(card.ID) == false && card.MeetsRequirements(soManager.SelectedCardBuffer.Data) &&
					soManager.SelectedCardBuffer.Data.Count(selected => selected.ID == card.ID) < card.MaxStack)
				.GroupBy(card => card.ID)
				.Select(group => group.First())
				.ToList();
		}

		private bool ShuffleDeck()
		{
			List<EquipmentData> equipments = Loadout.Equipments();
			List<int> ownedDecks = new();
			for (int i = 0; i < equipments.Count; i++)
			{
				if (equipments[i] != null && AvailableCards(i).Count > 0)
				{
					ownedDecks.Add(i);
				}
			}
			UISelectDeck deckPanel = Panels[CardPanelType.SelectDeck] as UISelectDeck;
			deckOffers.Clear();
			deckOffers.AddRange(Loadout.BuildDeckOffers(RunRules, ownedDecks, deckPanel.ChoiceCount, maxExclusive => Random.Range(0, maxExclusive)));
			if (deckOffers.Count == 0)
			{
				return false;
			}

			List<EquipmentData> offeredEquipments = new(deckOffers.Count);
			HashSet<EquipmentData> newEquipments = new();
			foreach (RunDeckOffer offer in deckOffers)
			{
				EquipmentData equipment = offer.IsNew ? RunLoadout.GetEquipment(offer.NewEquipmentId) : equipments[offer.DeckIndex];
				offeredEquipments.Add(equipment);
				if (offer.IsNew)
					newEquipments.Add(equipment);
			}

			deckPanel.SetDeckSelectButtons(offeredEquipments, soManager.SelectedCardBuffer.Data, newEquipments);
			return true;
		}

		public void LevelUp()
		{
			levelUpStack++;
			if (levelUpStack > 1 || isChestOpen)
			{
				return;
			}

			StartCoroutine(StartSelectCard());
		}

		private IEnumerator StartSelectCard()
		{
			timeManager.Pause(gameObject);
			yield return new WaitForSecondsRealtime(selectionDelay);
			EnsureCardPanels();
			if (ShuffleDeck() == false)
			{
				ConvertLevelUps();
				CancelSelection();
				TryOpenNextChest();
				yield break;
			}

			SetPanel(CardPanelType.SelectDeck);
		}

		public void SelectDeck(int selectIndex)
		{
			if (CurPanelType != CardPanelType.SelectDeck || selectIndex < 0 || selectIndex >= deckOffers.Count)
			{
				return;
			}
			RunDeckOffer offer = deckOffers[selectIndex];
			if (offer.IsNew)
			{
				AcquireEquipment(RunLoadout.GetEquipment(offer.NewEquipmentId));
				return;
			}
			curDeckIndex = offer.DeckIndex;
			List<CardData> candidates = AvailableCards(curDeckIndex);
			if (candidates.Count == 0)
			{
				SetPanel(CardPanelType.None);
				StartCoroutine(StartSelectCard());
				return;
			}

			offeredCards.Clear();
			int choiceCount = Math.Min(Math.Max(cardsPerChoice, 1), candidates.Count);
			DrawOfferedCards(candidates, choiceCount);
			ShowOfferedCards();
		}

		private void ShowOfferedCards()
		{
			List<EquipmentData> choiceEquipments = Loadout.Equipments();
			(Panels[CardPanelType.SelectCard] as UISelectCard).SetCardSelectButtons(
				offeredCards, choiceEquipments[curDeckIndex], choiceEquipments, soManager.SelectedCardBuffer.Data, cardDataBuffers[curDeckIndex]);

			SetPanel(CardPanelType.SelectCard);

			int equipmentID = choiceEquipments[curDeckIndex].ID;

			foreach (UIDeck deckUI in deckUIDic.Values)
				if (deckUI != null)
					deckUI.gameObject.SetActive(false);

			(Panels[CardPanelType.SelectCard] as UISelectCard).SetTreeAvailable(deckUIDic.ContainsKey(equipmentID));
			if (deckUIDic.TryGetValue(equipmentID, out UIDeck targetDeckUI))
			{
				targetDeckUI.SetCards(offeredCards);
				targetDeckUI.UpdateUI();
				targetDeckUI.gameObject.SetActive(true);
			}
		}

		public void SelectCard(CardData card)
		{
			if (CurPanelType != CardPanelType.SelectCard || card == null || offeredCards.Contains(card) == false)
			{
				return;
			}
			UiSfx.Play(UiSfxEvent.CardSelect);
			offeredCards.Clear();
			SetPanel(CardPanelType.None);
			CardBuffer selectedCardBuffer = soManager.SelectedCardBuffer;

			selectedCardBuffer.Add(card);
			if (card.AwakeningEffectPrefab != null)
			{
				awakeningEffect = Instantiate(card.AwakeningEffectPrefab);
				awakeningEffect.Play(playerProvider.Current.transform, timeManager);
			}

			int sameCardCount = selectedCardBuffer.Data.Where(m => m.ID == card.ID).Count();
			if (card.MaxStack == sameCardCount)
			{
				List<CardData> curDeckBuffer = cardDataBuffers[curDeckIndex];
				int cardIndex = curDeckBuffer.IndexOf(card);
				curDeckBuffer.RemoveAt(cardIndex);
			}

			levelUpStack--;
			ContinueAfterChoice();
		}

		private void ConvertLevelUps()
		{
			UnitObject player = playerProvider.CurrentObject;
			int heal = 0;
			int nyang = 0;
			for (; levelUpStack > 0; levelUpStack--)
			{
				if (player.UnitStat[UnitStatType.HP_CUR] + heal < player.UnitStat[UnitStatType.HP_MAX])
					heal += maxedLevelUpHeal;
				else
					nyang += maxedLevelUpNyang;
			}
			if (heal > 0)
				player.ReceiveHeal(heal);
			if (nyang > 0)
				dataManager.GameStat[GameStatType.NYANG] += nyang;
			EventBusBridge.Publish(new LevelUpConvertedEvent { Heal = heal, Nyang = nyang });
		}

		private float RarityWeight(CardData card) => card.Rarity switch
		{
			CardRarity.Rare => rareWeight * UpgradeData.LuckMultiplier,
			CardRarity.Epic => epicWeight * UpgradeData.LuckMultiplier,
			_ => commonWeight,
		};

		private int PickWeightedIndex(List<CardData> candidates)
		{
			float total = 0f;
			foreach (CardData card in candidates)
				total += RarityWeight(card);
			float roll = Random.value * total;
			for (int i = 0; i < candidates.Count; i++)
			{
				roll -= RarityWeight(candidates[i]);
				if (roll <= 0f)
					return i;
			}
			return candidates.Count - 1;
		}
	}
}
