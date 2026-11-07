using System;
using System.Collections.Generic;
using System.Linq;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	// 이번 판의 장비 출처. 모드별 차이는 여기와 DungeonRunModeRules 에만
	public class RunLoadout
	{
		private readonly DataManager dataManager;
		private readonly List<EquipmentData> acquired = new();

		public RunLoadout(DataManager dataManager) => this.dataManager = dataManager;

		public DungeonRunMode Mode { get; private set; } = DungeonRunMode.Equipped;
		public IReadOnlyList<EquipmentData> Acquired => acquired;
		public bool UsesRunSlots => DungeonRunModeRules.UsesSavedEquipment(Mode) == false;

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

			List<EquipmentData> equipments = new() { GetDoll(dataManager.CurDollID).SignatureEquipment };
			foreach (EquipmentData equipment in acquired)
				equipments.Add(equipments.Contains(equipment) ? null : equipment);
			return equipments;
		}

		public List<RunDeckOffer> BuildDeckOffers(SurvivalRunRules rules, IReadOnlyList<int> ownedDecks, int visibleCount, Func<int, int> nextIndex)
		{
			bool canOffer = DungeonRunModeRules.CanOfferNewEquipment(Mode, acquired.Count, rules.RandomModeSlotCount);
			IReadOnlyList<int> newEquipmentIds = canOffer ? NewEquipmentIds() : Array.Empty<int>();
			return DungeonRunModeRules.BuildDeckOffers(Mode, ownedDecks, newEquipmentIds, acquired.Count, rules.RandomModeSlotCount, visibleCount, rules.RandomModeNewOfferCount, nextIndex);
		}

		public int Acquire(EquipmentData equipment)
		{
			acquired.Add(equipment);
			return Equipments().Count - 1;
		}

		public static EquipmentData GetEquipment(int id) => (EquipmentData)GetItemData(id);

		// 인형 고유 장비와 채집 도구 제외, 강화 카드가 있는 장비만
		private List<int> NewEquipmentIds()
		{
			List<int> eligibleIds = new();
			ForEach<ItemData>(itemData =>
			{
				if (itemData is EquipmentData equipment && equipment.EquipmentType == EquipmentType.Default && equipment.EffectCards.Any(card => card != null))
					eligibleIds.Add(equipment.ID);
			});

			List<int> excludedIds = acquired.Select(equipment => equipment.ID).ToList();
			ForEach<Doll>(doll =>
			{
				if (doll.SignatureEquipment != null)
					excludedIds.Add(doll.SignatureEquipment.ID);
			});
			return DungeonRunModeRules.NewEquipmentCandidates(eligibleIds, excludedIds);
		}
	}
}
