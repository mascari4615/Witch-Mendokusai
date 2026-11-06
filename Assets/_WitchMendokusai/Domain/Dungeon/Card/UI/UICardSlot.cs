using System.Collections.Generic;

namespace WitchMendokusai
{
	public class UICardSlot : UISlot
	{
		private string choiceDescription;
		private readonly SlotData choiceTooltip = new();
		[UnityEngine.SerializeField] private UICardChoiceView choiceView;

		public UICardChoiceView ChoiceView => choiceView;
		public string ChoiceDetail { get; private set; }

		public void SetChoice(CardData card, EquipmentData equipment, IReadOnlyList<CardData> selectedCards, IReadOnlyList<CardData> deckCards)
		{
			choiceDescription = card?.Description;
			ChoiceDetail = CardChoiceText.Description(card, selectedCards, deckCards, true);
			choiceTooltip.SetData(card?.Sprite, card?.Name, ChoiceDetail);
			if (card != null)
				choiceView.Set(card, equipment, selectedCards, deckCards);
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
