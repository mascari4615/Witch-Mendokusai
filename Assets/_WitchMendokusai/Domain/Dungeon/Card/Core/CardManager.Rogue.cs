using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FMODUnity;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	public readonly struct CardEvolutionProgress
	{
		public readonly CardData Card;
		public readonly int Current;
		public readonly int Required;
		public readonly bool IsOwned;
		public readonly bool IsReady;
		public readonly bool IsBlocked;

		public CardEvolutionProgress(CardData card, int current, int required, bool isOwned, bool isReady, bool isBlocked)
		{
			Card = card;
			Current = current;
			Required = required;
			IsOwned = isOwned;
			IsReady = isReady;
			IsBlocked = isBlocked;
		}
	}

	// 리롤, 스킵, 차단, 상자, 진화
	public partial class CardManager
	{
		private readonly HashSet<int> bannedCardIds = new();
		private Coroutine chestCloseRoutine;

		private SurvivalRunRules RunRules => resourceManager.SurvivalRunRules;

		public int RerollsLeft { get; private set; }
		public int SkipsLeft { get; private set; }
		public int BansLeft { get; private set; }
		public bool IsChestOpen => choiceQueue.IsChestOpen;
		public int PendingChestCount => choiceQueue.PendingChests;
		public int OfferedCardCount => offeredCards.Count;
		public IReadOnlyList<RunDeckOffer> DeckOffers => deckOffers;

		public bool CanReroll => CurPanelType == CardPanelType.SelectCard && RerollsLeft > 0 && AvailableCards(curDeckIndex).Count > offeredCards.Count;
		public bool CanSkip => (CurPanelType == CardPanelType.SelectDeck || CurPanelType == CardPanelType.SelectCard) && SkipsLeft > 0;
		public bool CanBan(CardData card) => CurPanelType == CardPanelType.SelectCard && BansLeft > 0 && card != null && offeredCards.Contains(card) && IsTreeRoot(card) == false;
		public string BanDeniedText(CardData card) => card != null && IsTreeRoot(card) ? RunRules.BanRootDeniedText : null;
		private bool IsTreeRoot(CardData card) => deckTrees.Any(tree => tree != null && tree.IsRoot(card));
		public bool IsCardBanned(CardData card) => card != null && bannedCardIds.Contains(card.ID);

		private void ResetRunChoices()
		{
			SurvivalRunRules rules = RunRules;
			RerollsLeft = RunChoiceRules.StartCharges(rules.RerollCharges, UpgradeData.BonusOf(UpgradeType.RerollCharge));
			SkipsLeft = RunChoiceRules.StartCharges(rules.SkipCharges, UpgradeData.BonusOf(UpgradeType.SkipCharge));
			BansLeft = RunChoiceRules.StartCharges(rules.BanCharges, UpgradeData.BonusOf(UpgradeType.BanCharge));
			bannedCardIds.Clear();
			choiceQueue.ResetRun();
			PublishCharges();
		}

		private void PublishCharges()
		{
			EventBusBridge.Publish(new CardChoiceChargesChangedEvent { Rerolls = RerollsLeft, Skips = SkipsLeft, Bans = BansLeft });
		}

		private void DrawOfferedCards(List<CardData> candidates, int targetCount)
		{
			while (offeredCards.Count < targetCount && candidates.Count > 0)
			{
				int randomIndex = PickWeightedIndex(candidates);
				offeredCards.Add(candidates[randomIndex]);
				candidates.RemoveAt(randomIndex);
			}
		}

		private void RefreshOfferedCards()
		{
			ShowOfferedCards();
			Panels[CardPanelType.SelectCard].UpdateUI();
		}

		// 직전 제시와 겹치지 않게 다시 뽑고, 모자라면 겹치는 카드로 채움
		public bool RerollCards()
		{
			if (CanReroll == false)
				return false;

			RerollsLeft--;
			List<CardData> previous = new(offeredCards);
			List<CardData> candidates = AvailableCards(curDeckIndex);
			int choiceCount = Math.Min(Math.Max(cardsPerChoice, 1), candidates.Count);
			offeredCards.Clear();
			DrawOfferedCards(candidates.Where(card => previous.Contains(card) == false).ToList(), choiceCount);
			DrawOfferedCards(candidates.Where(card => offeredCards.Contains(card) == false).ToList(), choiceCount);

			PublishCharges();
			RefreshOfferedCards();
			return true;
		}

		// 이번 판 후보에서 제외. 빈 칸은 같은 덱에서 바로 채움
		public bool BanCard(CardData card)
		{
			if (CanBan(card) == false)
				return false;

			BansLeft--;
			bannedCardIds.Add(card.ID);
			int slotIndex = offeredCards.IndexOf(card);
			offeredCards.RemoveAt(slotIndex);
			List<CardData> candidates = AvailableCards(curDeckIndex).Where(candidate => offeredCards.Contains(candidate) == false).ToList();
			if (candidates.Count > 0)
				offeredCards.Insert(slotIndex, candidates[PickWeightedIndex(candidates)]);

			PublishCharges();
			EventBusBridge.Publish(new CardBannedEvent { Card = card });

			if (offeredCards.Count == 0)
			{
				SetPanel(CardPanelType.None);
				StartCoroutine(StartSelectCard());
				return true;
			}
			RefreshOfferedCards();
			return true;
		}

		// 레벨업 하나를 소비. 경험치 환급은 다음 레벨업을 낼 수 있어 흐름 정리 뒤에 지급
		public bool SkipLevelUp()
		{
			if (CanSkip == false)
				return false;

			SkipsLeft--;
			offeredCards.Clear();
			SetPanel(CardPanelType.None);
			choiceQueue.ConsumeLevelUp();
			PublishCharges();

			UnitStat playerStat = playerProvider.Current.UnitStat;
			int refundExp = Mathf.RoundToInt(playerStat[UnitStatType.EXP_MAX] * RunRules.SkipExpRefundRatio);
			ContinueAfterChoice();
			if (refundExp > 0)
				playerStat[UnitStatType.EXP_CUR] += refundExp;
			EventBusBridge.Publish(new LevelUpSkippedEvent { RefundExp = refundExp });
			return true;
		}

		// 새 장비 선택이 레벨업 하나를 소비
		private void AcquireEquipment(EquipmentData equipment)
		{
			UiSfx.Play(UiSfxEvent.CardSelect);
			deckOffers.Clear();
			SetPanel(CardPanelType.None);

			int deckIndex = Loadout.Acquire(equipment);
			while (cardDataBuffers.Count <= deckIndex)
				cardDataBuffers.Add(new List<CardData>());
			while (deckTrees.Count <= deckIndex)
				deckTrees.Add(null);
			cardDataBuffers[deckIndex].AddRange(skillTrees.CardsFor(equipment));
			deckTrees[deckIndex] = skillTrees.Find(equipment.ID);
			gameManager.EquipDuringRun(equipment);

			choiceQueue.ConsumeLevelUp();
			ContinueAfterChoice();
		}

		private void ContinueAfterChoice()
		{
			if (choiceQueue.PendingLevelUps > 0)
			{
				StartCoroutine(StartSelectCard());
				return;
			}
			timeManager.Resume(gameObject);
			playerProvider.CurrentObject.GrantInvincibility(postSelectionInvincibility);
			TryOpenNextChest();
		}

		private void HandleChestPickedUp(ChestPickedUpEvent chestEvent)
		{
			if (DungeonManagerBridge.IsDungeon == false)
				return;
			choiceQueue.AddChest();
			TryOpenNextChest();
		}

		// 카드 선택과 겹치면 대기, 선택이 끝난 뒤 개봉
		private void TryOpenNextChest()
		{
			if (choiceQueue.TryOpenChest() == false)
				return;

			timeManager.Pause(gameObject);
			SurvivalFeel.Instance.OnChestOpened(playerProvider.Current.transform.position);
			float autoCloseSeconds = RunRules.ChestAutoCloseSeconds;
			EventBusBridge.Publish(new ChestOpenedEvent { Rewards = RollChestRewards(), AutoCloseSeconds = autoCloseSeconds });
			if (autoCloseSeconds > 0f)
				chestCloseRoutine = StartCoroutine(CloseChestAfterDelay(autoCloseSeconds));
		}

		private IEnumerator CloseChestAfterDelay(float seconds)
		{
			yield return new WaitForSecondsRealtime(seconds);
			chestCloseRoutine = null;
			CloseChest();
		}

		public void CloseChest()
		{
			if (choiceQueue.IsChestOpen == false)
				return;
			if (chestCloseRoutine != null)
				StopCoroutine(chestCloseRoutine);
			ClearOpenChest();
			ContinueAfterChoice();
		}

		// OnDisable 경유면 메시지 통로가 이미 닫혔을 수 있어 발행 생략
		private void ClearOpenChest()
		{
			chestCloseRoutine = null;
			if (choiceQueue.CloseChest() == false)
				return;
			if (isActiveAndEnabled)
				EventBusBridge.Publish(new ChestClosedEvent());
		}

		private List<ChestReward> RollChestRewards()
		{
			SurvivalRunRules rules = RunRules;
			List<ChestReward> rewards = new();
			int rewardCount = PickChestRewardCount(rules);

			CardData evolution = FindReadyEvolution(rules);
			if (evolution != null)
			{
				GrantChestCard(evolution);
				rewards.Add(new ChestReward { Card = evolution, IsEvolution = true });
			}

			while (rewards.Count < rewardCount)
			{
				List<CardData> pool = ChestCardPool();
				if (pool.Count == 0)
				{
					dataManager.GameStat[GameStatType.NYANG] += rules.ChestFallbackNyang;
					rewards.Add(new ChestReward { Nyang = rules.ChestFallbackNyang });
					continue;
				}
				CardData card = pool[PickWeightedIndex(pool)];
				GrantChestCard(card);
				rewards.Add(new ChestReward { Card = card });
			}
			return rewards;
		}

		private static int PickChestRewardCount(SurvivalRunRules rules)
		{
			List<ChestRewardCountWeight> entries = rules.ChestRewardCounts;
			float total = 0f;
			foreach (ChestRewardCountWeight entry in entries)
				total += entry.Weight;
			float roll = Random.value * total;
			foreach (ChestRewardCountWeight entry in entries)
			{
				roll -= entry.Weight;
				if (roll <= 0f)
					return entry.Count;
			}
			return entries[^1].Count;
		}

		private List<CardData> ChestCardPool()
		{
			List<CardData> pool = new();
			for (int i = 0; i < cardDataBuffers.Count; i++)
				pool.AddRange(AvailableCards(i));
			return pool.GroupBy(card => card.ID).Select(group => group.First()).ToList();
		}

		private void GrantChestCard(CardData card)
		{
			soManager.SelectedCardBuffer.Add(card);
			if (card.AwakeningEffectPrefab != null && awakeningEffect == null)
			{
				awakeningEffect = Instantiate(card.AwakeningEffectPrefab);
				awakeningEffect.Play(playerProvider.Current.transform, timeManager);
			}
		}

		private CardData FindReadyEvolution(SurvivalRunRules rules)
		{
			IReadOnlyList<CardData> selectedCards = soManager.SelectedCardBuffer.Data;
			foreach (CardData card in rules.EvolutionCards)
				if (CountSelected(card) < card.MaxStack && card.MeetsRequirements(selectedCards) && IsTreeUnlocked(card))
					return card;
			return null;
		}

		// 장착 장비 트리 전부에서 해금돼야 함
		private bool IsTreeUnlocked(CardData card)
		{
			foreach (SkillTree tree in deckTrees)
				if (tree != null && tree.IsUnlocked(card, soManager.SelectedCardBuffer.Data) == false)
					return false;
			return true;
		}

		private int CountSelected(CardData card) => soManager.SelectedCardBuffer.Data.Count(selected => selected.ID == card.ID);

		// 지금 장비로 조건을 채울 수 있는 진화만 포함
		public List<CardEvolutionProgress> GetEvolutionProgress()
		{
			HashSet<int> equippedCardIds = new();
			foreach (EquipmentData equipment in Loadout.Equipments())
			{
				if (equipment == null)
					continue;
				foreach (CardData card in skillTrees.CardsFor(equipment))
					if (card != null)
						equippedCardIds.Add(card.ID);
			}

			List<CardEvolutionProgress> progressList = new();
			IReadOnlyList<CardData> selectedCards = soManager.SelectedCardBuffer.Data;
			foreach (CardData evolution in RunRules.EvolutionCards)
			{
				if (evolution.Requirements.All(requirement => equippedCardIds.Contains(requirement.Card.ID)) == false)
					continue;

				int current = 0;
				int required = 0;
				bool isBlocked = false;
				foreach (CardData.Requirement requirement in evolution.Requirements)
				{
					int count = CountSelected(requirement.Card);
					current += Mathf.Min(count, requirement.Stack);
					required += requirement.Stack;
					isBlocked |= count < requirement.Stack && IsCardBanned(requirement.Card);
				}
				bool isOwned = CountSelected(evolution) >= evolution.MaxStack;
				progressList.Add(new CardEvolutionProgress(evolution, current, required, isOwned, isOwned == false && evolution.MeetsRequirements(selectedCards), isBlocked));
			}
			return progressList;
		}
	}
}
