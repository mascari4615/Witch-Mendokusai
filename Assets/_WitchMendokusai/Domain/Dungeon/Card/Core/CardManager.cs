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
		public override bool CanBeClosedByCancelInput => false;
		public override CardPanelType DefaultPanel => CardPanelType.None;

		private GameEventManager gameEventManager;
		private TimeManager timeManager;
		private SOManager soManager;
		private DataManager dataManager;
		private PlayerProvider playerProvider;
		private CardAwakeningEffect awakeningEffect;
		private ResourceManager resourceManager;
		private SkillTreeCatalog skillTrees;

		[Inject]
		public void Construct(GameEventManager gameEventManager, TimeManager timeManager, SOManager soManager, DataManager dataManager, UIManager uiManager, PlayerProvider playerProvider, ResourceManager resourceManager, SkillTreeCatalog skillTrees)
		{
			this.resourceManager = resourceManager;
			this.skillTrees = skillTrees;
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
		private readonly List<SkillTree> deckTrees = new();
		private readonly List<int> deckIdMapping = new();
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
			deckIdMapping.Clear();
			offeredCards.Clear();
			SetPanel(CardPanelType.None);
			if (timeManager != null)
			{
				timeManager.Resume(gameObject);
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
			List<EquipmentData> equipments = dataManager.GetEquipmentData(dataManager.CurDollID);
			while (cardDataBuffers.Count < equipments.Count)
			{
				cardDataBuffers.Add(new List<CardData>());
			}
			deckTrees.Clear();
			for (int i = 0; i < cardDataBuffers.Count; i++)
				deckTrees.Add(null);
			for (int i = 0; i < equipments.Count; i++)
			{
				if (equipments[i] == null)
					continue;
				cardDataBuffers[i].AddRange(skillTrees.CardsFor(equipments[i]));
				deckTrees[i] = skillTrees.Find(equipments[i].ID);
			}
		}

		private List<CardData> AvailableCards(int deckIndex)
		{
			SkillTree tree = deckTrees[deckIndex];
			return cardDataBuffers[deckIndex]
				.Where(card => card != null && card.MaxStack > 0 && bannedCardIds.Contains(card.ID) == false && card.MeetsRequirements(soManager.SelectedCardBuffer.Data) &&
					(tree == null || tree.IsUnlocked(card, soManager.SelectedCardBuffer.Data)) &&
					soManager.SelectedCardBuffer.Data.Count(selected => selected.ID == card.ID) < card.MaxStack)
				.GroupBy(card => card.ID)
				.Select(group => group.First())
				.ToList();
		}

		private bool ShuffleDeck()
		{
			List<EquipmentData> equipments = dataManager.GetEquipmentData(dataManager.CurDollID);
			deckIdMapping.Clear();
			for (int i = 0; i < equipments.Count; i++)
			{
				if (equipments[i] != null && AvailableCards(i).Count > 0)
				{
					deckIdMapping.Add(i);
				}
			}
			if (deckIdMapping.Count == 0)
			{
				return false;
			}
			for (int i = deckIdMapping.Count - 1; i > 0; i--)
			{
				int otherIndex = Random.Range(0, i + 1);
				(deckIdMapping[i], deckIdMapping[otherIndex]) = (deckIdMapping[otherIndex], deckIdMapping[i]);
			}
		
			List<EquipmentData> shuffledEquipments = new(deckIdMapping.Count);
			foreach (int index in deckIdMapping)
				shuffledEquipments.Add(equipments[index]);
		
			(Panels[CardPanelType.SelectDeck] as UISelectDeck).SetDeckSelectButtons(shuffledEquipments, soManager.SelectedCardBuffer.Data, skillTrees);
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
			if (CurPanelType != CardPanelType.SelectDeck || selectIndex < 0 || selectIndex >= deckIdMapping.Count)
			{
				return;
			}
			curDeckIndex = deckIdMapping[selectIndex];
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
			List<EquipmentData> choiceEquipments = dataManager.GetEquipmentData(dataManager.CurDollID);
			UISelectCard selectCardPanel = Panels[CardPanelType.SelectCard] as UISelectCard;
			selectCardPanel.SetCardSelectButtons(
				offeredCards, choiceEquipments[curDeckIndex], choiceEquipments, soManager.SelectedCardBuffer.Data, cardDataBuffers[curDeckIndex], skillTrees);

			SetPanel(CardPanelType.SelectCard);

			selectCardPanel.SetSkillTree(skillTrees.Find(choiceEquipments[curDeckIndex].ID), choiceEquipments[curDeckIndex], soManager.SelectedCardBuffer.Data, offeredCards);
		}

		public void SelectCard(CardData card)
		{
			if (CurPanelType != CardPanelType.SelectCard || card == null || offeredCards.Contains(card) == false)
			{
				return;
			}
			RuntimeManager.PlayOneShot("event:/SFX/UI/Click", transform.position);
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
