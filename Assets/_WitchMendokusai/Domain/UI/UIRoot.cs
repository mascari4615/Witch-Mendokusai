using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// UI Toolkit 글로벌 panel 관리. 단일 UIDocument에 layer 분리:
	/// - WindowsLayer: WMWindow들 (BringToFront 영향 받음)
	/// - ScreenLayer: 전체화면 메뉴 패널 (SettingView 등)
	/// - HudLayer: 항상 보이는 HUD (핫바 등)
	/// - OverlayLayer: 최상단 floating (HoldingOverlay)
	/// 각 view 컴포넌트(InventoryView, HotbarView 등)가 적절한 layer에 자기 element 추가.
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

		[Inject]
		public void Construct(InputManager inputManager, HoldingManager holdingManager, IObjectResolver container)
		{
			this.inputManager = inputManager;
			this.holdingManager = holdingManager;
			this.container = container;
			container.Inject(SettingView);
			container.Inject(KeybindHelpView);
		}

		public UIDocument Document { get; private set; }
		public VisualElement Root => Document.rootVisualElement;
		public VisualElement WindowsLayer { get; private set; }
		public VisualElement ScreenLayer { get; private set; }
		public VisualElement HudLayer { get; private set; }
		public VisualElement OverlayLayer { get; private set; }
		public HoldingOverlay HoldingOverlay { get; private set; }
		public SettingView SettingView { get; private set; }
		public KeybindHelpView KeybindHelpView { get; private set; }
		public MagicBookView MagicBookView { get; private set; }

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
			Document = GetComponent<UIDocument>();
			CreateViews();
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
		}

		private void OnEnable()
		{
			VisualElement root = Document.rootVisualElement;

			if (styleSheet != null && root.styleSheets.Contains(styleSheet) == false)
				root.styleSheets.Add(styleSheet);

			WindowsLayer = MakeLayer("WindowsLayer", PickingMode.Ignore);
			ScreenLayer = MakeLayer("ScreenLayer", PickingMode.Ignore);
			HudLayer = MakeLayer("HudLayer", PickingMode.Ignore);
			OverlayLayer = MakeLayer("OverlayLayer", PickingMode.Ignore);

			root.Add(WindowsLayer);
			root.Add(ScreenLayer);
			root.Add(HudLayer);
			root.Add(OverlayLayer);

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
			if (Mouse.current == null || HoldingOverlay == null || HoldingOverlay.panel == null)
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
