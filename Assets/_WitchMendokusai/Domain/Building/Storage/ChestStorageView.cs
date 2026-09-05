using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
	// 보관 상자 단일 공유 UI (UI Toolkit). 상자 클릭(ChestStorage.AnyOpenRequested) 시 WMWindow 를 열어
	// 그 상자의 인벤토리를 ItemGrid 에 바인딩 — InventoryView 패턴(prefab 불요, 코드 생성).
	// 플레이어 인벤토리 창과 나란히 띄워 아이템 이동 = 상점식 양방향.
	// UIManager.Start 가 글로벌 UIRoot 에 AddComponent + Inject (다른 씬 view 와 동형).
	public class ChestStorageView : MonoBehaviour
	{
		private const string WINDOW_ID = "ChestStorage";

		private WMWindow window;
		private ScrollView scroll;
		private ItemGrid grid;

		private UIRoot uiRoot;
		private WindowManager windowManager;

		[Inject]
		public void Construct(UIRoot uiRoot, WindowManager windowManager)
		{
			this.uiRoot = uiRoot;
			this.windowManager = windowManager;
		}

		private void OnEnable() => ChestStorage.AnyOpenRequested += Open;

		private void OnDisable()
		{
			ChestStorage.AnyOpenRequested -= Open;
			grid?.Unbind();
		}

		private void EnsureWindow()
		{
			if (window != null)
				return;

			window = new WMWindow
			{
				WindowId = WINDOW_ID,
				Title = "보관함"
			};
			window.style.left = 360;
			window.style.top = 80;
			uiRoot.WindowsLayer.Add(window);
			window.EnableSizeToggle();

			scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.AddToClassList("wm-inventory-scroll");
			window.Content.Add(scroll);

			grid = new ItemGrid();
			grid.AddToClassList("wm-inventory-grid");
			scroll.Add(grid);
		}

		private void Open(ChestStorage chest)
		{
			EnsureWindow();
			grid.Bind(chest.Inventory);

			// 보관함과 나란히 아이템을 옮기도록, 플레이어 인벤토리가 닫혀 있으면 같이 연다.
			// (상자 창이 위로 오게 인벤토리를 먼저 연다.)
			WMWindow inventoryWindow = windowManager.Find(InventoryView.WINDOW_ID);
			if (inventoryWindow != null && inventoryWindow.IsOpen == false)
				inventoryWindow.Open();

			window.Open();
		}
	}
}
