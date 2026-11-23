using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using WitchMendokusai.Presentation;

namespace WitchMendokusai
{
	/// <summary>
	/// UI Toolkit 글로벌 panel 관리 — **층 순서가 곧 정책이다**(아래가 먼저, 뒤에 붙는 층이 위에 그려진다).
	///
	/// 1. `HudLayer` — 늘 떠 있는 본편 HUD. *가장 아래*다.
	/// 2. `ModeHudLayer` — 모드 전용 HUD(개척 핫바 등). 본편 HUD 를 통째 숨겨도 살아남아야 해서 한 단 위.
	/// 3. `WindowsLayer` — 사람이 *열어서* 보는 창(티메토 등). **핫바보다 위다.**
	/// 4. `ScreenLayer` — 전체화면 메뉴(설정 등).
	/// 5. `OverlayLayer` — 떠다니는 것(들고 있는 물건·말풍선·화면 전환).
	/// 6. `TooltipLayer` — 툴팁. **무조건 최상단** — 무엇 위에 얹히든 가려지면 안 된다.
	///
	/// ★ 왜 순서를 글로 박았나: 예전엔 층은 있는데 *정책이 없어서*, 무엇이 위인지가 「누가 먼저
	///   Add 했나」로 정해졌다. 그래서 핫바가 티메토 창을 덮고 툴팁이 핫바 뒤로 갔다(사용자 실증).
	///   층에 뜻을 주면 「어디에 붙일까」가 판단이 아니라 조회가 된다.
	/// </summary>
	[DefaultExecutionOrder(-50)]
	[RequireComponent(typeof(UIDocument))]
	public class UIRoot : MonoBehaviour
	{
		public static UIRoot Instance { get; private set; }

		public static bool TryGetExistingInstance(out UIRoot mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		[SerializeField] private StyleSheet styleSheet;
		[SerializeField, Tooltip("폰 조작 버튼 그림")] private MobileControlIcons mobileControlIcons;
		[SerializeField, Tooltip("상호작용 표시 모양")] private StyleSheet interactPromptStyle;
		[SerializeField, Tooltip("전체 메뉴 (ESC) 모양과 수치")] private FullMenuSettings fullMenuSettings;
		[field: SerializeField, Tooltip("업적 전체 화면 모양과 수치. 업적 화면은 씬 view 라 UIManager 가 넘겨 줌")] public AchievementScreenSettings AchievementSettings { get; private set; }
		[SerializeField, Tooltip("마도서 하늘 화면 모양과 수치")] private MagicBookSettings magicBookSettings;
		[field: SerializeField] public WitchMendokusai.Presentation.DamageTextSkin DamageTextSkin { get; private set; }
		[field: SerializeField] public CreditsInfo Credits { get; private set; }

		private InputManager inputManager;
		private HoldingManager holdingManager;
		private IObjectResolver container;
		private DiscoveryPreviewController discoveryPreviewController;
		private WindowManager windowManager;

		[Inject]
		public void Construct(InputManager inputManager, HoldingManager holdingManager, IObjectResolver container, DiscoveryPreviewController discoveryPreviewController, WindowManager windowManager)
		{
			this.inputManager = inputManager;
			this.holdingManager = holdingManager;
			this.container = container;
			this.discoveryPreviewController = discoveryPreviewController;
			this.windowManager = windowManager;
			// VContainer: prefab 비활성화 후 Instantiate → Awake 는 SetActive(true) 이후 발화.
			// CreateViews 를 Construct 선두로 이동 — inactive GO 에서도 AddComponent 정상 작동.
			CreateViews();
			container.Inject(SettingView);
			container.Inject(KeybindHelpView);
			container.Inject(MagicBookView);
			container.Inject(WorldClockView);
			container.Inject(FullMenuView);
		}

		public UIDocument Document { get; private set; }
		public VisualElement Root => Document.rootVisualElement;
		public VisualElement WindowsLayer { get; private set; }
		public VisualElement ScreenLayer { get; private set; }
		public VisualElement HudLayer { get; private set; }

		/// <summary> 모드 전용 HUD(개척 핫바 등) — 본편 HUD 위, 사람이 여는 창 아래. </summary>
		public VisualElement ModeHudLayer { get; private set; }

		public VisualElement OverlayLayer { get; private set; }

		/// <summary> 툴팁 전용 최상단 층 — 여기 붙은 것은 무엇에도 안 가려진다. </summary>
		public VisualElement TooltipLayer { get; private set; }
		public HoldingOverlay HoldingOverlay { get; private set; }
		public SettingView SettingView { get; private set; }
		public SaveNoticeView SaveNoticeView { get; private set; }
		public KeybindHelpView KeybindHelpView { get; private set; }
		public MagicBookView MagicBookView { get; private set; }
		public WorldClockView WorldClockView { get; private set; }
		public FullMenuView FullMenuView { get; private set; }

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
			Document = GetComponent<UIDocument>();
		}

		private void OnDestroy()
		{
			if (Instance == this)
				Instance = null;
		}

		/// <summary>
		/// 글로벌 View 컴포넌트를 동적으로 생성. 씬 무관 시스템 메뉴.
		/// 씬별 view (Inventory/Hotbar/BuildingBar 등) 는 해당 씬 매니저 (UIManager 등) 가 직접 AddComponent / OnDestroy 정리.
		/// </summary>
		private void CreateViews()
		{
			SettingView = gameObject.AddComponent<SettingView>();
			SaveNoticeView = gameObject.AddComponent<SaveNoticeView>();
			KeybindHelpView = gameObject.AddComponent<KeybindHelpView>();
			MagicBookView = gameObject.AddComponent<MagicBookView>();
			MagicBookView.Settings = magicBookSettings;
			WorldClockView = gameObject.AddComponent<WorldClockView>();
			// 전체 메뉴 (ESC, 폰 뒤로가기). 설정창은 이 메뉴 안의 버튼
			FullMenuView = gameObject.AddComponent<FullMenuView>();
			FullMenuView.Settings = fullMenuSettings;
			FullMenuView.Icons = mobileControlIcons;
			// TASK-WM-200 — 화면 조작(손가락). 씬과 무관하게 늘 있어야 한다: 손가락 기기에서
			// 조작 장치가 씬마다 있다 없다 하면 「어떤 화면에선 못 움직인다」가 된다.
			// 마우스를 쓰는 동안은 스스로 숨는다.
			// ★ 이 줄이 한 번 사라진 적이 있다(넓게 훑는 커밋이 옛 사본을 얹음) — 없으면 조작 장치를
			//   아무도 안 만들어서 **폰에서 화면만 뜨고 못 움직인다**. 지우지 말 것.
			gameObject.AddComponent<MobileControlsView>().Icons = mobileControlIcons;
			// 가까운 NPC 옆 상호작용 표시 (옛 UGUI UIInteractPopup 대체, 2026-11-17)
			gameObject.AddComponent<InteractPromptView>().Style = interactPromptStyle;
		}

		private void OnEnable()
		{
			UIToolkitFocus.Register(Document);
			VisualElement root = Document.rootVisualElement;

			if (styleSheet != null && root.styleSheets.Contains(styleSheet) == false)
				root.styleSheets.Add(styleSheet);

			HudLayer = MakeLayer("HudLayer", PickingMode.Ignore);
			ModeHudLayer = MakeLayer("ModeHudLayer", PickingMode.Ignore);
			WindowsLayer = MakeLayer("WindowsLayer", PickingMode.Ignore);
			ScreenLayer = MakeLayer("ScreenLayer", PickingMode.Ignore);
			OverlayLayer = MakeLayer("OverlayLayer", PickingMode.Ignore);
			TooltipLayer = MakeLayer("TooltipLayer", PickingMode.Ignore);

			// ★ 붙이는 순서가 곧 층 순서다 — 위 § 문서의 1~6 과 *반드시* 같아야 한다.
			//   여기 한 줄을 옮기면 화면 전체의 위아래가 바뀐다.
			root.Add(HudLayer);
			root.Add(ModeHudLayer);
			root.Add(WindowsLayer);
			root.Add(ScreenLayer);
			root.Add(OverlayLayer);
			root.Add(TooltipLayer);

			// 안전 영역: 화면 가장자리에 붙는 층만. Overlay, Tooltip 은 포인터 좌표를 따라가서 화면 전체 기준 유지
			safeArea = SafeArea.UNSET;
			ApplySafeArea();
			WindowsLayer.RegisterCallback<GeometryChangedEvent>(_ => ApplyFullBleed());
			ScreenLayer.RegisterCallback<GeometryChangedEvent>(_ => ApplyFullBleed());

			// TASK-WM-133 — panel-root 에 UI 서비스 1회 owner-push. UXML-cloned
			// VisualElement(DiscoveryDetailPanel 등)가 static Instance reach 대신
			// 조상 walk 로 panel-scoped 획득 (global Singleton 결합 제거).
			// TooltipController 는 [Inject] Construct(.., UIRoot) 로 UIRoot 의존 →
			// eager build 도중 UIRoot prefab spawn → 본 OnEnable 발화. 여기서
			// 즉시 Resolve<TooltipController>() 호출 시 같은 Lazy 재진입 →
			// InvalidOperationException("ValueFactory attempted to access the
			// Value property"). factory lambda 로 첫 사용 시점까지 미뤄 cycle break.
			root.userData = new UIServices(discoveryPreviewController, windowManager, () => container.Resolve<TooltipController>());

			HoldingOverlay = new HoldingOverlay();
			OverlayLayer.Add(HoldingOverlay);
			holdingManager.RegisterOverlay(HoldingOverlay);
			windowManager.WindowClosed += holdingManager.ReturnHeld;
		}

		private void OnDisable()
		{
			UIToolkitFocus.Unregister(Document);
			if (windowManager != null && holdingManager != null)
				windowManager.WindowClosed -= holdingManager.ReturnHeld;
			if (holdingManager != null && HoldingOverlay != null)
				holdingManager.UnregisterOverlay(HoldingOverlay);
		}

		private void Update()
		{
			ApplySafeArea();
			// 키 안내와 손가락 안내 전환. 규칙은 KeyHint 와 Slot.uss
			Document.rootVisualElement.EnableInClassList(KeyHint.TOUCH_ROOT_CLASS, inputManager.IsTouchMode);

			if (inputManager.IsMouseAvailable == false || HoldingOverlay == null || HoldingOverlay.panel == null)
				return;

			Vector2 screen = inputManager.MouseScreenPosition;
			screen.y = Screen.height - screen.y;
			Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(HoldingOverlay.panel, screen);
			holdingManager.OnPointerMove(panelPosition);
		}

		private Rect safeArea = SafeArea.UNSET;

		private void ApplySafeArea()
		{
			if (HudLayer == null || SafeArea.Refresh(ref safeArea) == false)
				return;
			foreach (VisualElement layer in new[] { HudLayer, ModeHudLayer, WindowsLayer, ScreenLayer })
				SafeArea.ApplyInsets(layer, safeArea);
			ApplyFullBleed();
		}

		private readonly List<VisualElement> fullBleedElements = new();
		private readonly Dictionary<VisualElement, VisualElement> frameContents = new();

		/// <summary>
		/// 전체 화면 창의 유일한 만드는 길. 창마다 따로 넓히면 창마다 같은 띠가 다시 생김 (2026-11-17 폰 실기, 설정, 입장창)
		/// - Root: 화면 끝까지. 배경, 어두운 막, 배경 그림, 테마 USS 클래스
		/// - Content: 안전 영역. 버튼, 목록, 글처럼 손가락이 닿거나 읽어야 하는 것
		/// 층 (ScreenLayer, WindowsLayer) 은 안전 영역만큼 줄어 있음. Root 는 그 밖으로 넓히고 Content 는 다시 안으로
		/// </summary>
		public FullScreenFrame CreateFullScreenFrame(VisualElement layer, string name)
		{
			VisualElement root = new() { name = name };
			root.style.position = Position.Absolute;
			root.style.left = 0;
			root.style.right = 0;
			root.style.top = 0;
			root.style.bottom = 0;

			VisualElement content = new() { name = name + "Content" };
			content.style.position = Position.Absolute;
			content.style.left = 0;
			content.style.right = 0;
			content.style.top = 0;
			content.style.bottom = 0;
			content.pickingMode = PickingMode.Ignore;
			root.Add(content);

			layer.Add(root);
			frameContents[root] = content;
			RegisterFullBleed(root);
			return new FullScreenFrame(root, content);
		}

		/// <summary>
		/// 이미 만든 요소 하나를 화면 끝까지. 안의 내용 배치는 바꾸지 않으므로 가운데 배치 내용이나 배경 전용 요소만
		/// 손가락 닿는 요소가 가장자리에 붙으면 CreateFullScreenFrame
		/// </summary>
		public void RegisterFullBleed(VisualElement element)
		{
			RegisterFullBleed(element, false);
		}

		private readonly HashSet<VisualElement> horizontalOnlyBleed = new();

		/// <summary> horizontalOnly: 좌우만 넓힘. 높이가 정해진 위아래 띠처럼 top, bottom 을 건드리면 안 되는 요소 </summary>
		public void RegisterFullBleed(VisualElement element, bool horizontalOnly)
		{
			if (horizontalOnly)
				horizontalOnlyBleed.Add(element);
			if (fullBleedElements.Contains(element))
				return;
			fullBleedElements.Add(element);
			element.RegisterCallback<AttachToPanelEvent>(_ => ApplyFullBleed(element));
			ApplyFullBleed(element);
		}

		private void ApplyFullBleed()
		{
			fullBleedElements.RemoveAll(element => element == null);
			foreach (VisualElement element in fullBleedElements)
				ApplyFullBleed(element);
		}

		private void ApplyFullBleed(VisualElement element)
		{
			VisualElement layer = element.parent;
			if (layer == null || safeArea == SafeArea.UNSET)
				return;
			float width = layer.resolvedStyle.width;
			float height = layer.resolvedStyle.height;
			// 층 크기 확정 전이면 층의 GeometryChanged 에서 다시
			if (float.IsNaN(width) || float.IsNaN(height) || width <= 0f || height <= 0f)
				return;
			Vector4 bleed = SafeArea.BleedOffsets(safeArea, new Vector2(width, height));
			element.style.left = -bleed.x;
			element.style.right = -bleed.y;
			if (horizontalOnlyBleed.Contains(element) == false)
			{
				element.style.top = -bleed.z;
				element.style.bottom = -bleed.w;
			}
			if (frameContents.TryGetValue(element, out VisualElement content))
			{
				content.style.left = bleed.x;
				content.style.right = bleed.y;
				content.style.top = bleed.z;
				content.style.bottom = bleed.w;
			}
		}

		private static VisualElement MakeLayer(string name, PickingMode pickingMode)
		{
			VisualElement layer = new() { name = name };
			layer.style.position = Position.Absolute;
			layer.style.left = 0;
			layer.style.top = 0;
			layer.style.right = 0;
			layer.style.bottom = 0;
			layer.pickingMode = pickingMode;
			return layer;
		}
	}
}
