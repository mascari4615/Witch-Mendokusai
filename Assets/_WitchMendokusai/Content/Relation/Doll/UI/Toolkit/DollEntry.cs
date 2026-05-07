using UnityEngine.UIElements;

namespace WitchMendokusai
{
	public class DollEntry : VisualElement
	{
		public const string USS_CLASS = "wm-doll-entry";
		public const string USS_SELECTED = "wm-doll-entry--selected";
		public const string USS_ICON = "wm-doll-entry__icon";
		public const string USS_NAME = "wm-doll-entry__name";
		public const string USS_LEVEL = "wm-doll-entry__level";

		private readonly VisualElement icon;
		private readonly Label nameLabel;
		private readonly Label levelLabel;

		public Doll Doll { get; private set; }

		public DollEntry()
		{
			AddToClassList(USS_CLASS);
			focusable = true;
			pickingMode = PickingMode.Position;

			icon = new VisualElement();
			icon.AddToClassList(USS_ICON);
			icon.pickingMode = PickingMode.Ignore;
			Add(icon);

			VisualElement textColumn = new();
			textColumn.style.flexDirection = FlexDirection.Column;
			textColumn.style.flexGrow = 1;
			textColumn.pickingMode = PickingMode.Ignore;
			Add(textColumn);

			nameLabel = new Label();
			nameLabel.AddToClassList(USS_NAME);
			nameLabel.pickingMode = PickingMode.Ignore;
			textColumn.Add(nameLabel);

			levelLabel = new Label();
			levelLabel.AddToClassList(USS_LEVEL);
			levelLabel.pickingMode = PickingMode.Ignore;
			textColumn.Add(levelLabel);
		}

		public void Bind(Doll doll)
		{
			Doll = doll;
			Refresh();
		}

		public void Refresh()
		{
			if (Doll == null)
				return;

			if (Doll.Sprite != null)
				icon.style.backgroundImage = new StyleBackground(Doll.Sprite);
			else
				icon.style.backgroundImage = StyleKeyword.None;

			nameLabel.text = Doll.Name ?? "?";
			levelLabel.text = $"Lv.{Doll.Level}";
		}

		public void SetSelected(bool selected)
		{
			if (selected)
				AddToClassList(USS_SELECTED);
			else
				RemoveFromClassList(USS_SELECTED);
		}
	}
}
