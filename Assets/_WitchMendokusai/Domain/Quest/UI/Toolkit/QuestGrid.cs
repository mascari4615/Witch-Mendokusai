using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	public class QuestGrid : VisualElement
	{
		public const string USS_CLASS = "wm-quest-grid";
		public const string USS_LIST = "wm-quest-grid__list";

		public event Action<RuntimeQuest> OnQuestSelected = delegate { };

		private QuestBuffer buffer;
		private QuestType filter = QuestType.None;
		private QuestEntry selectedEntry;
		private readonly List<QuestEntry> entries = new();
		private QuestManager questManager;
		private readonly ScrollView scrollView;

		public QuestGrid()
		{
			AddToClassList(USS_CLASS);

			QuestFilterBar filterBar = new();
			filterBar.OnFilterChanged += OnFilterChanged;
			Add(filterBar);

			scrollView = new ScrollView();
			scrollView.AddToClassList(USS_LIST);
			Add(scrollView);
		}

		public void Bind(QuestBuffer questBuffer, QuestManager questManager)
		{
			this.questManager = questManager;
			if (buffer != null)
				buffer.OnDataChanged -= Refresh;

			buffer = questBuffer;

			if (buffer == null)
				return;

			buffer.OnDataChanged += Refresh;
			Refresh();
		}

		public void Unbind()
		{
			if (buffer != null)
				buffer.OnDataChanged -= Refresh;
			buffer = null;
		}

		private void OnFilterChanged(QuestType newFilter)
		{
			filter = newFilter;
			Refresh();
		}

		public void Refresh()
		{
			if (buffer == null)
				return;

			List<RuntimeQuest> quests = buffer.Data;

			while (entries.Count > quests.Count)
			{
				QuestEntry last = entries[^1];
				scrollView.Remove(last);
				entries.RemoveAt(entries.Count - 1);
			}

			while (entries.Count < quests.Count)
			{
				QuestEntry entry = new(questManager);
				entry.RegisterCallback<PointerDownEvent>(_ => Select(entry));
				scrollView.Add(entry);
				entries.Add(entry);
			}

			for (int i = 0; i < quests.Count; i++)
			{
				entries[i].Bind(quests[i]);
				bool show = filter == QuestType.None || quests[i].Type == filter;
				entries[i].style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
			}

			if (selectedEntry != null && selectedEntry.Quest != null && quests.Contains(selectedEntry.Quest) == false)
				Select(entries.Count > 0 ? entries[0] : null);
		}

		private void Select(QuestEntry entry)
		{
			if (selectedEntry != null)
				selectedEntry.SetSelected(false);

			selectedEntry = entry;

			if (selectedEntry != null)
				selectedEntry.SetSelected(true);

			OnQuestSelected.Invoke(entry?.Quest);
		}
	}
}
