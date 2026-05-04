using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 도감 윈도우 본체 레이아웃. 좌 사이드바(카테고리) + 중 엔트리 list + 우 디테일.
	/// WMWindow 를 직접 상속 X — 컴포지션 (CodexWindowController 가 WMWindow 보유, view 는 Content 에 attach).
	/// DevWindowView 와 같은 모양.
	/// </summary>
	public class CodexView : VisualElement
	{
		public const string USS_CLASS = "wm-codex";
		public const string USS_SIDEBAR = "wm-codex__sidebar";
		public const string USS_SIDEBAR_BUTTON = "wm-codex__sidebar-button";
		public const string USS_SIDEBAR_BUTTON_ACTIVE = "wm-codex__sidebar-button--active";
		public const string USS_ENTRIES = "wm-codex__entries";
		public const string USS_ENTRY_BUTTON = "wm-codex__entry-button";
		public const string USS_ENTRY_BUTTON_ACTIVE = "wm-codex__entry-button--active";
		public const string USS_ENTRY_BUTTON_LOCKED = "wm-codex__entry-button--locked";
		public const string USS_DETAIL = "wm-codex__detail";

		public event Action<ICodexCategory> OnCategorySelected = delegate { };
		public event Action<CodexEntry> OnEntrySelected = delegate { };

		private readonly VisualElement sidebar;
		private readonly ScrollView entriesScroll;
		private readonly VisualElement detailArea;
		private readonly Dictionary<string, Button> sidebarButtons = new();
		private readonly Dictionary<string, Button> entryButtons = new();

		private ICodexCategory activeCategory;
		private CodexEntry activeEntry;

		public ICodexCategory ActiveCategory => activeCategory;
		public CodexEntry ActiveEntry => activeEntry;

		public CodexView()
		{
			AddToClassList(USS_CLASS);
			style.flexDirection = FlexDirection.Row;
			style.flexGrow = 1;

			sidebar = new VisualElement();
			sidebar.AddToClassList(USS_SIDEBAR);
			sidebar.style.width = 150;
			Add(sidebar);

			entriesScroll = new ScrollView(ScrollViewMode.Vertical);
			entriesScroll.AddToClassList(USS_ENTRIES);
			entriesScroll.style.flexGrow = 1;
			Add(entriesScroll);

			detailArea = new VisualElement();
			detailArea.AddToClassList(USS_DETAIL);
			detailArea.style.width = 280;
			Add(detailArea);
		}

		public void RebuildSidebar(IReadOnlyList<ICodexCategory> categories)
		{
			sidebar.Clear();
			sidebarButtons.Clear();

			for (int i = 0; i < categories.Count; i++)
			{
				ICodexCategory category = categories[i];
				ICodexCategory captured = category;
				Button button = new(() => OnCategorySelected.Invoke(captured))
				{
					text = category.DisplayName,
				};
				button.AddToClassList(USS_SIDEBAR_BUTTON);
				sidebarButtons[category.Id] = button;
				sidebar.Add(button);
			}
		}

		public void SetActiveCategory(ICodexCategory category)
		{
			if (activeCategory != null)
			{
				activeCategory.OnDeactivate();
				if (sidebarButtons.TryGetValue(activeCategory.Id, out Button previousButton))
					previousButton.RemoveFromClassList(USS_SIDEBAR_BUTTON_ACTIVE);
			}

			entriesScroll.Clear();
			entryButtons.Clear();
			detailArea.Clear();
			activeEntry = null;

			activeCategory = category;
			if (category == null)
				return;

			category.OnActivate();
			if (sidebarButtons.TryGetValue(category.Id, out Button currentButton))
				currentButton.AddToClassList(USS_SIDEBAR_BUTTON_ACTIVE);

			IReadOnlyList<CodexEntry> entries = category.GetEntries();
			for (int i = 0; i < entries.Count; i++)
			{
				CodexEntry entry = entries[i];
				CodexEntry captured = entry;
				string label = entry.IsUnlocked ? entry.DisplayName : "???";
				Button button = new(() => OnEntrySelected.Invoke(captured))
				{
					text = label,
				};
				button.AddToClassList(USS_ENTRY_BUTTON);
				if (entry.IsUnlocked == false)
					button.AddToClassList(USS_ENTRY_BUTTON_LOCKED);
				entryButtons[entry.Id] = button;
				entriesScroll.Add(button);
			}
		}

		public void SetActiveEntry(CodexEntry entry)
		{
			if (activeEntry != null && entryButtons.TryGetValue(activeEntry.Id, out Button previousButton))
				previousButton.RemoveFromClassList(USS_ENTRY_BUTTON_ACTIVE);

			detailArea.Clear();
			activeEntry = entry;
			if (entry == null)
				return;

			if (entryButtons.TryGetValue(entry.Id, out Button currentButton))
				currentButton.AddToClassList(USS_ENTRY_BUTTON_ACTIVE);

			if (entry.IsUnlocked == false)
			{
				Label lockedLabel = new("???");
				detailArea.Add(lockedLabel);
				return;
			}

			if (activeCategory == null)
				return;

			VisualElement detail = activeCategory.BuildDetail(entry);
			if (detail != null)
				detailArea.Add(detail);
		}
	}
}
