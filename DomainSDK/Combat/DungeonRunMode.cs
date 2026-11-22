using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	public enum DungeonRunMode
	{
		Equipped = 0,
		Random = 1,
	}

	public readonly struct RunDeckOffer
	{
		private const int NEW_EQUIPMENT_DECK = -1;

		public readonly int DeckIndex;
		public readonly int NewEquipmentId;

		private RunDeckOffer(int deckIndex, int newEquipmentId)
		{
			DeckIndex = deckIndex;
			NewEquipmentId = newEquipmentId;
		}

		public bool IsNew => DeckIndex == NEW_EQUIPMENT_DECK;

		public static RunDeckOffer Owned(int deckIndex) => new RunDeckOffer(deckIndex, 0);
		public static RunDeckOffer NewEquipment(int equipmentId) => new RunDeckOffer(NEW_EQUIPMENT_DECK, equipmentId);
	}

	// 판 모드별 장비 규칙의 정본
	public static class DungeonRunModeRules
	{
		public const int NO_PINNED_DECK = -1;

		public static bool UsesSavedEquipment(DungeonRunMode mode) => mode == DungeonRunMode.Equipped;

		public static bool CanOfferNewEquipment(DungeonRunMode mode, int occupiedSlots, int slotCapacity)
			=> UsesSavedEquipment(mode) == false && occupiedSlots < slotCapacity;

		public static List<int> NewEquipmentCandidates(IEnumerable<int> eligibleIds, ICollection<int> excludedIds)
		{
			List<int> candidates = new List<int>();
			foreach (int id in eligibleIds)
			{
				if (excludedIds.Contains(id) == false && candidates.Contains(id) == false)
					candidates.Add(id);
			}
			return candidates;
		}

		// nextIndex: 0 이상 인자 미만의 난수
		// deckWeight: 보유 덱 번호별 제시 가중치 (DeckOfferWeights). 없으면 균등 섞기
		// pinnedDeck: 늘 후보에 드는 덱 (인물 전용 무기). 보유 덱에 있으면 보이는 칸 하나를 먼저 차지
		public static List<RunDeckOffer> BuildDeckOffers(
			DungeonRunMode mode,
			IReadOnlyList<int> ownedDecks,
			IReadOnlyList<int> newEquipmentIds,
			int occupiedSlots,
			int slotCapacity,
			int visibleCount,
			int newOfferCount,
			Func<int, int> nextIndex,
			Func<int, float> deckWeight = null,
			int pinnedDeck = NO_PINNED_DECK)
		{
			List<int> owned;
			if (deckWeight == null)
			{
				owned = new List<int>(ownedDecks);
				Shuffle(owned, nextIndex);
			}
			else
			{
				owned = DeckOfferWeights.WeightedOrder(ownedDecks, deckWeight, nextIndex);
			}
			if (owned.Remove(pinnedDeck))
				owned.Insert(0, pinnedDeck);
			List<RunDeckOffer> offers = new List<RunDeckOffer>();

			if (UsesSavedEquipment(mode))
			{
				foreach (int deckIndex in owned)
					offers.Add(RunDeckOffer.Owned(deckIndex));
				return offers;
			}

			List<int> fresh = CanOfferNewEquipment(mode, occupiedSlots, slotCapacity) ? new List<int>(newEquipmentIds) : new List<int>();
			Shuffle(fresh, nextIndex);

			int ownedCount = Math.Min(visibleCount - Math.Min(Math.Min(newOfferCount, fresh.Count), visibleCount), owned.Count);
			int newCount = Math.Min(visibleCount - ownedCount, fresh.Count);

			for (int i = 0; i < ownedCount; i++)
				offers.Add(RunDeckOffer.Owned(owned[i]));
			for (int i = 0; i < newCount; i++)
				offers.Add(RunDeckOffer.NewEquipment(fresh[i]));
			Shuffle(offers, nextIndex);
			return offers;
		}

		private static void Shuffle<T>(List<T> items, Func<int, int> nextIndex)
		{
			for (int i = items.Count - 1; i > 0; i--)
			{
				int otherIndex = nextIndex(i + 1);
				(items[i], items[otherIndex]) = (items[otherIndex], items[i]);
			}
		}
	}
}
