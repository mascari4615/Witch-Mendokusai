using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 업적 창과 달성 띠. 창은 왼쪽 분류 탭, 가운데 머리줄과 칸 격자, 오른쪽 상세.
	/// 분류 탭을 넘기면 칸 모양만 그 분류의 것으로 바뀜 (칸 그림은 AchievementCategorySO).
	/// 판정과 기록은 AchievementManager, 여기는 그리기만
	/// </summary>
	public class AchievementView : MonoBehaviour
	{
		private const string WINDOW_ID = "Achievement";
		private const string ALL_TAB = "전체";

		public const string USS_BODY = "wm-ach-body";
		public const string USS_TABS = "wm-ach-tabs";
		public const string USS_TAB = "wm-ach-tab";
		public const string USS_TAB_ACTIVE = "wm-ach-tab--active";
		public const string USS_MAIN = "wm-ach-main";
		public const string USS_HEADER = "wm-ach-header";
		public const string USS_GRID = "wm-ach-grid";

		[SerializeField, Tooltip("달성 띠가 떠 있는 시간 (초)")] private float toastHoldSeconds = 3f;
		[SerializeField, Tooltip("띠가 사라지는 움직임을 기다리는 시간 (초). USS transition 과 맞춤")] private float toastFadeSeconds = 0.3f;

		private WMWindow window;
		private VisualElement tabs;
		private Label header;
		private VisualElement grid;
		private AchievementDetail detail;
		private AchievementToast toast;

		private readonly List<AchievementCategorySO> categories = new();
		private readonly List<AchievementSlot> slots = new();
		private readonly Queue<AchievementUnlockedEvent> toastQueue = new();
		private AchievementCategorySO currentCategory;
		private AchievementSO selected;
		private bool toastPlaying;

		private UIRoot uiRoot;
		private AchievementManager achievementManager;
		private InputManager inputManager;

		[Inject]
		public void Construct(UIRoot uiRoot, AchievementManager achievementManager, InputManager inputManager)
		{
			this.uiRoot = uiRoot;
			this.achievementManager = achievementManager;
			this.inputManager = inputManager;
		}

		private void Start()
		{
			window = new WMWindow
			{
				WindowId = WINDOW_ID,
				Title = "업적"
			};
			window.style.left = 180;
			window.style.top = 80;
			window.style.width = 780;
			window.style.height = 460;
			uiRoot.WindowsLayer.Add(window);

			VisualElement body = new();
			body.AddToClassList(USS_BODY);
			window.Content.Add(body);

			tabs = new VisualElement();
			tabs.AddToClassList(USS_TABS);
			body.Add(tabs);

			VisualElement main = new();
			main.AddToClassList(USS_MAIN);
			body.Add(main);

			header = new Label();
			header.AddToClassList(USS_HEADER);
			main.Add(header);

			ScrollView scroll = new(ScrollViewMode.Vertical);
			scroll.style.flexGrow = 1;
			main.Add(scroll);
			grid = new VisualElement();
			grid.AddToClassList(USS_GRID);
			scroll.Add(grid);

			detail = new AchievementDetail();
			body.Add(detail);

			toast = new AchievementToast();
			uiRoot.OverlayLayer.Add(toast);

			achievementManager.Changed += Refresh;
			EventBusBridge.Subscribe<AchievementUnlockedEvent>(OnUnlocked);
			inputManager.RegisterInputEvent(InputEventType.AchievementToggle, InputEventResponseType.Performed, OnToggle);
			window.OnOpened += Refresh;
		}

		private void OnDestroy()
		{
			if (achievementManager != null)
				achievementManager.Changed -= Refresh;
			EventBusBridge.Unsubscribe<AchievementUnlockedEvent>(OnUnlocked);
			if (inputManager != null)
				inputManager.UnregisterInputEvent(InputEventType.AchievementToggle, InputEventResponseType.Performed, OnToggle);
			toast?.RemoveFromHierarchy();
		}

		private void OnToggle() => window?.Toggle();

		private void Refresh()
		{
			if (window == null || window.IsOpen == false || achievementManager.Book == null)
			{
				return;
			}
			BuildTabs();
			BuildGrid();
		}

		private void BuildTabs()
		{
			categories.Clear();
			SOHelper.ForEach<AchievementCategorySO>(category => categories.Add(category));
			categories.Sort((left, right) => left.Order.CompareTo(right.Order));

			tabs.Clear();
			AddTab(ALL_TAB, null);
			foreach (AchievementCategorySO category in categories)
			{
				AddTab(category.Name, category);
			}
		}

		private void AddTab(string label, AchievementCategorySO category)
		{
			Button tab = new(() => SelectCategory(category)) { text = label };
			tab.AddToClassList(USS_TAB);
			tab.AddToClassList(WMWindow.NAV_CLASS);
			tab.EnableInClassList(USS_TAB_ACTIVE, category == currentCategory);
			tabs.Add(tab);
		}

		private void SelectCategory(AchievementCategorySO category)
		{
			currentCategory = category;
			selected = null;
			Refresh();
		}

		private void BuildGrid()
		{
			AchievementBook book = achievementManager.Book;
			grid.Clear();
			slots.Clear();
			int total = 0;
			int unlocked = 0;
			foreach (AchievementSO achievement in achievementManager.Achievements)
			{
				if (currentCategory != null && achievement.Category != currentCategory)
				{
					continue;
				}
				int tier = book.TierOf(achievement.ID);
				total++;
				if (tier > 0)
				{
					unlocked++;
				}

				AchievementSlot slot = new(withCaption: true);
				slot.AddToClassList(WMWindow.NAV_CLASS);
				slot.Bind(achievement, tier);
				slot.SetSelected(achievement == selected);
				slot.RegisterCallback<ClickEvent>(_ => Select(achievement));
				slot.RegisterCallback<NavigationSubmitEvent>(_ => Select(achievement));
				grid.Add(slot);
				slots.Add(slot);
			}

			string motif = currentCategory != null && string.IsNullOrEmpty(currentCategory.MotifName) == false ? currentCategory.MotifName : "업적";
			header.text = $"{motif} {unlocked} / {total}    1단계 {book.CountAtTier(1)}, 2단계 {book.CountAtTier(2)}, 3단계 {book.CountAtTier(3)}";

			if (selected != null)
			{
				detail.Bind(selected, book, achievementManager.CurrentValue(selected));
			}
			else
			{
				detail.Clear("업적을 고르면 여기에 보임");
			}
		}

		private void Select(AchievementSO achievement)
		{
			selected = achievement;
			foreach (AchievementSlot slot in slots)
			{
				slot.SetSelected(slot.Achievement == achievement);
			}
			detail.Bind(achievement, achievementManager.Book, achievementManager.CurrentValue(achievement));
		}

		private void OnUnlocked(AchievementUnlockedEvent evt)
		{
			toastQueue.Enqueue(evt);
			if (toastPlaying == false)
			{
				StartCoroutine(PlayToasts());
			}
		}

		private IEnumerator PlayToasts()
		{
			toastPlaying = true;
			while (toastQueue.Count > 0)
			{
				AchievementUnlockedEvent evt = toastQueue.Dequeue();
				toast.Show(SOHelper.Get<AchievementSO>(evt.AchievementId), evt.Tier);
				yield return new WaitForSecondsRealtime(toastHoldSeconds);
				toast.Hide();
				yield return new WaitForSecondsRealtime(toastFadeSeconds);
			}
			toastPlaying = false;
		}
	}
}
