using System;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	public class QuestFilterBar : VisualElement
	{
		public const string USS_CLASS = "wm-quest-filter-bar";
		public const string BUTTON_CLASS = "wm-quest-filter-bar__button";
		public const string BUTTON_ACTIVE_CLASS = "wm-quest-filter-bar__button--active";

		public event Action<QuestType> OnFilterChanged = delegate { };

		private QuestType current = QuestType.None;

		public QuestFilterBar()
		{
			AddToClassList(USS_CLASS);

			AddButton("전체", QuestType.None);
			AddButton("일반", QuestType.Normal);
			AddButton("마을의뢰", QuestType.VillageRequest);
			AddButton("업적", QuestType.Achievement);
			AddButton("연구", QuestType.Research);
			AddButton("던전", QuestType.Dungeon);

			RefreshActive();
		}

		private void AddButton(string label, QuestType type)
		{
			Button button = new(() => Select(type))
			{
				text = label,
				userData = type
			};
			button.AddToClassList(BUTTON_CLASS);
			Add(button);
		}

		private void Select(QuestType type)
		{
			if (current == type)
				return;
			current = type;
			RefreshActive();
			OnFilterChanged.Invoke(type);
		}

		private void RefreshActive()
		{
			foreach (VisualElement child in Children())
			{
				if (child is Button button && button.userData is QuestType type)
				{
					if (type == current)
						button.AddToClassList(BUTTON_ACTIVE_CLASS);
					else
						button.RemoveFromClassList(BUTTON_ACTIVE_CLASS);
				}
			}
		}
	}
}
