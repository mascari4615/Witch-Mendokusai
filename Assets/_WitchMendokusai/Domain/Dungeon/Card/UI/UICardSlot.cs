using System.Collections.Generic;

namespace WitchMendokusai
{
	public class UICardSlot : UISlot
	{
		private string choiceDescription;
		private readonly SlotData choiceTooltip = new();

		public void SetChoice(CardData card, IReadOnlyList<CardData> selectedCards, IReadOnlyList<CardData> deckCards)
		{
			choiceDescription = card == null ? null : CardChoiceText.Description(card, selectedCards, deckCards);
			choiceTooltip.SetData(card?.Sprite, card?.Name, CardChoiceText.Description(card, selectedCards, deckCards, true));
			SetSlot(card);
		}

		public override void UpdateUI()
		{
			base.UpdateUI();
			if (choiceDescription == null)
				return;
			if (descriptionText != null)
				descriptionText.text = choiceDescription;
			if (ToolTipTrigger != null)
				ToolTipTrigger.SetToolTipContent(choiceTooltip);
		}
	}
}
