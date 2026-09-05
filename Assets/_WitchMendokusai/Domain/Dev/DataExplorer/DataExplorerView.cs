using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 데이터 탐색 베이스 view — 좌 사이드바(SubGroups) + 우 카드 그리드.
	/// Discovery (런타임 도감) + DataSOWindow (에디터 툴) 둘 다 사용. (구) DiscoveryView 의 Category 모드 부분을 추출.
	///
	/// 디테일 영역은 X — 도메인 controller 가 OnEntrySelected 받아 자체 처리:
	/// - Discovery = Detail 모드 전환 (DiscoveryDetailPanel)
	/// - DataSOWindow = Inspector 연동 (Selection.activeObject)
	/// </summary>
	public class DataExplorerView : VisualElement
	{
		public const string USS_CLASS = "wm-data-explorer";
		public const string USS_SIDEBAR = "wm-data-explorer__sidebar";
		public const string USS_SIDEBAR_BUTTON = "wm-data-explorer__sidebar-button";
		public const string USS_SIDEBAR_BUTTON_ACTIVE = "wm-data-explorer__sidebar-button--active";
		public const string USS_GRID = "wm-data-explorer__grid";
		public const string USS_GRID_CONTENT = "wm-data-explorer__grid-content";

		private const string ALL_KEY = "__all__";
		private const string ALL_LABEL = "전체";

		public event Action<EntryDescriptor> OnEntrySelected = delegate { };

		private readonly VisualElement sidebar;
		private readonly ScrollView gridScroll;
		private readonly VisualElement gridContent;

		private readonly Dictionary<string, Button> sidebarButtons = new();
		private readonly Dictionary<string, DiscoveryCard> cards = new();

		private IEntryProvider activeProvider;
		private string currentSubGroup;

		public IEntryProvider ActiveProvider => activeProvider;

		public DataExplorerView()
		{
			AddToClassList(USS_CLASS);
			style.flexDirection = FlexDirection.Row;
			style.flexGrow = 1;

			sidebar = new VisualElement();
			sidebar.AddToClassList(USS_SIDEBAR);
			sidebar.style.width = 140;
			sidebar.style.flexShrink = 0;
			Add(sidebar);

			gridScroll = new ScrollView(ScrollViewMode.Vertical);
			gridScroll.AddToClassList(USS_GRID);
			gridScroll.style.flexGrow = 1;
			gridContent = new VisualElement();
			gridContent.AddToClassList(USS_GRID_CONTENT);
			gridContent.style.flexDirection = FlexDirection.Row;
			gridContent.style.flexWrap = Wrap.Wrap;
			gridScroll.Add(gridContent);
			Add(gridScroll);
		}

		public void SetActiveProvider(IEntryProvider provider)
		{
			if (activeProvider != null)
				activeProvider.OnDeactivate();

			sidebar.Clear();
			sidebarButtons.Clear();
			gridContent.Clear();
			cards.Clear();
			currentSubGroup = null;

			activeProvider = provider;
			if (provider == null)
				return;

			provider.OnActivate();
			RebuildSidebar();
			RebuildGrid();

			if (sidebarButtons.TryGetValue(ALL_KEY, out Button allButton))
				allButton.AddToClassList(USS_SIDEBAR_BUTTON_ACTIVE);
		}

		/// <summary>외부 controller (DiscoveryView 등) 가 entry 선택 시 카드 강조 토글.</summary>
		public void SetEntryActive(EntryDescriptor entry, bool active)
		{
			if (entry == null)
				return;
			if (cards.TryGetValue(entry.Id, out DiscoveryCard card))
				card.SetActive(active);
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

			IReadOnlyList<string> subGroups = activeProvider.SubGroups;
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

			if (activeProvider == null)
				return;

			IReadOnlyList<EntryDescriptor> entries = activeProvider.GetEntries();
			for (int i = 0; i < entries.Count; i++)
			{
				EntryDescriptor entry = entries[i];
				if (currentSubGroup != null && entry.SubGroup != currentSubGroup)
					continue;

				EntryDescriptor captured = entry;
				DiscoveryCard card = new(entry);
				card.OnClicked += () => OnEntrySelected.Invoke(captured);
				cards[entry.Id] = card;
				gridContent.Add(card);
			}
		}
	}
}
