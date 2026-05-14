using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
	public class InventoryView : MonoBehaviour
	{
		private const string WINDOW_ID = "Inventory";

		[SerializeField] private Inventory inventory;

		private WMWindow window;
		private FilterBar filterBar;
		private ScrollView scroll;
		private ItemGrid grid;

		private UIRoot uiRoot;
		private SOManager soManager;
		private InputManager inputManager;

		[Inject]
		public void Construct(UIRoot uiRoot, SOManager soManager, InputManager inputManager)
		{
			this.uiRoot = uiRoot;
			this.soManager = soManager;
			this.inputManager = inputManager;
		}

		private void Start()
		{
			window = new WMWindow
			{
				WindowId = WINDOW_ID,
				Title = "인벤토리"
			};
			window.style.left = 80;
			window.style.top = 80;
			uiRoot.WindowsLayer.Add(window);
			window.EnableSizeToggle();

			filterBar = new FilterBar();
			filterBar.OnFilterChanged += OnFilterChanged;
			window.Content.Add(filterBar);

			scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.AddToClassList("wm-inventory-scroll");
			window.Content.Add(scroll);

			grid = new ItemGrid();
			grid.AddToClassList("wm-inventory-grid");
			scroll.Add(grid);

			Inventory bound = inventory != null ? inventory : soManager.ItemInventory;
			grid.Bind(bound);

			inputManager.RegisterInputEvent(InputEventType.Inventory, InputEventResponseType.Performed, OnToggle);
		}

		private void OnFilterChanged(ItemType type) => grid?.SetFilter(type);

		private void OnDestroy()
		{
			grid?.Unbind();

			if (inputManager != null)
				inputManager.UnregisterInputEvent(InputEventType.Inventory, InputEventResponseType.Performed, OnToggle);
		}

		private void OnToggle() => window?.Toggle();
	}
}
