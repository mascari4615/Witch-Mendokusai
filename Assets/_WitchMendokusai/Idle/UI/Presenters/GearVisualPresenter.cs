using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai.Idle.UI
{
	public sealed class GearVisualPresenter
	{
		private readonly GearPresentationSO presentation;

		public GearVisualPresenter(GearPresentationSO presentation)
		{
			this.presentation = presentation;
		}

		public void SetSprite(VisualElement element, int slot, int tier)
		{
			Sprite sprite = presentation.SpriteOf(slot, tier);
			element.style.backgroundImage = sprite != null
				? new StyleBackground(sprite)
				: StyleKeyword.None;
		}

		public void SetTierOutline(VisualElement element, int tier)
		{
			Color color = presentation.ColorOf(tier);
			float width = tier > 0 ? presentation.TierBorderWidth : 0f;
			element.style.borderTopColor = color;
			element.style.borderRightColor = color;
			element.style.borderBottomColor = color;
			element.style.borderLeftColor = color;
			element.style.borderTopWidth = width;
			element.style.borderRightWidth = width;
			element.style.borderBottomWidth = width;
			element.style.borderLeftWidth = width;
		}
	}
}
