using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 슬롯 인터액션의 글로벌 holding 상태 관리. 마인크래프트식 좌/우/더블 매트릭스.
	/// HoldingOverlay는 별도 — 각 패널(InventoryView)이 자기 root에 부착하고 RegisterOverlay로 등록.
	/// </summary>
	public class HoldingManager : MonoBehaviour
	{
		public static HoldingManager Instance { get; private set; }

		public static bool TryGetExistingInstance(out HoldingManager mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		private const float DOUBLE_CLICK_THRESHOLD = 0.25f;

		private Item holdingItem;
		private Inventory holdingInventory;
		private int holdingIndex = -1;
		private ItemSlot holdingSourceSlot;
		private HoldingOverlay overlay;
		private float lastClickTime;
		private ItemSlot lastClickSlot;
		private ItemSlot pressedSlot;
		private Vector2 lastPointerPosition;

		public bool IsHolding => holdingItem != null;

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
		}

		private void OnDestroy()
		{
			if (Instance == this)
				Instance = null;
		}

		public void RegisterOverlay(HoldingOverlay newOverlay) => overlay = newOverlay;

		public void UnregisterOverlay(HoldingOverlay oldOverlay)
		{
			if (overlay == oldOverlay)
				overlay = null;
		}

		public void OnPointerMove(Vector2 panelPosition)
		{
			if (panelPosition == lastPointerPosition)
				return;
			lastPointerPosition = panelPosition;
			overlay?.FollowPointer(panelPosition);
		}

		public void MoveOverlayTo(Vector2 panelPosition) => overlay?.FollowPointer(panelPosition);

		public void HandleSlotClick(ItemSlot slot, Inventory inventory, int button, int clickCount)
		{
			if (slot == null || inventory == null)
				return;

			Item targetItem = inventory.GetItem(slot.Index);
			bool isLeft = button == 0;
			bool isRight = button == 1;

			// 같은 슬롯 빠른 두 번 클릭만 더블로 인정 (다른 슬롯 빠르게 클릭은 single x2)
			float now = Time.unscaledTime;
			bool isDouble = (slot == lastClickSlot) && (now - lastClickTime < DOUBLE_CLICK_THRESHOLD);
			lastClickTime = now;
			lastClickSlot = slot;
			pressedSlot = slot;

			if (IsHolding == false)
			{
				HandleClickWithoutHolding(slot, inventory, targetItem, isLeft, isRight);
			}
			else
			{
				HandleClickWithHolding(slot, inventory, targetItem, isLeft, isRight, isDouble);
			}
		}

		// 드래그 놓기. 누른 칸과 다른 칸에서 떼면 그 칸에 내려놓음
		public void HandleSlotRelease(ItemSlot slot, Inventory inventory, int button)
		{
			ItemSlot origin = pressedSlot;
			pressedSlot = null;
			if (button != 0 || IsHolding == false || origin == null || origin == slot)
				return;

			lastClickSlot = null;
			HandleClickWithHolding(slot, inventory, inventory.GetItem(slot.Index), true, false, false);
		}

		private void HandleClickWithoutHolding(ItemSlot slot, Inventory inventory, Item targetItem, bool isLeft, bool isRight)
		{
			if (targetItem == null)
				return;

			if (isLeft)
			{
				HoldFull(slot, inventory, targetItem);
			}
			else if (isRight)
			{
				HoldHalf(slot, inventory, targetItem);
			}
		}

		/// <summary>
		/// source 인벤 없이 holding 슬롯을 채운다 (개발자 윈도우 / 크리에이티브 모드용).
		/// holdingInventory = null 로 두며, 이후 Drop/Swap 시 source 비우기 분기는 null 가드로 skip.
		/// 이미 holding 중이면 무시 — 사용자가 holding 비우고 다시 호출해야 함.
		/// </summary>
		public void PickFromVoid(ItemData data, int amount)
		{
			if (data == null || amount <= 0)
				return;

			if (IsHolding == true)
				return;

			holdingItem = new Item(new(), data, amount);
			holdingInventory = null;
			holdingIndex = -1;
			holdingSourceSlot = null;
			overlay?.SetItem(holdingItem);
		}

		private void HandleClickWithHolding(ItemSlot slot, Inventory inventory, Item targetItem, bool isLeft, bool isRight, bool isDouble)
		{
			// 더블클릭 + 합칠 게 있으면 FuncA, 없으면 single 동작으로 fall-through (사용자 의도 보호)
			if (inventory.CanAccept(slot.Index, holdingItem) == false)
				return;

			bool doFuncA = isLeft && isDouble && HasCollectibleSameId();

			if (doFuncA)
			{
				CollectAllSameId();
				return;
			}

			if (targetItem == null)
			{
				if (isLeft)
					DropAll(slot, inventory);
				else if (isRight)
					DropOne(slot, inventory);
			}
			else
			{
				if (isLeft)
					SwapOrStack(slot, inventory, targetItem);
				else if (isRight && targetItem.Data.ID == holdingItem.Data.ID)
					DropOne(slot, inventory);
			}
		}

		/// <summary>
		/// holding과 같은 ID 아이템이 source 인벤토리에 존재하고 합쳐질 여지가 있는지.
		/// </summary>
		private bool HasCollectibleSameId()
		{
			if (holdingItem == null || holdingInventory == null)
				return false;

			if (holdingItem.Amount >= holdingItem.MaxAmount)
				return false;

			for (int i = 0; i < holdingInventory.Capacity; i++)
			{
				Item item = holdingInventory.GetItem(i);
				if (item != null && item.Data.ID == holdingItem.Data.ID)
					return true;
			}

			return false;
		}

		/// <summary>
		/// 들고있는 아이템과 같은 ID의 아이템을 source 인벤토리에서 모두 holding으로 모음.
		/// 마인크래프트 더블클릭 동작.
		/// </summary>
		private void CollectAllSameId()
		{
			if (holdingItem == null || holdingInventory == null)
				return;

			int maxAmount = holdingItem.MaxAmount;

			for (int i = 0; i < holdingInventory.Capacity; i++)
			{
				if (holdingItem.Amount >= maxAmount)
					break;

				Item item = holdingInventory.GetItem(i);
				if (item == null || item.Data.ID != holdingItem.Data.ID)
					continue;

				int sum = item.Amount + holdingItem.Amount;
				if (sum <= maxAmount)
				{
					holdingItem.SetAmount(sum);
					holdingInventory.SetItem(i, null);
				}
				else
				{
					item.SetAmount(sum - maxAmount);
					holdingItem.SetAmount(maxAmount);
					holdingInventory.UpdateSlot(i);
				}
			}

			overlay?.SetItem(holdingItem);
		}

		private void HoldFull(ItemSlot slot, Inventory inventory, Item targetItem)
		{
			holdingItem = targetItem;
			SetHoldingSource(inventory, slot);
			inventory.SetItem(slot.Index, null);
			overlay?.SetItem(holdingItem);
		}

		private void HoldHalf(ItemSlot slot, Inventory inventory, Item targetItem)
		{
			if (targetItem.Amount == 1)
			{
				HoldFull(slot, inventory, targetItem);
				return;
			}

			int halfAmount = targetItem.Amount / 2;
			holdingItem = new Item(new(), targetItem.Data, halfAmount);
			SetHoldingSource(inventory, slot);
			targetItem.SetAmount(targetItem.Amount - halfAmount);
			inventory.UpdateSlot(slot.Index);
			overlay?.SetItem(holdingItem);
		}

		private void DropAll(ItemSlot slot, Inventory inventory)
		{
			inventory.SetItem(slot.Index, holdingItem);
			ClearHolding();
		}

		private void DropOne(ItemSlot slot, Inventory inventory)
		{
			Item targetItem = inventory.GetItem(slot.Index);

			if (targetItem != null && targetItem.Data.ID == holdingItem.Data.ID)
			{
				if (targetItem.Amount < targetItem.MaxAmount)
				{
					holdingItem.SetAmount(holdingItem.Amount - 1);
					inventory.SetItemAmount(slot.Index, targetItem.Amount + 1);
				}
			}
			else
			{
				Item newItem = new(new(), holdingItem.Data, 1);
				holdingItem.SetAmount(holdingItem.Amount - 1);
				inventory.SetItem(slot.Index, newItem);
			}

			if (holdingItem.Amount <= 0)
				ClearHolding();
			else
				overlay?.SetItem(holdingItem);
		}

		private void SwapOrStack(ItemSlot slot, Inventory inventory, Item slotItem)
		{
			if (slotItem.Data.ID == holdingItem.Data.ID)
			{
				int maxAmount = slotItem.MaxAmount;
				int sum = slotItem.Amount + holdingItem.Amount;

				if (sum <= maxAmount)
				{
					slotItem.SetAmount(sum);
					inventory.UpdateSlot(slot.Index);
					ClearHolding();
				}
				else
				{
					slotItem.SetAmount(maxAmount);
					holdingItem.SetAmount(sum - maxAmount);
					inventory.UpdateSlot(slot.Index);
					overlay?.SetItem(holdingItem);
				}
			}
			else
			{
				inventory.SetItem(slot.Index, holdingItem);
				holdingItem = slotItem;
				SetHoldingSource(inventory, slot);
				overlay?.SetItem(holdingItem);
			}
		}

		private void SetHoldingSource(Inventory inventory, ItemSlot slot)
		{
			holdingInventory = inventory;
			holdingIndex = slot.Index;
			holdingSourceSlot = slot;
		}

		// 창이 닫히면 그 창에서 집은 물건을 원래 자리로 되돌림. 자리가 없으면 가방, 그래도 없으면 계속 들고 있음
		public void ReturnHeld(WMWindow closedWindow)
		{
			if (holdingItem == null)
				return;
			if (holdingSourceSlot == null || closedWindow == null || closedWindow.Contains(holdingSourceSlot) == false)
				return;

			ReturnHeldToSource();
		}

		// 슬롯이 묶인 인벤토리가 바뀌기 전에 호출. 다른 인형 전환처럼 같은 칸 번호가 다른 데이터를 가리키는 경우
		public void ReturnHeldFrom(Inventory inventory)
		{
			if (holdingItem == null || holdingInventory != inventory)
				return;

			ReturnHeldToSource();
		}

		private void ReturnHeldToSource()
		{
			bool placed = holdingInventory.TryPlaceBack(holdingIndex, holdingItem);
			if (placed == false && SOManagerBridge.HasInstance && ReferenceEquals(holdingInventory, SOManagerBridge.ItemInventory) == false)
				placed = SOManagerBridge.ItemInventory.TryPlaceBack(-1, holdingItem);

			if (placed == false)
			{
				Debug.LogWarning("집은 물건을 되돌릴 빈 칸이 없어 계속 들고 있음");
				return;
			}

			ClearHolding();
		}

		private void ClearHolding()
		{
			holdingItem = null;
			holdingInventory = null;
			holdingIndex = -1;
			holdingSourceSlot = null;
			overlay?.SetItem(null);
		}
	}
}
