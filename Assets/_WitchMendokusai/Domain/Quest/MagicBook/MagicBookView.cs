using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 마도서 풀스크린 view (UI Toolkit). <see cref="UIRoot"/>.ScreenLayer 통합 — <see cref="SettingView"/> 패턴.
	/// 챕터 리스트 (좌측 사이드바) + 선택 챕터의 노드 그래프 (우측, <see cref="ChapterView"/>) + 우측 floating <see cref="QuestDetail"/>.
	///
	/// TASK-WM-059 C (2026-05-09) — uGUI UIMagicBookPanel 의 UI Toolkit 대체.
	/// TASK-WM-059 D (2026-05-10) — <see cref="QuestDetailRequestedEvent"/> 구독 → <see cref="QuestDetail"/> 띄움.
	/// EventBus (086 IEvent 인프라) 첫 도메인 사용처. Provider ↔ Host 결합 0.
	/// 챕터 데이터 = <see cref="SOManager"/>.DataSOs 자동 수집 (ChapterSO : DataSO 정합).
	/// </summary>
	public class MagicBookView : MonoBehaviour
	{
		public const string USS_CLASS = "wm-magic-book-view";
		public const string ACTIVE_CLASS = "wm-magic-book-view--active";

		private VisualElement container;
		private VisualElement chapterListContainer;
		private VisualElement chapterContentContainer;
		private QuestDetail questDetail;

		private readonly List<ChapterSO> chapters = new();
		private readonly Dictionary<ChapterSO, ChapterView> chapterViewsByChapter = new();
		private ChapterSO currentChapter;

		public bool IsOpen { get; private set; }

		private UIRoot uiRoot;
		private InputManager inputManager;
		private SOManager soManager;
		private TimeManager timeManager;
		private QuestManager questManager;

		[Inject]
		public void Construct(InputManager inputManager, SOManager soManager, TimeManager timeManager, QuestManager questManager)
		{
			this.inputManager = inputManager;
			this.soManager = soManager;
			this.timeManager = timeManager;
			this.questManager = questManager;
		}

		private void Awake()
		{
			// SettingView 정본 패턴 — uiRoot 는 같은 GameObject (UIRoot.CreateViews 가 AddComponent).
			// 과거 Construct(UIRoot,...) [Inject] 는 UIRoot ⇄ MagicBookView 순환 → Build 중 Lazy 재진입.
			// GetComponent 로 획득해 순환 엣지 제거 (TASK-WM-078, 2026-05-16).
			uiRoot = GetComponent<UIRoot>();
		}

		private static readonly Color BACKGROUND_COLOR = new Color(0.08f, 0.08f, 0.12f, 0.97f);
		private static readonly Color SIDEBAR_COLOR = new Color(0.13f, 0.13f, 0.18f, 1f);
		private static readonly Color DETAIL_BACKGROUND_COLOR = new Color(0.13f, 0.13f, 0.18f, 0.95f);
		private const float SIDEBAR_WIDTH = 220f;
		private const float DETAIL_WIDTH = 280f;

		private void Start()
		{
			container = new VisualElement();
			container.AddToClassList(USS_CLASS);

			// inline layout (USS stylesheet 없이도 작동) — 풀스크린 + flex row + default 닫힘
			container.style.position = Position.Absolute;
			container.style.left = 0;
			container.style.top = 0;
			container.style.right = 0;
			container.style.bottom = 0;
			container.style.flexDirection = FlexDirection.Row;
			container.style.backgroundColor = BACKGROUND_COLOR;
			container.style.display = DisplayStyle.None;

			uiRoot.ScreenLayer.Add(container);

			BuildUI();
			IsOpen = false;

			inputManager.RegisterInputEvent(InputEventType.MagicBookToggle, InputEventResponseType.Performed, Toggle);
			EventBusBridge.Subscribe<QuestDetailRequestedEvent>(OnQuestDetailRequested);
		}

		private void OnDestroy()
		{
			if (InputManager.TryGetExistingInstance(out InputManager inputManager))
				inputManager.UnregisterInputEvent(InputEventType.MagicBookToggle, InputEventResponseType.Performed, Toggle);

			EventBusBridge.Unsubscribe<QuestDetailRequestedEvent>(OnQuestDetailRequested);

			container?.RemoveFromHierarchy();
		}

		private void BuildUI()
		{
			chapterListContainer = new VisualElement();
			chapterListContainer.AddToClassList("wm-magic-book-list");
			chapterListContainer.style.width = SIDEBAR_WIDTH;
			chapterListContainer.style.flexShrink = 0;
			chapterListContainer.style.backgroundColor = SIDEBAR_COLOR;
			chapterListContainer.style.paddingTop = 12;
			chapterListContainer.style.paddingBottom = 12;
			chapterListContainer.style.paddingLeft = 12;
			chapterListContainer.style.paddingRight = 12;
			container.Add(chapterListContainer);

			chapterContentContainer = new VisualElement();
			chapterContentContainer.AddToClassList("wm-magic-book-content");
			chapterContentContainer.style.flexGrow = 1;
			container.Add(chapterContentContainer);

			Button btnClose = new Button(Close) { text = "닫기 (M)" };
			btnClose.AddToClassList("wm-magic-book-close");
			btnClose.style.position = Position.Absolute;
			btnClose.style.top = 12;
			btnClose.style.right = 12;
			container.Add(btnClose);

			questDetail = new QuestDetail();
			questDetail.style.position = Position.Absolute;
			questDetail.style.top = 60;
			questDetail.style.right = 12;
			questDetail.style.bottom = 12;
			questDetail.style.width = DETAIL_WIDTH;
			questDetail.style.backgroundColor = DETAIL_BACKGROUND_COLOR;
			questDetail.style.paddingTop = 12;
			questDetail.style.paddingBottom = 12;
			questDetail.style.paddingLeft = 12;
			questDetail.style.paddingRight = 12;
			container.Add(questDetail);

			CollectChapters();
			BuildChapterButtons();

			if (chapters.Count > 0)
				OpenChapter(chapters[0]);
		}

		private void CollectChapters()
		{
			chapters.Clear();

			if (soManager.DataSOs.TryGetValue(typeof(ChapterSO), out Dictionary<int, DataSO> chapterDict) == false)
			{
				Debug.LogWarning("[MagicBookView] SOManager.DataSOs[ChapterSO] 미등록 — DataLoader 가 Addressable 라벨 'ChapterSO' 로 자동 load. ChapterSO 자산 Inspector 에서 Addressable 라벨 'ChapterSO' 추가 필요");
				return;
			}

			foreach (DataSO dataSO in chapterDict.Values)
			{
				if (dataSO is ChapterSO chapterSO == false)
					continue;
				chapters.Add(chapterSO);
			}

			if (chapters.Count == 0)
				Debug.LogWarning("[MagicBookView] ChapterSO 자산 0개 — Addressable 라벨 'ChapterSO' 등록된 자산 없음");
		}

		private void BuildChapterButtons()
		{
			chapterListContainer.Clear();

			foreach (ChapterSO chapter in chapters)
			{
				ChapterSO captured = chapter;
				Button button = new Button(() => OpenChapter(captured)) { text = string.IsNullOrEmpty(chapter.Name) ? chapter.name : chapter.Name };
				button.AddToClassList("wm-magic-book-chapter-btn");
				chapterListContainer.Add(button);
			}

			// TASK-WM-165 item9 — 투기장(관전 모드) 진입 항목 (★사용자 컨펌 진입점). 스펠 연구 챕터와 구분해 하단 분리 배치.
			Button arenaButton = new Button(EnterArena) { text = "⚔ 투기장" };
			arenaButton.AddToClassList("wm-magic-book-arena-btn");
			arenaButton.style.marginTop = 16;
			chapterListContainer.Add(arenaButton);
		}

		// TASK-WM-165 item9 — 마도서 → 투기장 진입. 마도서 닫고 GameMode.Arena 로 (ArenaModeController 가 카메라/입력/매치 처리).
		private void EnterArena()
		{
			Close();
			GameModeManager.Instance.SetMode(GameMode.Arena);
		}

		private void OpenChapter(ChapterSO chapter)
		{
			if (currentChapter == chapter)
				return;

			currentChapter = chapter;
			chapterContentContainer.Clear();

			if (chapterViewsByChapter.TryGetValue(chapter, out ChapterView view) == false)
			{
				view = new ChapterView();
				view.Bind(chapter);
				chapterViewsByChapter[chapter] = view;
			}

			chapterContentContainer.Add(view);
		}

		public void Open()
		{
			if (IsOpen)
				return;
			IsOpen = true;

			// 매번 최신 ChapterSO 목록 (SOManager 가 늦게 초기화 됐을 가능성 + 동적 자산 추가 대응 — SettingView ShaderPack 패턴)
			CollectChapters();
			BuildChapterButtons();
			if (currentChapter == null && chapters.Count > 0)
				OpenChapter(chapters[0]);

			container.AddToClassList(ACTIVE_CLASS);
			container.style.display = DisplayStyle.Flex;
			timeManager.Pause(gameObject);
		}

		public void Close()
		{
			if (IsOpen == false)
				return;
			IsOpen = false;
			container.RemoveFromClassList(ACTIVE_CLASS);
			container.style.display = DisplayStyle.None;
			timeManager.Resume(gameObject);
		}

		public void Toggle()
		{
			if (IsOpen)
				Close();
			else
				Open();
		}

		private void OnQuestDetailRequested(QuestDetailRequestedEvent evt)
		{
			if (IsOpen == false)
				return;

			if (evt.QuestSOID == -1)
				return;

			QuestSO questSO = SOHelper.GetQuestSO(evt.QuestSOID);
			if (questSO == null)
				return;

			RuntimeQuest quest = questManager.GetQuest(questSO);
			questDetail.Bind(questSO, quest);
		}

		[ContextMenu("Open MagicBook")]
		private void DebugOpen() => Open();

		[ContextMenu("Close MagicBook")]
		private void DebugClose() => Close();
	}
}
