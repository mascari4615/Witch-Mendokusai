using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	public class PopupCard : VisualElement
	{
		public const string USS_CLASS = "wm-popup-card";
		public const string USS_ACTIVE = "wm-popup-card--active";
		public const string USS_VARIANT_STAGE = "wm-popup-card--stage";
		public const string USS_ICON = "wm-popup-card__icon";
		public const string USS_NAME = "wm-popup-card__name";
		public const string USS_DESC = "wm-popup-card__desc";

		private readonly VisualElement icon;
		private readonly Label nameLabel;
		private readonly Label descLabel;

		public PopupCard()
		{
			AddToClassList(USS_CLASS);
			pickingMode = PickingMode.Ignore;

			icon = new VisualElement();
			icon.AddToClassList(USS_ICON);
			icon.pickingMode = PickingMode.Ignore;
			Add(icon);

			nameLabel = new Label();
			nameLabel.AddToClassList(USS_NAME);
			nameLabel.pickingMode = PickingMode.Ignore;
			Add(nameLabel);

			descLabel = new Label();
			descLabel.AddToClassList(USS_DESC);
			descLabel.pickingMode = PickingMode.Ignore;
			Add(descLabel);
		}

		public void SetData(Sprite sprite, string name, string desc)
		{
			if (sprite != null)
				icon.style.backgroundImage = new StyleBackground(sprite);
			else
				icon.style.backgroundImage = StyleKeyword.None;

			nameLabel.text = name ?? string.Empty;
			descLabel.text = desc ?? string.Empty;
			descLabel.style.display = string.IsNullOrEmpty(desc) ? DisplayStyle.None : DisplayStyle.Flex;
		}

		public void SetActive(bool active)
		{
			if (active)
				AddToClassList(USS_ACTIVE);
			else
				RemoveFromClassList(USS_ACTIVE);
		}
	}
}
