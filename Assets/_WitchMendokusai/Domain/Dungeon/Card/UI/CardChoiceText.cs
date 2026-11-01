using System;
using System.Collections.Generic;
using System.Text;

namespace WitchMendokusai
{
	public static class CardChoiceText
	{
		public static string Description(CardData card, IReadOnlyList<CardData> selectedCards,
			IReadOnlyList<CardData> deckCards, bool includeRequirements = false)
		{
			if (card == null)
				return string.Empty;

			int current = Count(card, selectedCards);
			StringBuilder text = new(card.Description);
			text.Append($"\n강화 {current} → {Math.Min(current + 1, card.MaxStack)} / {card.MaxStack}회");
			if (deckCards == null)
				return text.ToString();

			foreach (CardData advanced in deckCards)
			{
				if (advanced == null || advanced.Requirements.Count == 0 || Count(advanced, selectedCards) >= advanced.MaxStack)
					continue;
				bool related = false;
				bool valid = true;
				int before = 0;
				int after = 0;
				int required = 0;
				foreach (CardData.Requirement requirement in advanced.Requirements)
				{
					if (requirement.Card == null || requirement.Stack < 1)
					{
						valid = false;
						break;
					}
					int count = Count(requirement.Card, selectedCards);
					bool matches = requirement.Card.ID == card.ID;
					related |= matches;
					before += Math.Min(count, requirement.Stack);
					after += Math.Min(count + (matches ? 1 : 0), requirement.Stack);
					required += requirement.Stack;
				}
				if (valid == false || related == false)
					continue;

				text.Append($"\n{advanced.Name} 조건 {before} → {after} / {required}회");
				if (includeRequirements)
				{
					foreach (CardData.Requirement requirement in advanced.Requirements)
						text.Append($"\n{requirement.Card.Name}: 현재 {Math.Min(Count(requirement.Card, selectedCards), requirement.Stack)}/{requirement.Stack}회");
					text.Append("\n조건을 모두 채우면 다음 카드 추첨부터 후보에 포함됩니다.");
				}
				break;
			}
			return text.ToString();
		}

		private static int Count(CardData card, IReadOnlyList<CardData> selectedCards)
		{
			int count = 0;
			if (selectedCards != null)
				foreach (CardData selected in selectedCards)
					if (selected != null && selected.ID == card.ID)
						count++;
			return count;
		}
	}
}
