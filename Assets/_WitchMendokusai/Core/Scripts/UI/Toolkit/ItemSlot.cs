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
			RegisterCallback<PointerDownEvent>(OnPointerDown);
			RegisterCallback<PointerEnterEvent>(OnPointerEnter);
			RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
		}

		public void SetInventory(Inventory inventory) => Inventory = inventory;

		public void Bind(Item item)
		{
			Item = item;

			if (item == null || item.Data == null)
			{
				Clear();
				return;
			}

			SetIcon(item.Data.Sprite);
			SetAmount(item.Amount);
		}

		private void OnPointerDown(PointerDownEvent evt)
		{
			if (Inventory == null)
				return;

			HoldingManager.Instance.HandleSlotClick(this, Inventory, evt.button, evt.clickCount);
			evt.StopPropagation();
		}

		private void OnPointerEnter(PointerEnterEvent evt)
		{
			if (Item == null || Item.Data == null)
				return;
			TooltipController.Instance.Show(Item.Data);
		}

		private void OnPointerLeave(PointerLeaveEvent evt)
		{
			if (TooltipController.TryGetExistingInstance(out TooltipController controller))
				controller.Hide();
		}
	}
}
