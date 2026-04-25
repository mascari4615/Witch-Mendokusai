using UnityEngine;

namespace WitchMendokusai
{
	public class InventoryView : MonoBehaviour
	{
		// uGUI 시절 "Inventory" anchoredPosition 데이터와 충돌 회피용 임시 ID. 마이그레이션 끝나면 "Inventory"로 통일.
		private const string WINDOW_ID = "InventoryToolkit";

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
