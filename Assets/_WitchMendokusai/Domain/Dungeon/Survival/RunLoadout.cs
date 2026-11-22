using System;
using System.Collections.Generic;
using System.Linq;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	// 이번 판의 장비 출처. 모드별 차이는 여기와 DungeonRunModeRules 에만
	public class RunLoadout
	{
		public const int SIGNATURE_DECK = 0;

		private readonly DataManager dataManager;
		private readonly List<EquipmentData> acquired = new();

		public RunLoadout(DataManager dataManager) => this.dataManager = dataManager;

		public DungeonRunMode Mode { get; private set; } = DungeonRunMode.Equipped;
		public IReadOnlyList<EquipmentData> Acquired => acquired;
		public bool UsesRunSlots => DungeonRunModeRules.UsesSavedEquipment(Mode) == false;
		public EquipmentData Signature => GetDoll(dataManager.CurDollID).SignatureEquipment;

		public void Begin(DungeonRunMode mode, SurvivalRunRules rules)
		{
			Mode = mode;
			acquired.Clear();
			if (UsesRunSlots)
				acquired.Add(rules.RandomModeStartEquipment);
		}

		public void End()
		{
			Mode = DungeonRunMode.Equipped;
			acquired.Clear();
		}

		// 인덱스는 덱 번호. 판 중 장착은 뒤에만 붙어 기존 번호 유지
		public List<EquipmentData> Equipments()
		{
			if (UsesRunSlots == false)
				return dataManager.GetEquipmentData(dataManager.CurDollID);

			List<EquipmentData> equipments = new() { Signature };
			foreach (EquipmentData equipment in acquired)
				equipments.Add(equipments.Contains(equipment) ? null : equipment);
			return equipments;
		}

		// pickCountOf: 덱 번호별 이번 판에 받은 카드 수. 가중치는 rules.DeckWeightByPicks
		public List<RunDeckOffer> BuildDeckOffers(SurvivalRunRules rules, IReadOnlyList<int> ownedDecks, int visibleCount, Func<int, int> nextIndex, Func<int, int> pickCountOf)
		{
			bool canOffer = DungeonRunModeRules.CanOfferNewEquipment(Mode, acquired.Count, rules.RandomModeSlotCount);
			IReadOnlyList<int> newEquipmentIds = canOffer ? NewEquipmentIds() : Array.Empty<int>();
			Func<int, float> deckWeight = deckIndex => DeckOfferWeights.Weight(rules.DeckWeightByPicks, pickCountOf(deckIndex));
			// 전용 무기는 두 모드 모두 0 번 덱. 늘 후보에 듦
			int pinnedDeck = Signature != null ? SIGNATURE_DECK : DungeonRunModeRules.NO_PINNED_DECK;
			return DungeonRunModeRules.BuildDeckOffers(Mode, ownedDecks, newEquipmentIds, acquired.Count, rules.RandomModeSlotCount, visibleCount, rules.RandomModeNewOfferCount, nextIndex, deckWeight, pinnedDeck);
		}

		public int Acquire(EquipmentData equipment)
		{
			acquired.Add(equipment);
			return Equipments().Count - 1;
		}

		public static EquipmentData GetEquipment(int id) => (EquipmentData)GetItemData(id);

		// 이번 판에 그 장비 트리에서 받은 카드 수 (겹친 장 포함, 상자 보상 포함). 진화 카드는 트리 밖이라 안 셈
		// ID 로 비교. 트리 카드는 다시 만들어질 수 있어 참조 비교는 0 으로 셀 수 있음
		public static int PickCount(SkillTreeCatalog skillTrees, EquipmentData equipment, IReadOnlyList<CardData> selectedCards)
		{
			HashSet<int> treeCardIds = new(skillTrees.CardsFor(equipment).Where(card => card != null).Select(card => card.ID));
			return selectedCards.Count(selected => selected != null && treeCardIds.Contains(selected.ID));
		}

		// 현재 인형 고유 장비와 채집 도구 제외, 강화 카드가 있는 장비만
		private List<int> NewEquipmentIds()
		{
			List<int> eligibleIds = new();
			ForEach<ItemData>(itemData =>
			{
				if (itemData is EquipmentData equipment && equipment.IsRunOfferType && equipment.EffectCards.Any(card => card != null))
					eligibleIds.Add(equipment.ID);
			});

			List<int> excludedIds = acquired.Select(equipment => equipment.ID).ToList();
			EquipmentData signature = Signature;
			if (signature != null)
				excludedIds.Add(signature.ID);
			return DungeonRunModeRules.NewEquipmentCandidates(eligibleIds, excludedIds);
		}
	}
}
