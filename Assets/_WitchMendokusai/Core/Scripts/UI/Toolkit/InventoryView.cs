using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	[RequireComponent(typeof(UIDocument))]
	public class InventoryView : MonoBehaviour
	{
		// uGUI 시절 "Inventory" anchoredPosition 데이터와 충돌 회피용 임시 ID. 마이그레이션 끝나면 "Inventory"로 통일.
		private const string WINDOW_ID = "InventoryToolkit";

		[SerializeField] private Inventory inventory;
		[SerializeField] private StyleSheet styleSheet;

		private UIDocument document;
		private VisualElement windowsLayer;
		private VisualElement overlayLayer;
		private WMWindow window;
		private ItemGrid grid;
		private HoldingOverlay holdingOverlay;

		private void OnEnable()
		{
			document = GetComponent<UIDocument>();
			VisualElement root = document.rootVisualElement;

			if (styleSheet != null && root.styleSheets.Contains(styleSheet) == false)
				root.styleSheets.Add(styleSheet);

			// Layer 분리: windows(BringToFront 영향 받음) ↔ overlay(항상 위)
			windowsLayer = new VisualElement { name = "WindowsLayer" };
			windowsLayer.style.position = Position.Absolute;
			windowsLayer.style.left = 0;
			windowsLayer.style.top = 0;
			windowsLayer.style.right = 0;
			windowsLayer.style.bottom = 0;
			windowsLayer.pickingMode = PickingMode.Ignore;
			root.Add(windowsLayer);

			overlayLayer = new VisualElement { name = "OverlayLayer" };
			overlayLayer.style.position = Position.Absolute;
			overlayLayer.style.left = 0;
			overlayLayer.style.top = 0;
			overlayLayer.style.right = 0;
			overlayLayer.style.bottom = 0;
			overlayLayer.pickingMode = PickingMode.Ignore;
			root.Add(overlayLayer);

			window = new WMWindow
			{
				WindowId = WINDOW_ID,
				Title = "인벤토리"
			};
			window.style.left = 80;
			window.style.top = 80;
			windowsLayer.Add(window);

			grid = new ItemGrid();
			window.Content.Add(grid);

			Inventory bound = inventory != null ? inventory : SOManager.Instance.ItemInventory;
			grid.Bind(bound);

			holdingOverlay = new HoldingOverlay();
			overlayLayer.Add(holdingOverlay);
			HoldingManager.Instance.RegisterOverlay(holdingOverlay);

			InputManager.Instance.RegisterInputEvent(InputEventType.Inventory, InputEventResponseType.Performed, OnToggle);
		}

		private void OnDisable()
		{
			grid?.Unbind();

			if (HoldingManager.TryGetExistingInstance(out HoldingManager holdingManager) && holdingOverlay != null)
				holdingManager.UnregisterOverlay(holdingOverlay);

			if (InputManager.TryGetExistingInstance(out InputManager inputManager))
				inputManager.UnregisterInputEvent(InputEventType.Inventory, InputEventResponseType.Performed, OnToggle);
		}

		private void Update()
		{
			if (Mouse.current == null || holdingOverlay == null || holdingOverlay.panel == null)
				return;

			Vector2 screen = Mouse.current.position.ReadValue();
			// Mouse.current.position은 좌하단(0,0) y-up, UI Toolkit panel은 좌상단(0,0) y-down
			screen.y = Screen.height - screen.y;
			Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(holdingOverlay.panel, screen);
			HoldingManager.Instance.OnPointerMove(panelPosition);
		}

		private void OnToggle() => window?.Toggle();
	}
}
