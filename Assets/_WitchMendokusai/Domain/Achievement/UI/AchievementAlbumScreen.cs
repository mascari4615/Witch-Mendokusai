using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 업적 도감. 왼쪽 분류 판 (ESC 메뉴 큰 칸과 같은 평행사변형, 고른 분류는 밝게), 오른쪽 머리줄 (분류 이름, 모은 수, 거르기) 과 카드 격자.
	/// 분류와 거르기도 방향 키로 옮겨 가는 칸 (새 키 없이 확인으로 고름). 카드를 고르면 단계 상세
	/// </summary>
	public class AchievementAlbumScreen : VisualElement
	{
		public enum Filter
		{
			All = 0,
			Unlocked = 1,
			Locked = 2,
		}

		private const string USS_CLASS = "wm-ach-album";
		private const string USS_CATEGORY = "wm-ach-category";
		private const string USS_CATEGORY_ACTIVE = "wm-ach-category--active";
		private const string USS_FILTER = "wm-ach-filter";
		private const string USS_FILTER_ACTIVE = "wm-ach-filter--active";
		private const string ALL_NAME = "전체";
		private static readonly string[] FILTER_NAMES = { "모두", "모은 것", "아직" };

		private readonly AchievementScreenSettings settings;
		private readonly MenuFocus focus;
		private readonly Action<AchievementSO> openDetail;
		private readonly VisualElement categoryList;
		private readonly Label headerName;
		private readonly Label headerCount;
		private readonly VisualElement headerFill;
		private readonly List<Button> filterButtons = new();
		private readonly ScrollView scroll;
		private readonly VisualElement grid;
		private readonly List<AchievementCard> cards = new();
		private readonly List<AchievementCategorySO> categories = new();

		private AchievementManager manager;
		private AchievementCategorySO currentCategory;
		private Filter currentFilter;

		public AchievementAlbumScreen(AchievementScreenSettings settings, MenuFocus focus, Action<AchievementSO> openDetail)
		{
			this.settings = settings;
			this.focus = focus;
			this.openDetail = openDetail;
			AddToClassList(USS_CLASS);

			categoryList = AchievementElements.Part(this, "wm-ach-album__categories");

			VisualElement main = AchievementElements.Part(this, "wm-ach-album__main");
			VisualElement header = AchievementElements.Part(main, "wm-ach-album__header");
			headerName = AchievementElements.Text(header, string.Empty, "wm-ach-album__name");
			headerCount = AchievementElements.Text(header, string.Empty, "wm-ach-album__count");
			headerFill = AchievementElements.Bar(header, "wm-ach-album__bar");
			VisualElement filters = AchievementElements.Part(header, "wm-ach-album__filters");
			for (int i = 0; i < FILTER_NAMES.Length; i++)
			{
				Filter filter = (Filter)i;
				Button button = new(() => SelectFilter(filter)) { text = FILTER_NAMES[i] };
				button.AddToClassList(USS_FILTER);
				focus.Register(button);
				filters.Add(button);
				filterButtons.Add(button);
			}

			// 넘치는 카드만 세로 스크롤 (여백 규칙: 넘치는 목록만 한 방향 스크롤)
			scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.AddToClassList("wm-ach-album__scroll");
			scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
			main.Add(scroll);
			grid = AchievementElements.Part(scroll.contentContainer, "wm-ach-album__grid");
		}

		/// <summary>지금 고른 카드 (상세에서 돌아올 자리). 없으면 첫 카드, 카드가 없으면 지금 분류</summary>
		public VisualElement FirstFocus
		{
			get
			{
				if (cards.Count > 0)
				{
					return cards[0];
				}
				return categoryList.Q(className: USS_CATEGORY_ACTIVE);
			}
		}

		public AchievementCard CardOf(AchievementSO achievement) => cards.Find(card => card.Achievement == achievement);

		public void Bind(AchievementManager achievementManager)
		{
			manager = achievementManager;
			BuildCategories();
			BuildGrid();
		}

		private void BuildCategories()
		{
			categories.Clear();
			SOHelper.ForEach<AchievementCategorySO>(category => categories.Add(category));
			categories.Sort((left, right) => left.Order.CompareTo(right.Order));

			categoryList.Clear();
			AddCategory(null, ALL_NAME, settings.AllCategoryIcon);
			foreach (AchievementCategorySO category in categories)
			{
				AddCategory(category, category.Name, category.Sprite);
			}
		}

		private void AddCategory(AchievementCategorySO category, string label, UnityEngine.Sprite icon)
		{
			(int unlocked, int total) = Count(category);
			Button button = new(() => SelectCategory(category));
			// 판 모양과 고름 강조는 ESC 메뉴 큰 칸 (FullMenu.uss), 크기만 업적 USS
			button.AddToClassList("wm-fullmenu-main");
			button.AddToClassList(USS_CATEGORY);
			button.EnableInClassList(USS_CATEGORY_ACTIVE, category == currentCategory);
			VisualElement iconElement = AchievementElements.Part(button, "wm-fullmenu-main__icon wm-ach-category__icon");
			iconElement.style.backgroundImage = new StyleBackground(icon);
			AchievementElements.Text(button, label, "wm-ach-category__label");
			AchievementElements.Text(button, $"{unlocked} / {total}", "wm-ach-category__count");
			focus.Register(button);
			categoryList.Add(button);
		}

		private void SelectCategory(AchievementCategorySO category)
		{
			UiSfx.Play(UiSfxEvent.Confirm);
			currentCategory = category;
			foreach (VisualElement child in categoryList.Children())
			{
				child.RemoveFromClassList(USS_CATEGORY_ACTIVE);
			}
			int index = category == null ? 0 : categories.IndexOf(category) + 1;
			categoryList[index].AddToClassList(USS_CATEGORY_ACTIVE);
			BuildGrid();
		}

		private void SelectFilter(Filter filter)
		{
			UiSfx.Play(UiSfxEvent.Confirm);
			currentFilter = filter;
			BuildGrid();
		}

		private void BuildGrid()
		{
			AchievementBook book = manager.Book;
			(int unlocked, int total) = Count(currentCategory);
			headerName.text = currentCategory == null ? ALL_NAME : currentCategory.Name;
			headerCount.text = $"{unlocked} / {total}";
			headerFill.style.width = Length.Percent(total == 0 ? 0f : 100f * unlocked / total);
			for (int i = 0; i < filterButtons.Count; i++)
			{
				filterButtons[i].EnableInClassList(USS_FILTER_ACTIVE, (int)currentFilter == i);
			}

			grid.Clear();
			cards.Clear();
			foreach (AchievementSO achievement in manager.Achievements)
			{
				if (currentCategory != null && achievement.Category != currentCategory)
				{
					continue;
				}
				bool isUnlocked = book.TierOf(achievement.ID) > 0;
				if ((currentFilter == Filter.Unlocked && isUnlocked == false) || (currentFilter == Filter.Locked && isUnlocked))
				{
					continue;
				}
				AchievementCard card = new();
				card.clicked += () => openDetail(achievement);
				card.Bind(achievement, book, manager.CurrentValue(achievement));
				// 방향 키로 화면 밖 카드에 가면 따라 내려감
				card.RegisterCallback<FocusInEvent>(_ => scroll.ScrollTo(card));
				focus.Register(card);
				grid.Add(card);
				cards.Add(card);
			}
			if (cards.Count == 0)
			{
				AchievementElements.Text(grid, "여기에 보일 업적이 없음", "wm-ach-album__empty");
			}
		}

		private (int Unlocked, int Total) Count(AchievementCategorySO category)
		{
			int unlocked = 0;
			int total = 0;
			foreach (AchievementSO achievement in manager.Achievements)
			{
				if (category != null && achievement.Category != category)
				{
					continue;
				}
				total++;
				if (manager.Book.TierOf(achievement.ID) > 0)
				{
					unlocked++;
				}
			}
			return (unlocked, total);
		}
	}
}
