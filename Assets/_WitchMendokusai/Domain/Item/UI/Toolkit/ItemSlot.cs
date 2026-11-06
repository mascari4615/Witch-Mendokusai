using UnityEngine.UIElements;

namespace WitchMendokusai
{
	[UxmlElement]
	public partial class ItemSlot : Slot
	{
		public const string ITEM_SLOT_CLASS = "wm-item-slot";

		public Item Item { get; private set; }
		public Inventory Inventory { get; private set; }

		public ItemSlot()
		{
			AddToClassList(ITEM_SLOT_CLASS);
			AddToClassList(WMWindow.NAV_CLASS);
			RegisterCallback<PointerDownEvent>(OnPointerDown);
			RegisterCallback<PointerUpEvent>(OnPointerUp);
			RegisterCallback<NavigationSubmitEvent>(OnSubmit);
			RegisterCallback<FocusInEvent>(_ => MoveHoldingHere());
		}

		// Enter는 왼쪽 클릭과 동일. 집은 물건 표시는 선택 칸으로 이동
		private void OnSubmit(NavigationSubmitEvent evt)
		{
			if (Inventory == null)
				return;

			HoldingManager.Instance.HandleSlotClick(this, Inventory, 0, 1);
			MoveHoldingHere();
			evt.StopPropagation();
		}

		private void MoveHoldingHere()
		{
			if (HoldingManager.TryGetExistingInstance(out HoldingManager holding) && holding.IsHolding)
				holding.MoveOverlayTo(worldBound.center);
		}

		public void SetInventory(Inventory inventory) => Inventory = inventory;

		public void Bind(Item item)
		{
			Item = item;

			if (item == null || item.Data == null)
			{
				Clear();
				SetTooltipData(null);
				return;
			}

			SetIcon(((ItemData)item.Data).Sprite);
			SetAmount(item.Amount);
			SetTooltipData(item.Data);
		}

		private void OnPointerDown(PointerDownEvent evt)
		{
			if (Inventory == null)
				return;

			HoldingManager.Instance.HandleSlotClick(this, Inventory, evt.button, evt.clickCount);
			evt.StopPropagation();
		}

		private void OnPointerUp(PointerUpEvent evt)
		{
			if (Inventory == null)
				return;

			HoldingManager.Instance.HandleSlotRelease(this, Inventory, evt.button);
			evt.StopPropagation();
		}
	}
}
