using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	// 상점 패널. 장바구니 판정은 DomainSDK ShopCart
	public partial class UIShopToolkit : UIToolkitPanel, IUIPanelInputOwner
	{
		private sealed class ShopTab
		{
			public string Name;
			public bool IsSell;
			public List<ItemData> Items;
		}

		private sealed class InventoryBag : IShopBag
		{
			private readonly Inventory inventory;

			public InventoryBag(Inventory inventory) => this.inventory = inventory;

			public int Consume(int itemId, int amount) => inventory.Consume(itemId, amount);
			public int Add(int itemId, int amount) => inventory.Add(GetItemData(itemId), amount);
		}

		[Inject] internal SOManager soManager;
		[Inject] internal DataManager dataManager;
		[Inject] internal InputManager inputManager;

		public override string Name => settings.PanelName;
		public override Sprite PanelIcon => settings.PanelIcon;
		public override bool IsFullscreen => true;

		private ShopUISettings settings;
		private ShopView view;
		private ShopCart cart;
		private NPC npc;

		private readonly List<ShopTab> shopTabs = new();
		private int tabIndex;
		private int selectedIndex;
		private bool isCartFocused;
		private int cartIndex;

		public void SetSettings(ShopUISettings newSettings) => settings = newSettings;

		public bool OwnsTabInput => true;

		private ShopTab CurrentTab => shopTabs[Mathf.Clamp(tabIndex, 0, shopTabs.Count - 1)];
		private ItemData SelectedItem =>
			selectedIndex >= 0 && selectedIndex < CurrentTab.Items.Count ? CurrentTab.Items[selectedIndex] : null;
		private ShopCartSide CurrentSide => CurrentTab.IsSell ? ShopCartSide.Sell : ShopCartSide.Buy;
		private bool IsPayFocused => isCartFocused && cartIndex >= cart.Lines.Count;
		private int Wallet
		{
			get => dataManager.GameStat[GameStatType.NYANG];
			set => dataManager.GameStat[GameStatType.NYANG] = value;
		}

		protected override void BuildUI(VisualElement root)
		{
			cart = new ShopCart(settings.CartLineLimit);
			view = new ShopView(settings);
			root.Add(view);

			view.RegisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
			view.RegisterCallback<NavigationSubmitEvent>(OnNavigationSubmit, TrickleDown.TrickleDown);
			view.RegisterCallback<NavigationCancelEvent>(StopEvent, TrickleDown.TrickleDown);
			view.RegisterCallback<PointerUpEvent>(_ => FocusView());
			view.schedule.Execute(RestoreLostFocus).Every(settings.FocusGuardMilliseconds);

			view.CellHovered += OnCellHovered;
			view.CellClicked += OnCellClicked;
			view.TabClicked += OnTabClicked;
			view.CartRowHovered += OnCartRowHovered;
			view.CartRowClicked += OnCartRowClicked;
			view.PayHovered += OnPayHovered;
			view.PayClicked += Pay;
		}

		public override void SetNPC(NPCObject npcObject)
		{
			npc = npcObject.Data;
			tabIndex = 0;
			selectedIndex = 0;
			isCartFocused = false;
			cartIndex = 0;
			cart.Clear();
		}

		protected override void OnOpen()
		{
			inputManager.RegisterInputEvent(InputEventType.CameraControlModeToggle, InputEventResponseType.Performed, OnTabInput);
			FocusView();
		}

		protected override void OnClose()
		{
			inputManager.UnregisterInputEvent(InputEventType.CameraControlModeToggle, InputEventResponseType.Performed, OnTabInput);
			cart?.Clear();
			Root?.panel?.focusController?.focusedElement?.Blur();
		}

		private void OnDestroy()
		{
			if (inputManager != null)
				inputManager.UnregisterInputEvent(InputEventType.CameraControlModeToggle, InputEventResponseType.Performed, OnTabInput);
		}

		public override void UpdateUI() => RefreshAll(jumpCursor: true);

		private void FocusView() => view.schedule.Execute(() => view.Focus());

		// 다른 창이 고른 칸을 놓으며 포커스가 빈 경우 복구
		private void RestoreLostFocus()
		{
			if (view.panel != null && view.resolvedStyle.display == DisplayStyle.Flex && Root.resolvedStyle.display == DisplayStyle.Flex && view.focusController.focusedElement == null)
				view.Focus();
		}

		private static void StopEvent(NavigationCancelEvent evt) => evt.StopPropagation();

		private void OnNavigationMove(NavigationMoveEvent evt)
		{
			evt.StopPropagation();
			view.focusController?.IgnoreEvent(evt);
			switch (evt.direction)
			{
				case NavigationMoveEvent.Direction.Up:
					MoveVertical(-1);
					break;
				case NavigationMoveEvent.Direction.Down:
					MoveVertical(1);
					break;
				case NavigationMoveEvent.Direction.Left:
					MoveHorizontal(-1);
					break;
				case NavigationMoveEvent.Direction.Right:
					MoveHorizontal(1);
					break;
			}
		}

		private void OnNavigationSubmit(NavigationSubmitEvent evt)
		{
			evt.StopPropagation();
			Submit();
		}

		private void OnTabInput()
		{
			int direction = inputManager.IsShiftHeld ? -1 : 1;
			SetTab(tabIndex + direction);
		}

		// X와 Esc 공용. 장바구니가 비었을 때만 닫기 허용
		public bool HandleCancelInput()
		{
			if (isCartFocused)
			{
				if (cartIndex < cart.Lines.Count)
				{
					ShopCartLine line = cart.Lines[cartIndex];
					cart.RemoveOne(line.Goods.ItemId, line.Side);
					cartIndex = Mathf.Min(cartIndex, cart.Lines.Count);
					UiSfx.Play(UiSfxEvent.Cancel);
					RefreshAll(jumpCursor: false);
					return true;
				}

				isCartFocused = false;
				UiSfx.Play(UiSfxEvent.Cancel);
				RefreshSelection();
				return true;
			}

			ItemData item = SelectedItem;
			if (item != null && cart.RemoveOne(item.ID, CurrentSide))
			{
				UiSfx.Play(UiSfxEvent.Cancel);
				RefreshAll(jumpCursor: false);
				return true;
			}

			if (cart.IsEmpty)
			{
				UiSfx.Play(UiSfxEvent.Cancel);
				return false;
			}

			UiSfx.Play(UiSfxEvent.PurchaseFail);
			view.Pop("뺄 물건이 없습니다.", warning: true);
			return true;
		}

		private void MoveVertical(int direction)
		{
			if (isCartFocused)
			{
				int nextIndex = Mathf.Clamp(cartIndex + direction, 0, cart.Lines.Count);
				if (nextIndex != cartIndex)
				{
					cartIndex = nextIndex;
					UiSfx.Play(UiSfxEvent.FocusMove);
					RefreshSelection();
				}
				return;
			}

			int count = CurrentTab.Items.Count;
			int columns = settings.GridColumns;
			int next = selectedIndex + direction * columns;
			if (next < 0)
				return;
			if (next >= count)
			{
				bool hasLowerRow = Mathf.CeilToInt((float)count / columns) - 1 > selectedIndex / columns;
				if (hasLowerRow == false)
					return;
				next = count - 1;
			}
			SelectCell(next);
		}

		private void MoveHorizontal(int direction)
		{
			if (isCartFocused)
			{
				if (direction < 0)
				{
					isCartFocused = false;
					UiSfx.Play(UiSfxEvent.FocusMove);
					RefreshSelection();
				}
				return;
			}

			int count = CurrentTab.Items.Count;
			if (direction > 0 && (selectedIndex % settings.GridColumns == settings.GridColumns - 1 || selectedIndex >= count - 1))
			{
				isCartFocused = true;
				cartIndex = cart.Lines.Count;
				UiSfx.Play(UiSfxEvent.FocusMove);
				RefreshSelection();
				return;
			}

			int next = selectedIndex + direction;
			if (next < 0)
				return;
			SelectCell(Mathf.Min(count - 1, next));
		}

		private void SelectCell(int index)
		{
			if (index == selectedIndex || index < 0)
				return;
			selectedIndex = index;
			UiSfx.Play(UiSfxEvent.FocusMove);
			RefreshSelection();
		}

		private void Submit()
		{
			if (isCartFocused == false)
			{
				AddSelected();
				return;
			}

			if (IsPayFocused)
			{
				Pay();
				return;
			}

			ShopCartLine line = cart.Lines[cartIndex];
			AddGoods(ItemFromLine(line), line.Side, cartWording: true);
		}

		private static ItemData ItemFromLine(ShopCartLine line) => GetItemData(line.Goods.ItemId);

		private void AddSelected()
		{
			ItemData item = SelectedItem;
			if (item != null)
				AddGoods(item, CurrentSide, cartWording: false);
		}

		private void AddGoods(ItemData item, ShopCartSide side, bool cartWording)
		{
			ShopCartAddResult result = cart.AddOne(GoodsOf(item), side, Wallet);
			if (result == ShopCartAddResult.Added)
			{
				UiSfx.Play(UiSfxEvent.Confirm);
				RefreshAll(jumpCursor: false);
				return;
			}

			UiSfx.Play(UiSfxEvent.PurchaseFail);
			view.Pop(AddFailureText(result, item, cartWording), warning: true);
		}

		private static string AddFailureText(ShopCartAddResult result, ItemData item, bool cartWording)
		{
			if (cartWording)
				return "더 담을 수 없습니다.";
			switch (result)
			{
				case ShopCartAddResult.CannotAfford:
					return "냥이 부족합니다.";
				case ShopCartAddResult.SellLimitReached:
					return "가방 수량까지만 담습니다.";
				default:
					return item.IsCountable ? "더 담을 수 없습니다." : "1개까지 담습니다.";
			}
		}

		private void Pay()
		{
			if (cart.IsEmpty)
				return;

			if (cart.CanCheckout(Wallet) == false)
			{
				UiSfx.Play(UiSfxEvent.PurchaseFail);
				view.Pop("냥이 부족합니다.", warning: true);
				return;
			}

			ShopCheckoutResult result = cart.Checkout(Wallet, new InventoryBag(soManager.ItemInventory));
			Wallet = (int)result.Wallet;
			UiSfx.Play(UiSfxEvent.Purchase);
			isCartFocused = false;
			cartIndex = 0;
			RefreshAll(jumpCursor: false);

			if (result.UnplacedAmount > 0)
				view.Pop("가방이 가득 찼습니다.", warning: true);
			else
				view.Pop(result.Net >= 0 ? $"+ {result.Net:N0}" : $"- {-result.Net:N0}", warning: false);
		}

		private void SetTab(int index)
		{
			int count = shopTabs.Count;
			if (count == 0)
				return;
			tabIndex = (index + count) % count;
			selectedIndex = 0;
			isCartFocused = false;
			UiSfx.Play(UiSfxEvent.Tab);
			RefreshAll(jumpCursor: true);
		}

		private void OnTabClicked(int index) => SetTab(index);

		private void OnCellHovered(int index)
		{
			if (index == selectedIndex && isCartFocused == false)
				return;
			isCartFocused = false;
			selectedIndex = index;
			RefreshSelection();
		}

		private void OnCellClicked(int index)
		{
			isCartFocused = false;
			selectedIndex = index;
			AddSelected();
		}

		private void OnCartRowHovered(int index)
		{
			if (isCartFocused && cartIndex == index)
				return;
			isCartFocused = true;
			cartIndex = index;
			RefreshSelection();
		}

		private void OnCartRowClicked(int index)
		{
			if (index >= cart.Lines.Count)
				return;
			ShopCartLine line = cart.Lines[index];
			cart.RemoveOne(line.Goods.ItemId, line.Side);
			isCartFocused = true;
			cartIndex = Mathf.Min(index, cart.Lines.Count);
			UiSfx.Play(UiSfxEvent.Cancel);
			RefreshAll(jumpCursor: false);
		}

		private void OnPayHovered()
		{
			if (IsPayFocused)
				return;
			isCartFocused = true;
			cartIndex = cart.Lines.Count;
			RefreshSelection();
		}

		private int OwnedOf(int itemId) => soManager.ItemInventory.CountByID(itemId);

		private ShopGoods GoodsOf(ItemData item) =>
			new(item.ID, npc.GetShopPrice(item), item.SalePrice, item.MaxAmount, OwnedOf(item.ID));
	}
}
