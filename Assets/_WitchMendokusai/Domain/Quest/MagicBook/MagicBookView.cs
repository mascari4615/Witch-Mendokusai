using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 마도서 전체 화면. 화면 전체가 밤하늘 (MagicBookSky) 이고, 위 줄은 장 이름과 투기장과 닫기, 아래 줄은 키 안내와 진행.
	/// 별을 고르면 옆에 팝업 (MagicBookView.Popup.cs, 안은 QuestDetail). 게임 화면을 비치지 않는 불투명 하늘.
	/// 글꼴과 색 토큰과 무대 배율은 ESC 메뉴 (FullMenu.uss, MenuStage) 를 같이 씀. 모양은 MagicBook.uss.
	/// 장 자료는 SOManager.DataSOs 에서 모음. 시안과 결정: memo wm/design/magic-book-20261122
	/// </summary>
	public partial class MagicBookView : MonoBehaviour
	{
		private const string USS_ROOT = "wm-fullmenu";
		private const string USS_ACTIVE = "wm-fullmenu--active";
		private const string USS_CLASS = "wm-mb";
		private const string NAV_CLASS = "wm-mb-nav";
		private const string USS_TAB = "wm-mb-tab";
		private const string USS_TAB_LOCKED = "wm-mb-tab--locked";
		private const string LOCKED_SUFFIX = " (잠김)";
		private const string PROGRESS_FORMAT = "마친 연구 {0} / {1}";

		/// <summary>UIRoot 가 만든 직후 넣어 줌 (UIRoot 프리팹의 MagicBookSettings)</summary>
		public MagicBookSettings Settings { get; set; }

		public bool IsOpen { get; private set; }

		private VisualElement container;
		private VisualElement stage;
		private MagicBookSky sky;
		private MenuFocus focus;
		private VisualElement tabRow;
		// 팝업이 덮지 않을 위아래 줄
		private VisualElement topBar;
		private VisualElement bottomBar;
		private Label progressLabel;
		private Button closeButton;
		private Texture2D glowTexture;
		// 전체 메뉴, 업적과 같은 UIRoot 오브젝트. 그 오브젝트를 정지 열쇠로 쓰면 서로의 정지를 풀어 버려 자기 열쇠를 따로
		private GameObject pauseKey;

		private readonly List<ChapterSO> chapters = new();
		private readonly Dictionary<ChapterSO, Button> tabsByChapter = new();

		private UIRoot uiRoot;
		private InputManager inputManager;
		private SOManager soManager;
		private TimeManager timeManager;
		private QuestManager questManager;

		// GameModeManager 주입 불가 -> 쓰는 자리에서 찾기
		// 이유: UIRoot(DontDestroyOnLoad)가 AddComponent 뒤 뿌리 컨테이너로 Inject
		//       GameModeManager 는 씬 스코프 -> 뿌리에 등록 없음 -> UIRoot.Construct 폭발 -> 부팅 정지 (2026-09-05 실측)
		[Inject]
		public void Construct(InputManager inputManager, SOManager soManager, TimeManager timeManager, QuestManager questManager, ResourceManager resourceManager, DataManager dataManager)
		{
			this.inputManager = inputManager;
			this.soManager = soManager;
			this.timeManager = timeManager;
			this.questManager = questManager;
			this.resourceManager = resourceManager;
			this.dataManager = dataManager;
		}

		private void Awake()
		{
			// uiRoot 는 같은 GameObject (UIRoot.CreateViews 가 AddComponent). [Inject] 로 받으면 UIRoot 와 순환 (TASK-WM-078)
			uiRoot = GetComponent<UIRoot>();
		}

		private void Start()
		{
			if (Settings == null)
			{
				throw new InvalidOperationException($"{nameof(MagicBookView)}: UIRoot 프리팹의 MagicBookSettings 가 비어 있음. 프리팹에 값이 있는데 비면 새 필드보다 프리팹이 먼저 임포트된 것 (UIRoot.prefab Reimport)");
			}

			// 하늘은 화면 끝까지 (container), 칸과 글은 안전 영역 (frame.Content). 기본 닫힘
			FullScreenFrame frame = uiRoot.CreateFullScreenFrame(uiRoot.ScreenLayer, "MagicBook");
			container = frame.Root;
			container.AddToClassList(USS_ROOT);
			container.AddToClassList(USS_CLASS);
			container.styleSheets.Add(Settings.Menu.Style);
			container.styleSheets.Add(Settings.Style);
			focus = new MenuFocus(container, NAV_CLASS, () => IsOpen, () => Settings.Menu.NavOverlapTolerance, () => Settings.Menu.NavAcrossWeight)
			{
				FocusFirst = FocusFirst,
			};
			pauseKey = new GameObject("MagicBookPauseKey");
			pauseKey.transform.SetParent(transform, false);
			glowTexture = MenuStage.CreateGlowTexture(Settings.Menu.GlowTextureSize, "MagicBookGlow");

			BuildStage(frame.Content);
			IsOpen = false;

			inputManager.RegisterInputEvent(InputEventType.MagicBookToggle, InputEventResponseType.Performed, Toggle);
		}

		private void OnDestroy()
		{
			// Start 전에 부서질 수 있음 (같은 프레임에 AddComponent 와 파괴). 그때는 구독도 화면도 없음
			if (container == null)
			{
				return;
			}
			if (IsOpen && TimeManager.TryGetExistingInstance(out TimeManager existingTimeManager))
			{
				existingTimeManager.Resume(pauseKey);
			}
			if (InputManager.TryGetExistingInstance(out InputManager existingInputManager))
			{
				existingInputManager.UnregisterInputEvent(InputEventType.MagicBookToggle, InputEventResponseType.Performed, Toggle);
			}
			Destroy(glowTexture);
			container.RemoveFromHierarchy();
		}

		private void BuildStage(VisualElement safeContent)
		{
			stage = new VisualElement();
			stage.AddToClassList("wm-fullmenu__stage");
			stage.AddToClassList("wm-mb__stage");
			safeContent.Add(stage);
			safeContent.RegisterCallback<GeometryChangedEvent>(_ => MenuStage.Fit(safeContent, stage, Settings.Menu.ReferenceHeight, Settings.Menu.MinimumWidth));

			// 하늘이 맨 아래, 위아래 줄과 팝업이 그 위
			sky = new MagicBookSky(Settings, glowTexture);
			sky.StarSubmitted += OnStarSubmitted;
			sky.CameraMoved += PlacePopup;
			stage.Add(sky);

			VisualElement top = Part(stage, "wm-fullmenu__top");
			topBar = top;
			top.AddToClassList("wm-mb__top");
			tabRow = Part(top, "wm-mb-tabs");
			Part(top, "wm-fullmenu__spacer");
			BuildSpellButton(top);
			Button arenaButton = new(EnterArena) { text = "투기장" };
			arenaButton.AddToClassList("wm-mb-arena");
			focus.Register(arenaButton);
			top.Add(arenaButton);
			closeButton = KeyHint.KeyButton("닫기", "ESC", Close);
			closeButton.AddToClassList("wm-fullmenu-close");
			focus.Register(closeButton);
			top.Add(closeButton);

			Part(stage, "wm-fullmenu__spacer");
			VisualElement bottom = Part(stage, "wm-mb__bottom");
			bottomBar = bottom;
			VisualElement hints = KeyHint.KeyOnly(Part(bottom, "wm-mb-hints"));
			AddHint(hints, "방향키", "별 고르기");
			AddHint(hints, "Z", "자세히");
			AddHint(hints, "휠", "확대");
			AddHint(hints, "끌기", "하늘 옮기기");
			AddHint(hints, "ESC", "뒤로");
			Part(bottom, "wm-fullmenu__spacer");
			progressLabel = new Label { pickingMode = PickingMode.Ignore };
			progressLabel.AddToClassList("wm-mb-progress");
			bottom.Add(progressLabel);

			BuildPopup();
			BuildSpellPanel();
		}

		private static void AddHint(VisualElement hints, string key, string text)
		{
			VisualElement hint = Part(hints, "wm-mb-hint");
			Label keyLabel = new(key) { pickingMode = PickingMode.Ignore };
			keyLabel.AddToClassList("wm-fullmenu-key");
			keyLabel.AddToClassList("wm-mb-hint__key");
			hint.Add(keyLabel);
			Label textLabel = new(text) { pickingMode = PickingMode.Ignore };
			textLabel.AddToClassList("wm-mb-hint__text");
			hint.Add(textLabel);
		}

		private static VisualElement Part(VisualElement parent, string className)
		{
			VisualElement element = new() { pickingMode = PickingMode.Ignore };
			element.AddToClassList(className);
			parent.Add(element);
			return element;
		}

		private void CollectChapters()
		{
			chapters.Clear();
			if (soManager.DataSOs.TryGetValue(typeof(ChapterSO), out Dictionary<int, DataSO> chapterDict) == false)
			{
				// 이 시점에 확실한 건 아직 안 들어왔다는 것뿐. 라벨 누락이면 로드 자체가 실패해 따로 뜸 (2026-08-06 실측)
				Debug.LogWarning("[MagicBookView] 마도서 챕터가 아직 안 들어왔다. 자료 로드보다 화면이 먼저 열렸을 수 있다. "
					+ "로드가 끝난 뒤에도 비어 있으면 챕터 자산의 Addressable 라벨과 번호 중복을 본다.");
				return;
			}
			foreach (DataSO dataSO in chapterDict.Values)
			{
				if (dataSO is ChapterSO chapterSO)
				{
					chapters.Add(chapterSO);
				}
			}
			chapters.Sort((left, right) => left.ID.CompareTo(right.ID));
			if (chapters.Count == 0)
			{
				Debug.LogWarning("[MagicBookView] ChapterSO 자산 0개. Addressable 라벨 'ChapterSO' 등록된 자산 없음");
			}
		}

		// 장 목록이 바뀌었을 때만 하늘과 탭을 다시 짬
		private void RebuildIfChanged()
		{
			bool same = chapters.Count == tabsByChapter.Count;
			foreach (ChapterSO chapter in chapters)
			{
				same &= tabsByChapter.ContainsKey(chapter);
			}
			if (same)
			{
				return;
			}
			sky.Build(chapters);
			foreach (MagicBookStar star in sky.Stars)
			{
				// 마우스 올리기로 고르지 않음 (카메라가 커서를 따라 흔들리지 않게). 클릭과 키만
				star.AddToClassList(NAV_CLASS);
			}
			tabRow.Clear();
			tabsByChapter.Clear();
			foreach (ChapterSO chapter in chapters)
			{
				ChapterSO captured = chapter;
				Button tab = new(() => OnTabSubmitted(captured));
				tab.AddToClassList(USS_TAB);
				focus.Register(tab);
				tabRow.Add(tab);
				tabsByChapter[chapter] = tab;
			}
		}

		private void RefreshState()
		{
			sky.Refresh(questManager);
			foreach (KeyValuePair<ChapterSO, Button> pair in tabsByChapter)
			{
				bool locked = sky.IsChapterLocked(pair.Key);
				pair.Value.text = MagicBookSky.ChapterTitle(pair.Key) + (locked ? LOCKED_SUFFIX : string.Empty);
				pair.Value.EnableInClassList(USS_TAB_LOCKED, locked);
			}
			progressLabel.text = string.Format(PROGRESS_FORMAT, sky.CountDone(), sky.Stars.Count);
		}

		private void OnTabSubmitted(ChapterSO chapter)
		{
			UiSfx.Play(UiSfxEvent.Confirm);
			HidePopup();
			sky.FlyToChapter(chapter);
			MagicBookStar first = sky.FirstStarOf(chapter);
			if (first != null && inputManager.IsTouchMode == false)
			{
				first.Focus();
			}
		}

		// 처음 고를 칸은 첫 장 이름 (장이 없으면 닫기). 열자마자 별을 고르면 카메라가 그 별로 가 하늘 전체가 안 보임
		private bool FocusFirst()
		{
			Button target = chapters.Count > 0 && tabsByChapter.TryGetValue(chapters[0], out Button tab) ? tab : closeButton;
			if (target.worldBound.width <= 0f)
			{
				return false;
			}
			target.Focus();
			return true;
		}

		// TASK-WM-165 item9. 마도서에서 투기장 (관전 모드) 으로 (사용자 확정 진입점). 카메라, 입력, 매치는 ArenaModeController
		private void EnterArena()
		{
			Close();
			if (GameModeManager.TryGetExistingInstance(out GameModeManager gameModeManager))
			{
				gameModeManager.SetMode(GameMode.Arena);
			}
		}

		public void Toggle()
		{
			if (IsOpen)
			{
				Close();
			}
			else
			{
				Open();
			}
		}

		public void Open()
		{
			if (IsOpen)
			{
				return;
			}
			IsOpen = true;
			// 매번 최신 목록 (SOManager 가 늦게 채워졌거나 자산이 더해졌을 수 있음)
			CollectChapters();
			RebuildIfChanged();
			RefreshState();
			HidePopup();
			UiSfx.Play(UiSfxEvent.WindowOpen);
			UIMotion.Show(container, USS_ACTIVE);
			timeManager.Pause(pauseKey);
			// 열린 직후에는 배치 계산 전이라 크기가 0. 잡힐 때까지 다음 프레임에 다시
			bool fitted = false;
			sky.schedule.Execute(() => fitted = IsOpen == false || FitSky()).Every(0).Until(() => fitted);
			if (inputManager.IsTouchMode == false)
			{
				focus.FocusWhenReady();
			}
		}

		// 하늘 전체를 위 줄과 아래 줄 사이에 담음. 두 줄의 배치가 끝나기 전이면 false
		private bool FitSky()
		{
			Rect skyLayout = sky.layout;
			Rect topLayout = topBar.layout;
			Rect bottomLayout = bottomBar.layout;
			if (float.IsNaN(skyLayout.height) || float.IsNaN(topLayout.yMax) || float.IsNaN(bottomLayout.yMin) || bottomLayout.height <= 0f)
			{
				return false;
			}
			sky.SetCovered(topLayout.yMax - skyLayout.yMin, skyLayout.yMax - bottomLayout.yMin);
			return sky.FitAll(true);
		}

		public void Close()
		{
			if (IsOpen == false)
			{
				return;
			}
			IsOpen = false;
			// 닫힌 뒤 확인 키가 숨은 버튼을 누르지 않게 포커스를 놓음
			if (container.focusController?.focusedElement is VisualElement focused && container.Contains(focused))
			{
				focused.Blur();
			}
			HidePopup();
			HideSpellPanel();
			UiSfx.Play(UiSfxEvent.WindowClose);
			UIMotion.Hide(container, USS_ACTIVE);
			timeManager.Resume(pauseKey);
		}

		/// <summary>뒤로 한 단계: 팝업이 떠 있으면 팝업, 아니면 닫기. UIManager 의 취소 입력도 여기로</summary>
		public void Back()
		{
			if (IsOpen == false)
			{
				return;
			}
			if (IsPopupShown)
			{
				UiSfx.Play(UiSfxEvent.WindowClose);
				HidePopup();
				return;
			}
			if (IsSpellPanelShown)
			{
				UiSfx.Play(UiSfxEvent.WindowClose);
				HideSpellPanel();
				return;
			}
			Close();
		}

		[ContextMenu("Open MagicBook")]
		private void DebugOpen() => Open();

		[ContextMenu("Close MagicBook")]
		private void DebugClose() => Close();
	}
}
