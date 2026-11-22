using System;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 업적 전체 화면. 메달 벽 (첫 화면), 도감, 단계 상세 (가운데 창) 를 오가고, 뒤로 (ESC, 취소) 는 한 단계씩.
	/// 결은 ESC 메뉴 그대로: 흐린 게임 화면 배경, 1080 기준 무대, FullMenu.uss 의 판과 키 칸 (AchievementScreenSettings).
	/// 판정과 기록은 AchievementManager, 여기는 그리기와 화면 전환만. 달성 띠는 AchievementView.Toast.cs
	/// 시안과 결정: memo wm/reference/achievement-bench-20261122
	/// </summary>
	public partial class AchievementView : MonoBehaviour
	{
		private enum Page
		{
			Wall = 0,
			Album = 1,
		}

		// 뿌리는 ESC 메뉴와 같은 클래스 (색 토큰, 글꼴, 열고 닫는 투명도). 업적만의 모양은 wm-ach
		private const string USS_ROOT = "wm-fullmenu";
		private const string USS_ACTIVE = "wm-fullmenu--active";
		private const string USS_CLASS = "wm-ach";
		private const string USS_PALETTE = "wm-fullmenu-palette";
		private const string USS_HIDDEN = "wm-ach--hidden";
		private const string NAV_CLASS = "wm-ach-nav";
		private const string TITLE_WALL = "업적";
		private const string TITLE_ALBUM = "업적 도감";

		/// <summary>UIManager 가 만든 직후 넣어 줌 (UIRoot 프리팹의 AchievementSettings)</summary>
		public AchievementScreenSettings Settings { get; set; }

		public bool IsOpen { get; private set; }

		private VisualElement container;
		private VisualElement safeContent;
		private VisualElement stage;
		private MenuBackdrop backdrop;
		private MenuFocus focus;
		private Label titleLabel;
		private AchievementWallScreen wallScreen;
		private AchievementAlbumScreen albumScreen;
		private AchievementDetail detail;
		private Page page;
		private VisualElement focusOnDetailClose;
		// 전체 메뉴와 같은 UIRoot 오브젝트에 붙어 있어 그 오브젝트를 정지 열쇠로 쓰면 서로의 정지를 풀어 버림. 자기 열쇠를 따로
		private GameObject pauseKey;
		// 벽과 상세 창 뒤 둥근 빛. 한 장을 같이 씀
		private Texture2D glowTexture;
		private VisualElement bottom;
		private Button backButton;

		private UIRoot uiRoot;
		private AchievementManager achievementManager;
		private InputManager inputManager;
		private TimeManager timeManager;

		[Inject]
		public void Construct(UIRoot uiRoot, AchievementManager achievementManager, InputManager inputManager, TimeManager timeManager)
		{
			this.uiRoot = uiRoot;
			this.achievementManager = achievementManager;
			this.inputManager = inputManager;
			this.timeManager = timeManager;
		}

		private void Start()
		{
			if (Settings == null)
			{
				throw new InvalidOperationException($"{nameof(AchievementView)}: UIRoot 프리팹의 AchievementSettings 가 비어 있음. 프리팹에 값이 있는데 비면 새 필드보다 프리팹이 먼저 임포트된 것 (UIRoot.prefab Reimport)");
			}

			// 흐린 배경은 화면 끝까지 (container), 칸과 글은 안전 영역 (safeContent)
			FullScreenFrame frame = uiRoot.CreateFullScreenFrame(uiRoot.ScreenLayer, "Achievement");
			container = frame.Root;
			safeContent = frame.Content;
			container.AddToClassList(USS_ROOT);
			container.AddToClassList(USS_CLASS);
			container.styleSheets.Add(Settings.Menu.Style);
			container.styleSheets.Add(Settings.Style);
			backdrop = new MenuBackdrop(container, Settings.Menu.BlurOutput);
			focus = new MenuFocus(container, NAV_CLASS, () => IsOpen, () => Settings.Menu.NavOverlapTolerance, () => Settings.Menu.NavAcrossWeight)
			{
				FocusFirst = FocusFirst,
				Scope = () => detail.IsShown ? detail.Window : stage,
			};
			pauseKey = new GameObject("AchievementPauseKey");
			pauseKey.transform.SetParent(transform, false);

			BuildStage();
			BuildToast();

			achievementManager.Changed += Refresh;
			EventBusBridge.Subscribe<AchievementUnlockedEvent>(OnUnlocked);
			inputManager.RegisterInputEvent(InputEventType.AchievementToggle, InputEventResponseType.Performed, Toggle);
			IsOpen = false;
		}

		private void OnDestroy()
		{
			// Start 전에 부서질 수 있음 (UIManager 가 AddComponent 한 같은 프레임). 그때는 구독도 화면도 아직 없음
			if (container == null)
			{
				return;
			}
			if (IsOpen)
			{
				backdrop.SetBlurred(false);
				if (TimeManager.TryGetExistingInstance(out TimeManager existingTimeManager))
				{
					existingTimeManager.Resume(pauseKey);
				}
			}
			achievementManager.Changed -= Refresh;
			EventBusBridge.Unsubscribe<AchievementUnlockedEvent>(OnUnlocked);
			if (InputManager.TryGetExistingInstance(out InputManager existingInputManager))
			{
				existingInputManager.UnregisterInputEvent(InputEventType.AchievementToggle, InputEventResponseType.Performed, Toggle);
			}
			Destroy(glowTexture);
			DestroyToast();
			container.RemoveFromHierarchy();
		}

		private void Update() => backdrop?.Repaint();

		private void BuildStage()
		{
			stage = new VisualElement();
			stage.AddToClassList("wm-fullmenu__stage");
			stage.AddToClassList("wm-ach__stage");
			safeContent.Add(stage);
			safeContent.RegisterCallback<GeometryChangedEvent>(_ => MenuStage.Fit(safeContent, stage, Settings.Menu.ReferenceHeight, Settings.Menu.MinimumWidth));

			VisualElement top = AchievementElements.Part(stage, "wm-fullmenu__top");
			top.pickingMode = PickingMode.Position;
			AchievementElements.Part(top, "wm-fullmenu__spacer");
			backButton = KeyHint.KeyButton("뒤로", "ESC", Back);
			backButton.AddToClassList("wm-fullmenu-close");
			focus.Register(backButton);
			top.Add(backButton);
			// 제목은 양옆 칸 너비와 무관하게 화면 가운데
			titleLabel = AchievementElements.Text(stage, TITLE_WALL, "wm-fullmenu__title");

			VisualElement body = AchievementElements.Part(stage, "wm-ach__body");
			body.pickingMode = PickingMode.Position;
			glowTexture = MenuStage.CreateGlowTexture(Settings.Menu.GlowTextureSize, "AchievementGlow");
			wallScreen = new AchievementWallScreen(Settings, focus, glowTexture, OpenDetail, OpenAlbum);
			body.Add(wallScreen);
			albumScreen = new AchievementAlbumScreen(Settings, focus, OpenDetail);
			body.Add(albumScreen);

			// 키 안내. 손가락 조작에서는 KeyHint 규칙으로 숨음
			bottom = AchievementElements.Part(stage, "wm-ach__bottom");
			VisualElement hints = KeyHint.KeyOnly(AchievementElements.Part(bottom, "wm-ach-hints"));
			AddHint(hints, "방향키", "고르기");
			AddHint(hints, "Z", "자세히, 고르기");
			AddHint(hints, "ESC", "뒤로");

			detail = new AchievementDetail(Settings, focus, glowTexture, ToggleWall, Back);
			detail.pickingMode = PickingMode.Position;
			stage.Add(detail);
		}

		private static void AddHint(VisualElement hints, string key, string text)
		{
			VisualElement hint = AchievementElements.Part(hints, "wm-ach-hint");
			AchievementElements.Text(hint, key, "wm-fullmenu-key wm-ach-hint__key");
			AchievementElements.Text(hint, text, "wm-ach-hint__text");
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
			if (IsOpen || achievementManager.Book == null)
			{
				return;
			}
			IsOpen = true;
			ShowPage(Page.Wall);
			detail.Hide();
			SetBackLayerVisible(true);
			backdrop.SetBlurred(UIEffects.Enabled);
			UiSfx.Play(UiSfxEvent.WindowOpen);
			UIMotion.Show(container, USS_ACTIVE);
			timeManager.Pause(pauseKey);
			// 손가락 조작에는 선택 칸이 없음. 누르지도 않은 칸이 강조돼 보이지 않게
			if (inputManager.IsTouchMode == false)
			{
				focus.FocusWhenReady();
			}
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
			detail.Hide();
			UiSfx.Play(UiSfxEvent.WindowClose);
			UIMotion.Hide(container, USS_ACTIVE);
			backdrop.SetBlurred(false);
			timeManager.Resume(pauseKey);
		}

		/// <summary>뒤로 한 단계: 상세 창, 도감, 메달 벽 순. 메달 벽에서는 닫힘. UIManager 의 취소 입력도 여기로</summary>
		public void Back()
		{
			if (IsOpen == false)
			{
				return;
			}
			if (detail.IsShown)
			{
				CloseDetail();
				return;
			}
			if (page == Page.Album)
			{
				UiSfx.Play(UiSfxEvent.WindowClose);
				ShowPage(Page.Wall);
				FocusLater(wallScreen.AlbumButton);
				return;
			}
			Close();
		}

		private void OpenAlbum()
		{
			UiSfx.Play(UiSfxEvent.Confirm);
			ShowPage(Page.Album);
			FocusLater(null);
		}

		private void OpenDetail(AchievementSO achievement)
		{
			UiSfx.Play(UiSfxEvent.WindowOpen);
			focusOnDetailClose = container.focusController?.focusedElement as VisualElement;
			detail.Show(achievement, achievementManager);
			// 상세 창은 자기 닫기와 안내가 있음. 뒤 화면의 뒤로 버튼과 안내는 숨겨 한 벌만
			SetBackLayerVisible(false);
			FocusLater(null);
		}

		private void CloseDetail()
		{
			AchievementSO shown = detail.Current;
			detail.Hide();
			SetBackLayerVisible(true);
			UiSfx.Play(UiSfxEvent.WindowClose);
			// 도감이 그사이 다시 그려졌으면 같은 업적의 새 카드로
			VisualElement target = focusOnDetailClose;
			if (target == null || target.panel == null)
			{
				target = page == Page.Album ? albumScreen.CardOf(shown) : null;
			}
			focusOnDetailClose = null;
			FocusLater(target);
		}

		private void SetBackLayerVisible(bool visible)
		{
			bottom.EnableInClassList(USS_HIDDEN, visible == false);
			backButton.EnableInClassList(USS_HIDDEN, visible == false);
		}

		private AchievementWallChange ToggleWall(AchievementSO achievement) => achievementManager.ToggleWall(achievement, Settings.WallSlotCount);

		private void ShowPage(Page next)
		{
			page = next;
			wallScreen.EnableInClassList(USS_HIDDEN, page != Page.Wall);
			albumScreen.EnableInClassList(USS_HIDDEN, page != Page.Album);
			titleLabel.text = page == Page.Wall ? TITLE_WALL : TITLE_ALBUM;
			Refresh();
		}

		private void Refresh()
		{
			if (IsOpen == false || achievementManager.Book == null)
			{
				return;
			}
			if (page == Page.Wall)
			{
				wallScreen.Bind(achievementManager);
			}
			else
			{
				albumScreen.Bind(achievementManager);
			}
			detail.Bind(achievementManager);

			// 다시 그리느라 고른 칸이 사라졌으면 처음 칸으로
			if (inputManager.IsTouchMode == false && container.focusController?.focusedElement is VisualElement focused && focused.panel == null)
			{
				focus.FocusWhenReady();
			}
		}

		// 화면을 바꾼 직후에는 배치 계산 전. 정한 칸이 있으면 그 칸, 없으면 그 화면의 처음 칸
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
			VisualElement target = detail.IsShown ? detail.FirstFocus : (page == Page.Album ? albumScreen.FirstFocus : wallScreen.FirstFocus);
			if (target == null || target.worldBound.width <= 0f)
			{
				return false;
			}
			target.Focus();
			return true;
		}
	}
}
