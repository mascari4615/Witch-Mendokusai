using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 도감 윈도우 본체 — 엔드필드식. 3 모드 전환.
	/// - Root: 큰 주제 버튼 (블록/아이템/주민)
	/// - Category: 뒤로 + 좌 사이드바(세부 분류) + 우 카드 그리드
	/// - Detail: 뒤로 + BuildDetail
	/// 시각 톤은 `Resources/Codex/CodexWindow.uss`.
	/// </summary>
	public class CodexView : VisualElement
	{
		public const string USS_CLASS = "wm-codex";
		public const string USS_ROOT = "wm-codex__root";
		public const string USS_ROOT_BUTTON = "wm-codex__root-button";
		public const string USS_CATEGORY = "wm-codex__category";
		public const string USS_CATEGORY_BODY = "wm-codex__category-body";
		public const string USS_SIDEBAR = "wm-codex__sidebar";
		public const string USS_SIDEBAR_BUTTON = "wm-codex__sidebar-button";
		public const string USS_SIDEBAR_BUTTON_ACTIVE = "wm-codex__sidebar-button--active";
		public const string USS_BACK_BUTTON = "wm-codex__back-button";
		public const string USS_GRID = "wm-codex__grid";
		public const string USS_GRID_CONTENT = "wm-codex__grid-content";
		public const string USS_DETAIL = "wm-codex__detail";
		public const string USS_DETAIL_CONTENT = "wm-codex__detail-content";

		private const string ALL_KEY = "__all__";
		private const string ALL_LABEL = "전체";

		public event Action<ICodexCategory> OnCategorySelected = delegate { };
		public event Action<CodexEntry> OnEntrySelected = delegate { };

		private enum CodexMode
		{
			Root,
			Category,
			Detail,
		}

		private readonly VisualElement rootArea;
		private readonly VisualElement categoryArea;
		private readonly Button categoryBackButton;
		private readonly VisualElement categoryBody;
		private readonly VisualElement sidebar;
		private readonly ScrollView gridScroll;
		private readonly VisualElement gridContent;
		private readonly VisualElement detailArea;
		private readonly Button detailBackButton;
		private readonly VisualElement detailContent;

		private readonly Dictionary<string, Button> sidebarButtons = new();
		private readonly Dictionary<string, CodexCard> cards = new();

		private ICodexCategory activeCategory;
		private CodexEntry activeEntry;
		private CodexMode currentMode;
		private string currentSubGroup;

		public ICodexCategory ActiveCategory => activeCategory;
		public CodexEntry ActiveEntry => activeEntry;

		public CodexView()
		{
			AddToClassList(USS_CLASS);
			style.flexDirection = FlexDirection.Column;
			style.flexGrow = 1;

			rootArea = new VisualElement();
			rootArea.AddToClassList(USS_ROOT);
			rootArea.style.flexGrow = 1;
			rootArea.style.flexDirection = FlexDirection.Row;
			rootArea.style.justifyContent = Justify.Center;
			rootArea.style.alignItems = Align.Center;

			categoryArea = new VisualElement();
			categoryArea.AddToClassList(USS_CATEGORY);
			categoryArea.style.flexGrow = 1;
			categoryArea.style.flexDirection = FlexDirection.Column;

			categoryBackButton = new Button(BackToRoot)
			{
				text = "← 뒤로",
			};
			categoryBackButton.AddToClassList(USS_BACK_BUTTON);
			categoryArea.Add(categoryBackButton);

			categoryBody = new VisualElement();
			categoryBody.AddToClassList(USS_CATEGORY_BODY);
			categoryBody.style.flexDirection = FlexDirection.Row;
			categoryBody.style.flexGrow = 1;
			categoryArea.Add(categoryBody);

			sidebar = new VisualElement();
			sidebar.AddToClassList(USS_SIDEBAR);
			sidebar.style.width = 140;
			sidebar.style.flexShrink = 0;
			categoryBody.Add(sidebar);

			gridScroll = new ScrollView(ScrollViewMode.Vertical);
			gridScroll.AddToClassList(USS_GRID);
			gridScroll.style.flexGrow = 1;
			gridContent = new VisualElement();
			gridContent.AddToClassList(USS_GRID_CONTENT);
			gridContent.style.flexDirection = FlexDirection.Row;
			gridContent.style.flexWrap = Wrap.Wrap;
			gridScroll.Add(gridContent);
			categoryBody.Add(gridScroll);

			detailArea = new VisualElement();
			detailArea.AddToClassList(USS_DETAIL);
			detailArea.style.flexGrow = 1;
			detailArea.style.flexDirection = FlexDirection.Column;

			detailBackButton = new Button(BackToCategory)
			{
				text = "← 뒤로",
			};
			detailBackButton.AddToClassList(USS_BACK_BUTTON);
			detailArea.Add(detailBackButton);

			detailContent = new VisualElement();
			detailContent.AddToClassList(USS_DETAIL_CONTENT);
			detailContent.style.flexGrow = 1;
			detailArea.Add(detailContent);

			SetMode(CodexMode.Root);
		}

		private void SetMode(CodexMode mode)
		{
			currentMode = mode;

			Clear();
			if (mode == CodexMode.Root)
				Add(rootArea);
			else if (mode == CodexMode.Category)
				Add(categoryArea);
			else
				Add(detailArea);
		}

		public void RebuildRoot(IReadOnlyList<ICodexCategory> categories)
		{
			rootArea.Clear();

			for (int i = 0; i < categories.Count; i++)
			{
				ICodexCategory category = categories[i];
				ICodexCategory captured = category;
				Button button = new(() => OnCategorySelected.Invoke(captured))
				{
					text = category.DisplayName,
				};
				button.AddToClassList(USS_ROOT_BUTTON);
				rootArea.Add(button);
			}
		}

		public void SetActiveCategory(ICodexCategory category)
		{
			if (activeCategory != null)
				activeCategory.OnDeactivate();

			detailContent.Clear();
			activeEntry = null;
			currentSubGroup = null;

			activeCategory = category;
			if (category == null)
			{
				SetMode(CodexMode.Root);
				return;
			}

			category.OnActivate();
			RebuildSidebar();
			RebuildGrid();

			if (sidebarButtons.TryGetValue(ALL_KEY, out Button allButton))
				allButton.AddToClassList(USS_SIDEBAR_BUTTON_ACTIVE);

			SetMode(CodexMode.Category);
		}

		private void RebuildSidebar()
		{
			sidebar.Clear();
			sidebarButtons.Clear();

			Button allButton = new(() => SetSubGroup(null))
			{
				text = ALL_LABEL,
			};
			allButton.AddToClassList(USS_SIDEBAR_BUTTON);
			sidebarButtons[ALL_KEY] = allButton;
			sidebar.Add(allButton);

			IReadOnlyList<string> subGroups = activeCategory.SubGroups;
			if (subGroups == null)
				return;

			for (int i = 0; i < subGroups.Count; i++)
			{
				string subGroup = subGroups[i];
				string captured = subGroup;
				Button button = new(() => SetSubGroup(captured))
				{
					text = subGroup,
				};
				button.AddToClassList(USS_SIDEBAR_BUTTON);
				sidebarButtons[subGroup] = button;
				sidebar.Add(button);
			}
		}

		private void SetSubGroup(string subGroup)
		{
			string previousKey = currentSubGroup ?? ALL_KEY;
			if (sidebarButtons.TryGetValue(previousKey, out Button previousButton))
				previousButton.RemoveFromClassList(USS_SIDEBAR_BUTTON_ACTIVE);

			currentSubGroup = subGroup;
			string currentKey = currentSubGroup ?? ALL_KEY;
			if (sidebarButtons.TryGetValue(currentKey, out Button currentButton))
				currentButton.AddToClassList(USS_SIDEBAR_BUTTON_ACTIVE);

			RebuildGrid();
		}

		private void RebuildGrid()
		{
			gridContent.Clear();
			cards.Clear();

			if (activeCategory == null)
				return;

			IReadOnlyList<CodexEntry> entries = activeCategory.GetEntries();
			for (int i = 0; i < entries.Count; i++)
			{
				CodexEntry entry = entries[i];
				if (currentSubGroup != null && entry.SubGroup != currentSubGroup)
					continue;

				CodexEntry captured = entry;
				CodexCard card = new(entry);
				card.OnClicked += () => OnEntrySelected.Invoke(captured);
				cards[entry.Id] = card;
				gridContent.Add(card);
			}
		}

		public void SetActiveEntry(CodexEntry entry)
		{
			if (activeEntry != null && cards.TryGetValue(activeEntry.Id, out CodexCard previousCard))
				previousCard.SetActive(false);

			detailContent.Clear();
			activeEntry = entry;
			if (entry == null)
			{
				SetMode(CodexMode.Category);
				return;
			}

			if (cards.TryGetValue(entry.Id, out CodexCard currentCard))
				currentCard.SetActive(true);

			if (entry.IsUnlocked == false)
			{
				detailContent.Add(new Label("???"));
				SetMode(CodexMode.Detail);
				return;
			}

			if (activeCategory == null)
				return;

			CodexDetailPanel panel = new(entry, activeCategory);
			detailContent.Add(panel);

			SetMode(CodexMode.Detail);
		}

		private void BackToRoot()
		{
			if (activeCategory != null)
				activeCategory.OnDeactivate();
			activeCategory = null;
			activeEntry = null;
			currentSubGroup = null;
			gridContent.Clear();
			cards.Clear();
			detailContent.Clear();
			sidebar.Clear();
			sidebarButtons.Clear();
			SetMode(CodexMode.Root);
		}

		private void BackToCategory()
		{
			if (activeEntry != null && cards.TryGetValue(activeEntry.Id, out CodexCard previousCard))
				previousCard.SetActive(false);
			activeEntry = null;
			detailContent.Clear();
			SetMode(CodexMode.Category);
		}
	}
}
