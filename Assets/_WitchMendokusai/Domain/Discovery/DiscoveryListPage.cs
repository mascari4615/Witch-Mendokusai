using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 도감 목록. 왼쪽 분류 판 (ESC 메뉴 큰 칸 결) 과 끝까지 적은 수, 오른쪽 머리줄 (분류, 만난 수, 세부 거르기) 과 카드 격자.
	/// 항목과 잠금은 IEntryProvider 가 답한 것을 그대로, 단계는 IDiscoveryRecordProvider 가 있으면 얹음
	/// </summary>
	public class DiscoveryListPage : VisualElement
	{
		private const string USS_CATEGORY_ACTIVE = "wm-ach-category--active";
		private const string USS_FILTER_ACTIVE = "wm-ach-filter--active";
		private const string ALL_LABEL = "모두";

		private readonly DiscoverySettings settings;
		private readonly MenuFocus focus;
		private readonly IReadOnlyList<IEntryProvider> providers;
		private readonly MonsterStampBaker stampBaker;
		private readonly Action<EntryDescriptor> openEntry;
		private readonly VisualElement categoryList;
		private readonly VisualElement completeBox;
		private readonly Label completeCount;
		private readonly Label completeTotal;
		private readonly VisualElement completeFill;
		private readonly Label headerName;
		private readonly Label headerCount;
		private readonly VisualElement headerFill;
		private readonly VisualElement filters;
		private readonly ScrollView scroll;
		private readonly VisualElement grid;
		private readonly List<DiscoveryScreenCard> cards = new();
		private string currentSubGroup;

		public IEntryProvider CurrentProvider { get; private set; }

		public DiscoveryListPage(DiscoverySettings settings, MenuFocus focus, IReadOnlyList<IEntryProvider> providers, MonsterStampBaker stampBaker, Action<EntryDescriptor> openEntry)
		{
			this.settings = settings;
			this.focus = focus;
			this.providers = providers;
			this.stampBaker = stampBaker;
			this.openEntry = openEntry;
			AddToClassList("wm-ach-album");
			CurrentProvider = providers[0];

			VisualElement left = AchievementElements.Part(this, "wm-ach-album__categories");
			categoryList = AchievementElements.Part(left, "wm-discovery-categories");
			completeBox = AchievementElements.Part(left, "wm-discovery-complete");
			AchievementElements.Text(completeBox, "끝까지 적은 기록", "wm-discovery-complete__label");
			VisualElement number = AchievementElements.Part(completeBox, "wm-discovery-complete__number");
			completeCount = AchievementElements.Text(number, string.Empty, "wm-discovery-complete__count");
			completeTotal = AchievementElements.Text(number, string.Empty, "wm-discovery-complete__total");
			completeFill = AchievementElements.Bar(completeBox, "wm-discovery-complete__bar");

			VisualElement main = AchievementElements.Part(this, "wm-ach-album__main");
			VisualElement header = AchievementElements.Part(main, "wm-ach-album__header");
			headerName = AchievementElements.Text(header, string.Empty, "wm-ach-album__name");
			headerCount = AchievementElements.Text(header, string.Empty, "wm-ach-album__count");
			headerFill = AchievementElements.Bar(header, "wm-ach-album__bar");
			filters = AchievementElements.Part(header, "wm-ach-album__filters");

			// 넘치는 카드만 세로 스크롤
			scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.AddToClassList("wm-ach-album__scroll");
			scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
			main.Add(scroll);
			grid = AchievementElements.Part(scroll.contentContainer, "wm-ach-album__grid");
		}

		/// <summary>처음 고를 칸. 카드가 없으면 지금 분류</summary>
		public VisualElement FirstFocus => cards.Count > 0 ? cards[0] : categoryList.Q(className: USS_CATEGORY_ACTIVE);

		public DiscoveryScreenCard CardOf(string entryId) => cards.Find(card => card.Entry.Id == entryId);

		/// <summary>지금 보이는 카드 순서에서 step 칸 옆의 항목. 끝이면 제자리. 장을 열 수 없는 잠긴 항목은 건너뜀</summary>
		public EntryDescriptor Neighbor(string entryId, int step)
		{
			int index = cards.FindIndex(card => card.Entry.Id == entryId);
			for (int next = index + step; next >= 0 && next < cards.Count; next += step)
			{
				if (CanOpen(cards[next].Entry))
				{
					return cards[next].Entry;
				}
			}
			return cards[index].Entry;
		}

		// 기록이 있는 분류는 못 만난 것도 장을 엶 (무엇이 적힐지 보임). 그 밖의 분류는 잠기면 안 엶
		private bool CanOpen(EntryDescriptor entry) => entry.IsUnlocked || CurrentProvider is IDiscoveryRecordProvider;

		/// <summary>항목을 새로 읽음. 화면을 열 때와 기록이 바뀐 때 한 번. 장에서 돌아올 때는 안 부름</summary>
		public void Activate()
		{
			foreach (IEntryProvider provider in providers)
			{
				provider.OnActivate();
			}
		}

		public void Deactivate()
		{
			grid.Clear();
			cards.Clear();
			foreach (IEntryProvider provider in providers)
			{
				provider.OnDeactivate();
			}
		}

		public void Bind()
		{
			BuildCategories();
			BuildFilters();
			BuildGrid();
		}

		private void BuildCategories()
		{
			categoryList.Clear();
			foreach (IEntryProvider provider in providers)
			{
				(int unlocked, int total) = Count(provider);
				IEntryProvider captured = provider;
				Button button = new(() => SelectCategory(captured));
				AchievementElements.AddClasses(button, "wm-fullmenu-main wm-ach-category");
				button.EnableInClassList(USS_CATEGORY_ACTIVE, provider == CurrentProvider);
				VisualElement icon = AchievementElements.Part(button, "wm-fullmenu-main__icon wm-ach-category__icon");
				icon.style.backgroundImage = new StyleBackground(provider.Icon != null ? provider.Icon : settings.OtherIcon);
				AchievementElements.Text(button, provider.DisplayName, "wm-ach-category__label");
				AchievementElements.Text(button, $"{unlocked} / {total}", "wm-ach-category__count");
				focus.Register(button);
				categoryList.Add(button);
			}
		}

		private void SelectCategory(IEntryProvider provider)
		{
			UiSfx.Play(UiSfxEvent.Confirm);
			CurrentProvider = provider;
			currentSubGroup = null;
			for (int i = 0; i < providers.Count; i++)
			{
				categoryList[i].EnableInClassList(USS_CATEGORY_ACTIVE, providers[i] == provider);
			}
			BuildFilters();
			BuildGrid();
		}

		private void BuildFilters()
		{
			filters.Clear();
			AddFilter(ALL_LABEL, null);
			foreach (string subGroup in CurrentProvider.SubGroups)
			{
				AddFilter(subGroup, subGroup);
			}
		}

		private void AddFilter(string label, string subGroup)
		{
			Button button = new() { text = label };
			button.clicked += () => SelectFilter(button, subGroup);
			button.AddToClassList("wm-ach-filter");
			button.EnableInClassList(USS_FILTER_ACTIVE, subGroup == currentSubGroup);
			focus.Register(button);
			filters.Add(button);
		}

		private void SelectFilter(Button selected, string subGroup)
		{
			UiSfx.Play(UiSfxEvent.Confirm);
			currentSubGroup = subGroup;
			foreach (VisualElement child in filters.Children())
			{
				child.EnableInClassList(USS_FILTER_ACTIVE, child == selected);
			}
			BuildGrid();
		}

		private void BuildGrid()
		{
			IDiscoveryRecordProvider records = CurrentProvider as IDiscoveryRecordProvider;
			(int unlocked, int total) = Count(CurrentProvider);
			headerName.text = CurrentProvider.DisplayName;
			headerCount.text = $"{unlocked} / {total}";
			headerFill.style.width = Length.Percent(total == 0 ? 0f : 100f * unlocked / total);

			grid.Clear();
			cards.Clear();
			foreach (EntryDescriptor entry in CurrentProvider.GetEntries())
			{
				if (currentSubGroup == null || entry.SubGroup == currentSubGroup)
				{
					AddCard(entry, records);
				}
			}
			BindComplete(records);
		}

		private void AddCard(EntryDescriptor entry, IDiscoveryRecordProvider records)
		{
			DiscoveryScreenCard card = new(entry);
			card.clicked += () =>
			{
				if (CanOpen(entry))
				{
					openEntry(entry);
				}
			};
			if (records != null)
			{
				DiscoveryRecordInfo record = records.RecordOf(entry);
				card.Bind(record);
				if (record.IsComplete && entry.Source is Monster monster)
				{
					card.AddStamp(stampBaker.StampOf(monster));
				}
			}
			// 방향 키로 화면 밖 카드에 가면 따라 내려감
			card.RegisterCallback<FocusInEvent>(_ => scroll.ScrollTo(card));
			focus.Register(card);
			grid.Add(card);
			cards.Add(card);
		}

		// 거르기와 상관없이 분류 전체에서 끝까지 채운 수. 단계가 없는 분류는 칸을 숨김 (자리는 그대로)
		private void BindComplete(IDiscoveryRecordProvider records)
		{
			completeBox.style.visibility = records == null ? Visibility.Hidden : Visibility.Visible;
			if (records == null)
			{
				return;
			}
			IReadOnlyList<EntryDescriptor> entries = CurrentProvider.GetEntries();
			int complete = 0;
			foreach (EntryDescriptor entry in entries)
			{
				if (records.RecordOf(entry).IsComplete)
				{
					complete++;
				}
			}
			completeCount.text = $"{complete}";
			completeTotal.text = $"/ {entries.Count}";
			completeFill.style.width = Length.Percent(entries.Count == 0 ? 0f : 100f * complete / entries.Count);
		}

		private static (int Unlocked, int Total) Count(IEntryProvider provider)
		{
			IReadOnlyList<EntryDescriptor> entries = provider.GetEntries();
			int unlocked = 0;
			foreach (EntryDescriptor entry in entries)
			{
				if (entry.IsUnlocked)
				{
					unlocked++;
				}
			}
			return (unlocked, entries.Count);
		}
	}
}
