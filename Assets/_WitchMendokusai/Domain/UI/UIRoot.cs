using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

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

		private InputManager inputManager;
		private HoldingManager holdingManager;
		private IObjectResolver container;
		private CodexPreviewController codexPreviewController;
		private WindowManager windowManager;

		[Inject]
		public void Construct(InputManager inputManager, HoldingManager holdingManager, IObjectResolver container, CodexPreviewController codexPreviewController, WindowManager windowManager)
		{
			this.inputManager = inputManager;
			this.holdingManager = holdingManager;
			this.container = container;
			this.codexPreviewController = codexPreviewController;
			this.windowManager = windowManager;
			// VContainer: prefab 비활성화 후 Instantiate → Awake 는 SetActive(true) 이후 발화.
			// CreateViews 를 Construct 선두로 이동 — inactive GO 에서도 AddComponent 정상 작동.
			CreateViews();
			container.Inject(SettingView);
			container.Inject(KeybindHelpView);
			container.Inject(MagicBookView);
			container.Inject(WorldClockView);
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
		public KeybindHelpView KeybindHelpView { get; private set; }
		public MagicBookView MagicBookView { get; private set; }
		public WorldClockView WorldClockView { get; private set; }

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
			KeybindHelpView = gameObject.AddComponent<KeybindHelpView>();
			MagicBookView = gameObject.AddComponent<MagicBookView>();
			WorldClockView = gameObject.AddComponent<WorldClockView>();
		}

		private void OnEnable()
		{
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

			// TASK-WM-133 — panel-root 에 UI 서비스 1회 owner-push. UXML-cloned
			// VisualElement(CodexDetailPanel 등)가 static Instance reach 대신
			// 조상 walk 로 panel-scoped 획득 (global Singleton 결합 제거).
			// TooltipController 는 [Inject] Construct(.., UIRoot) 로 UIRoot 의존 →
			// eager build 도중 UIRoot prefab spawn → 본 OnEnable 발화. 여기서
			// 즉시 Resolve<TooltipController>() 호출 시 같은 Lazy 재진입 →
			// InvalidOperationException("ValueFactory attempted to access the
			// Value property"). factory lambda 로 첫 사용 시점까지 미뤄 cycle break.
			root.userData = new UIServices(codexPreviewController, windowManager, () => container.Resolve<TooltipController>());

			HoldingOverlay = new HoldingOverlay();
			OverlayLayer.Add(HoldingOverlay);
			holdingManager.RegisterOverlay(HoldingOverlay);
		}

		private void OnDisable()
		{
			if (holdingManager != null && HoldingOverlay != null)
				holdingManager.UnregisterOverlay(HoldingOverlay);
		}

		private void Update()
		{
			if (inputManager.IsMouseAvailable == false || HoldingOverlay == null || HoldingOverlay.panel == null)
				return;

			Vector2 screen = inputManager.MouseScreenPosition;
			screen.y = Screen.height - screen.y;
			Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(HoldingOverlay.panel, screen);
			holdingManager.OnPointerMove(panelPosition);
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
