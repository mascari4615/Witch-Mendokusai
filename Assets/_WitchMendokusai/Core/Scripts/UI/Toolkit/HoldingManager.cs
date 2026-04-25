using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 슬롯 인터액션의 글로벌 holding 상태 관리. 마인크래프트식 좌/우/더블 매트릭스.
	/// HoldingOverlay는 별도 — 각 패널(InventoryView)이 자기 root에 부착하고 RegisterOverlay로 등록.
	/// </summary>
	public class HoldingManager : Singleton<HoldingManager>
	{
		private Item holdingItem;
		private Inventory holdingInventory;
		private HoldingOverlay overlay;

		public bool IsHolding => holdingItem != null;

		public void RegisterOverlay(HoldingOverlay newOverlay) => overlay = newOverlay;

		public void UnregisterOverlay(HoldingOverlay oldOverlay)
		{
			if (overlay == oldOverlay)
				overlay = null;
		}

		public void OnPointerMove(Vector2 panelPosition) => overlay?.FollowPointer(panelPosition);

		public void HandleSlotClick(ItemSlot slot, Inventory inventory, int button, int clickCount)
		{
			if (slot == null || inventory == null)
				return;

			Item targetItem = inventory.GetItem(slot.Index);
			bool isLeft = button == 0;
			bool isRight = button == 1;

			if (IsHolding == false)
			{
				HandleClickWithoutHolding(slot, inventory, targetItem, isLeft, isRight);
			}
			else
			{
				HandleClickWithHolding(slot, inventory, targetItem, isLeft, isRight);
			}
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

		private void HandleClickWithHolding(ItemSlot slot, Inventory inventory, Item targetItem, bool isLeft, bool isRight)
		{
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

		private void HoldFull(ItemSlot slot, Inventory inventory, Item targetItem)
		{
			holdingItem = targetItem;
			holdingInventory = inventory;
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
			holdingInventory = inventory;
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
				holdingInventory = inventory;
				overlay?.SetItem(holdingItem);
			}
		}

		private void ClearHolding()
		{
			holdingItem = null;
			holdingInventory = null;
			overlay?.SetItem(null);
		}
	}
}
