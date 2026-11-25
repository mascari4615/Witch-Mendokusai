using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using WitchMendokusai.DomainSDK.Discovery;

namespace WitchMendokusai
{
	/// <summary>
	/// 도감 전체 화면. 목록 (분류 판과 카드 격자) 과 한 항목의 장을 오가고, 뒤로 (ESC, 취소) 는 한 단계씩.
	/// 결은 ESC 메뉴와 업적: 1080 기준 무대, FullMenu.uss 와 Achievement.uss 의 판, 카드, 고리. 배경은 불투명.
	/// 몬스터 장은 MonsterLogPage, 그 밖의 분류는 DiscoveryDetailPanel. 새 기록 띠는 DiscoveryScreen.Toast.cs
	/// 시안과 결정: memo wm/reference/discovery-bench-20261125
	/// </summary>
	public partial class DiscoveryScreen : MonoBehaviour
	{
		public static DiscoveryScreen Instance { get; private set; }

		public static bool TryGetExistingInstance(out DiscoveryScreen screen)
		{
			screen = Instance;
			return screen != null;
		}

		private const string USS_ROOT = "wm-fullmenu";
		private const string USS_ACTIVE = "wm-fullmenu--active";
		private const string USS_CLASS = "wm-discovery";
		private const string USS_CARD_TOKENS = "wm-ach";
		private const string USS_HIDDEN = "wm-ach--hidden";
		private const string NAV_CLASS = "wm-discovery-nav";
		private const string TITLE = "도감";

		public bool IsOpen { get; private set; }

		private InputManager inputManager;
		private UIRoot uiRoot;
		private TimeManager timeManager;
		private MonsterLogManager monsterLog;
		private ResourceManager resourceManager;

		private VisualElement container;
		private VisualElement safeContent;
		private VisualElement stage;
		private MenuFocus focus;
		private DiscoveryListPage listPage;
		private VisualElement detailArea;
		private MonsterLogPage monsterPage;
		private Label detailHint;
		private Button backButton;
		private EntryDescriptor shownEntry;
		// 다른 전체 화면과 같은 오브젝트를 정지 열쇠로 쓰면 서로의 정지를 풀어 버림. 자기 열쇠를 따로
		private GameObject pauseKey;
		private Texture2D glowTexture;

		public EntryProviderRegistry Providers { get; } = new();

		private DiscoverySettings Settings => resourceManager.Discovery;

		[Inject]
		public void Construct(InputManager inputManager, UIRoot uiRoot, TimeManager timeManager, MonsterLogManager monsterLog, ResourceManager resourceManager)
		{
			this.inputManager = inputManager;
			this.uiRoot = uiRoot;
			this.timeManager = timeManager;
			this.monsterLog = monsterLog;
			this.resourceManager = resourceManager;
		}

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
		}

		private void Start()
		{
			RegisterBuiltins();
			BuildScreen();
			BuildToast();
			monsterLog.Changed += Refresh;
			EventBusBridge.Subscribe<MonsterLogStageEvent>(OnStageRaised);
			inputManager.RegisterInputEvent(InputEventType.DiscoveryToggle, InputEventResponseType.Performed, Toggle);
		}

		private void OnDestroy()
		{
			if (Instance == this)
			{
				Instance = null;
			}
			// Start 전에 부서질 수 있음. 그때는 구독도 화면도 아직 없음
			if (container == null)
			{
				return;
			}
			if (IsOpen && TimeManager.TryGetExistingInstance(out TimeManager existingTimeManager))
			{
				existingTimeManager.Resume(pauseKey);
			}
			monsterLog.Changed -= Refresh;
			EventBusBridge.Unsubscribe<MonsterLogStageEvent>(OnStageRaised);
			if (InputManager.TryGetExistingInstance(out InputManager existingInputManager))
			{
				existingInputManager.UnregisterInputEvent(InputEventType.DiscoveryToggle, InputEventResponseType.Performed, Toggle);
			}
			Destroy(glowTexture);
			DestroyToast();
			container.RemoveFromHierarchy();
		}

		private void RegisterBuiltins()
		{
			// 해금 출처 꽂기 (호스트 몫). 블록과 아이템은 출처 없음 -> 전부 열림 (해금 기록 부재)
			DiscoveryUnlocks.Register(new MonsterLogUnlockSource(monsterLog));
			DiscoveryUnlocks.Register(new PlantSpecimenUnlockSource());

			Providers.Register(new MonsterDiscoveryCategory(monsterLog, Settings));
			// 잠금 기록이 없거나 데모에서 안 쓰는 분류는 개발 빌드에만
			if (Debug.isDebugBuild)
			{
				Providers.Register(new PlantDiscoveryCategory());
				Providers.Register(new ItemDiscoveryCategory());
				Providers.Register(new BlockDiscoveryCategory());
			}
		}

		private void BuildScreen()
		{
			FullScreenFrame frame = uiRoot.CreateFullScreenFrame(uiRoot.ScreenLayer, "Discovery");
			container = frame.Root;
			safeContent = frame.Content;
			AchievementElements.AddClasses(container, $"{USS_ROOT} {USS_CLASS} {USS_CARD_TOKENS}");
			AddStyles(container);
			// 칸 이동보다 먼저. 장에서 좌우는 다른 항목으로
			container.RegisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
			focus = new MenuFocus(container, NAV_CLASS, () => IsOpen, () => Settings.Menu.NavOverlapTolerance, () => Settings.Menu.NavAcrossWeight)
			{
				FocusFirst = FocusFirst,
			};
			pauseKey = new GameObject("DiscoveryPauseKey");
			pauseKey.transform.SetParent(transform, false);

			stage = AchievementElements.Part(safeContent, "wm-fullmenu__stage");
			stage.pickingMode = PickingMode.Position;
			safeContent.RegisterCallback<GeometryChangedEvent>(_ => MenuStage.Fit(safeContent, stage, Settings.Menu.ReferenceHeight, Settings.Menu.MinimumWidth));

			VisualElement top = AchievementElements.Part(stage, "wm-fullmenu__top");
			AchievementElements.Part(top, "wm-fullmenu__spacer");
			backButton = KeyHint.KeyButton("뒤로", "ESC", Back);
			backButton.AddToClassList("wm-fullmenu-close");
			focus.Register(backButton);
			top.Add(backButton);
			AchievementElements.Text(stage, TITLE, "wm-fullmenu__title");

			VisualElement body = AchievementElements.Part(stage, "wm-ach__body");
			listPage = new DiscoveryListPage(Settings, focus, Providers.Providers, OpenEntry);
			body.Add(listPage);

			glowTexture = MenuStage.CreateGlowTexture(Settings.Menu.GlowTextureSize, "DiscoveryGlow");
			detailArea = AchievementElements.Part(body, "wm-discovery__detail");
			monsterPage = new MonsterLogPage(monsterLog, glowTexture);

			// 손가락 조작에서는 KeyHint 규칙으로 숨음
			VisualElement bottom = AchievementElements.Part(stage, "wm-ach__bottom");
			VisualElement hints = KeyHint.KeyOnly(AchievementElements.Part(bottom, "wm-ach-hints"));
			detailHint = AddHint(hints, "방향키", string.Empty);
			AddHint(hints, "ESC", "뒤로");
		}

		private void AddStyles(VisualElement element)
		{
			element.styleSheets.Add(Settings.Menu.Style);
			element.styleSheets.Add(Settings.Cards);
			element.styleSheets.Add(Settings.Style);
		}

		private static Label AddHint(VisualElement hints, string key, string text)
		{
			VisualElement hint = AchievementElements.Part(hints, "wm-ach-hint");
			AchievementElements.Text(hint, key, "wm-fullmenu-key wm-ach-hint__key");
			return AchievementElements.Text(hint, text, "wm-ach-hint__text");
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
			if (IsOpen || monsterLog.Book == null)
			{
				return;
			}
			IsOpen = true;
			ShowList();
			UiSfx.Play(UiSfxEvent.WindowOpen);
			UIMotion.Show(container, USS_ACTIVE);
			timeManager.Pause(pauseKey);
			FocusLater(null);
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
			UiSfx.Play(UiSfxEvent.WindowClose);
			UIMotion.Hide(container, USS_ACTIVE);
			timeManager.Resume(pauseKey);
		}

		/// <summary>뒤로 한 단계: 장, 목록 순. 목록에서는 닫힘. UIManager 의 취소 입력도 여기로</summary>
		public void Back()
		{
			if (IsOpen == false)
			{
				return;
			}
			if (shownEntry == null)
			{
				Close();
				return;
			}
			string shownId = shownEntry.Id;
			UiSfx.Play(UiSfxEvent.WindowClose);
			ShowList();
			FocusLater(listPage.CardOf(shownId));
		}

		private void ShowList()
		{
			shownEntry = null;
			detailArea.Clear();
			listPage.RemoveFromClassList(USS_HIDDEN);
			detailArea.AddToClassList(USS_HIDDEN);
			detailHint.text = "고르기";
			listPage.Bind();
		}

		private void OpenEntry(EntryDescriptor entry)
		{
			UiSfx.Play(UiSfxEvent.WindowOpen);
			ShowEntry(entry);
			FocusLater(backButton);
		}

		private void ShowEntry(EntryDescriptor entry)
		{
			shownEntry = entry;
			listPage.AddToClassList(USS_HIDDEN);
			detailArea.RemoveFromClassList(USS_HIDDEN);
			detailHint.text = "좌우로 다른 항목";
			detailArea.Clear();
			if (entry.Source is Monster monster)
			{
				monsterPage.Bind(monster);
				detailArea.Add(monsterPage);
				// 열어 보면 새 기록 표시가 사라짐
				monsterLog.MarkSeen(monster);
			}
			else
			{
				detailArea.Add(new DiscoveryDetailPanel(entry, listPage.CurrentProvider));
			}
		}

		// 장에서 좌우는 목록의 앞뒤 항목. 못 연 항목도 넘겨 볼 수 있으나 잠긴 일반 항목은 건너뜀
		private void OnNavigationMove(NavigationMoveEvent evt)
		{
			if (shownEntry == null)
			{
				return;
			}
			int step = evt.direction switch
			{
				NavigationMoveEvent.Direction.Left => -1,
				NavigationMoveEvent.Direction.Right => 1,
				_ => 0,
			};
			if (step == 0)
			{
				return;
			}
			evt.StopImmediatePropagation();
			container.focusController?.IgnoreEvent(evt);
			EntryDescriptor next = listPage.Neighbor(shownEntry.Id, step);
			if (next.Id != shownEntry.Id)
			{
				UiSfx.Play(UiSfxEvent.FocusMove);
				ShowEntry(next);
			}
		}

		// 기록이 바뀌면 (판 안에서 열려 있는 일은 없지만 확인 표시는 여기서 바뀜) 보이는 쪽을 다시 그림
		private void Refresh()
		{
			if (IsOpen == false || monsterLog.Book == null)
			{
				return;
			}
			if (shownEntry == null)
			{
				listPage.Bind();
			}
			else if (shownEntry.Source is Monster monster)
			{
				monsterPage.Bind(monster);
			}
		}

		// 화면을 바꾼 직후에는 배치 계산 전. 손가락 조작에는 선택 칸이 없음
		private void FocusLater(VisualElement target)
		{
			if (inputManager.IsTouchMode)
			{
				return;
			}
			if (target == null)
			{
				focus.FocusWhenReady();
				return;
			}
			container.schedule.Execute(() =>
			{
				if (target.panel != null)
				{
					target.Focus();
				}
			});
		}

		private bool FocusFirst()
		{
			VisualElement target = shownEntry == null ? listPage.FirstFocus : backButton;
			if (target == null || target.worldBound.width <= 0f)
			{
				return false;
			}
			target.Focus();
			return true;
		}
	}
}
