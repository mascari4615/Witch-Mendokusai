using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	// 장비 하나의 작은 카드 칸에 보일 카드. 레벨업 덱 칸 (UIDeckChoiceView) 과 카드 고르기 덱 탭 (UIDeckTabs) 이 같이 씀
	// 트리 카드가 상한 이하면 전부 (지금까지와 같음). 넘으면 받은 카드와 지금 열린 카드만, 받은 것 먼저 (알리사 반짇고리 46장이 칸 밖으로 넘친 것, 2026-11-23)
	public static class DeckMiniCards
	{
		public static List<CardData> Pick(IReadOnlyList<CardData> treeCards, IReadOnlyList<CardData> selectedCards, SkillTree tree, int maxCount)
		{
			List<CardData> cards = treeCards.Where(card => card != null).GroupBy(card => card.ID).Select(group => group.First()).ToList();
			if (cards.Count <= maxCount || tree == null)
				return cards;
			return cards.Where(card => IsOwned(card, selectedCards) || tree.IsUnlocked(card, selectedCards))
				.OrderBy(card => IsOwned(card, selectedCards) ? 0 : 1)
				.Take(maxCount)
				.ToList();
		}

		private static bool IsOwned(CardData card, IReadOnlyList<CardData> selectedCards) => selectedCards.Any(selected => selected.ID == card.ID);
	}
}
