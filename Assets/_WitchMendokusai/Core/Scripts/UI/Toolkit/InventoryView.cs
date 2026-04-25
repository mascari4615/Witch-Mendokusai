using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	[RequireComponent(typeof(UIDocument))]
	public class InventoryView : MonoBehaviour
	{
		[SerializeField] private Inventory inventory;
		[SerializeField] private StyleSheet styleSheet;

		private UIDocument document;
		private ItemGrid grid;
		private HoldingOverlay holdingOverlay;

		private void OnEnable()
		{
			document = GetComponent<UIDocument>();
			VisualElement root = document.rootVisualElement;

			if (styleSheet != null && root.styleSheets.Contains(styleSheet) == false)
				root.styleSheets.Add(styleSheet);

			grid = root.Q<ItemGrid>();
			if (grid == null)
			{
				grid = new ItemGrid();
				root.Add(grid);
			}

			Inventory bound = inventory != null ? inventory : SOManager.Instance.ItemInventory;
			grid.Bind(bound);

			holdingOverlay = new HoldingOverlay();
			root.Add(holdingOverlay);
			HoldingManager.Instance.RegisterOverlay(holdingOverlay);

			root.RegisterCallback<PointerMoveEvent>(OnRootPointerMove);
		}

		private void OnDisable()
		{
			if (document != null && document.rootVisualElement != null)
				document.rootVisualElement.UnregisterCallback<PointerMoveEvent>(OnRootPointerMove);

			grid?.Unbind();

			if (HoldingManager.TryGetExistingInstance(out HoldingManager manager) && holdingOverlay != null)
				manager.UnregisterOverlay(holdingOverlay);
		}

		private void OnRootPointerMove(PointerMoveEvent evt)
		{
			HoldingManager.Instance.OnPointerMove(evt.position);
		}
	}
}
