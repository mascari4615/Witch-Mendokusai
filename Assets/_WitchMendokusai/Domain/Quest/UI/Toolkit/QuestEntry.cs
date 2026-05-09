using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	public class QuestEntry : VisualElement
	{
		public const string USS_CLASS = "wm-quest-entry";
		public const string USS_SELECTED = "wm-quest-entry--selected";
		public const string USS_NAME = "wm-quest-entry__name";
		public const string USS_TYPE = "wm-quest-entry__type";
		public const string USS_STATE = "wm-quest-entry__state";
		public const string USS_PROGRESS_BG = "wm-quest-entry__progress";
		public const string USS_PROGRESS_FILL = "wm-quest-entry__progress-fill";

		private readonly Label nameLabel;
		private readonly Label typeLabel;
		private readonly Label stateLabel;
		private readonly VisualElement progressFill;

		public RuntimeQuest Quest { get; private set; }

		public QuestEntry()
		{
			AddToClassList(USS_CLASS);
			focusable = true;
			pickingMode = PickingMode.Position;

			nameLabel = new Label();
			nameLabel.AddToClassList(USS_NAME);
			nameLabel.pickingMode = PickingMode.Ignore;
			Add(nameLabel);

			VisualElement footer = new();
			footer.style.flexDirection = FlexDirection.Row;
			footer.pickingMode = PickingMode.Ignore;
			Add(footer);

			typeLabel = new Label();
			typeLabel.AddToClassList(USS_TYPE);
			typeLabel.pickingMode = PickingMode.Ignore;
			footer.Add(typeLabel);

			stateLabel = new Label();
			stateLabel.AddToClassList(USS_STATE);
			stateLabel.pickingMode = PickingMode.Ignore;
			footer.Add(stateLabel);

			VisualElement progressBg = new();
			progressBg.AddToClassList(USS_PROGRESS_BG);
			progressBg.pickingMode = PickingMode.Ignore;
			Add(progressBg);

			progressFill = new VisualElement();
			progressFill.AddToClassList(USS_PROGRESS_FILL);
			progressFill.pickingMode = PickingMode.Ignore;
			progressBg.Add(progressFill);
		}

		public void Bind(RuntimeQuest quest)
		{
			Quest = quest;
			Refresh();
		}

		public void Refresh()
		{
			if (Quest == null)
				return;

			nameLabel.text = Quest.Name ?? Quest.SO?.Name ?? "?";
			typeLabel.text = Quest.Type.ToString();
			stateLabel.text = Quest.State.ToString();
			progressFill.style.width = new StyleLength(new Length(Quest.GetProgress() * 100f, LengthUnit.Percent));
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
