using UnityEngine;

namespace WitchMendokusai
{
	public class InventoryView : MonoBehaviour
	{
		private const string WINDOW_ID = "Inventory";

		[SerializeField] private Inventory inventory;

		private WMWindow window;
		private FilterBar filterBar;
		private ItemGrid grid;

		private void Start()
		{
			window = new WMWindow
			{
				WindowId = WINDOW_ID,
				Title = "인벤토리"
			};
			window.style.left = 80;
			window.style.top = 80;
			UIRoot.Instance.WindowsLayer.Add(window);

			filterBar = new FilterBar();
			filterBar.OnFilterChanged += OnFilterChanged;
			window.Content.Add(filterBar);

			grid = new ItemGrid();
			window.Content.Add(grid);

			Inventory bound = inventory != null ? inventory : SOManager.Instance.ItemInventory;
			grid.Bind(bound);

			InputManager.Instance.RegisterInputEvent(InputEventType.Inventory, InputEventResponseType.Performed, OnToggle);
		}

		private void OnFilterChanged(ItemType type) => grid?.SetFilter(type);

		private void OnDestroy()
		{
			grid?.Unbind();

			if (InputManager.TryGetExistingInstance(out InputManager inputManager))
				inputManager.UnregisterInputEvent(InputEventType.Inventory, InputEventResponseType.Performed, OnToggle);
		}

		private void OnToggle() => window?.Toggle();
	}
}
