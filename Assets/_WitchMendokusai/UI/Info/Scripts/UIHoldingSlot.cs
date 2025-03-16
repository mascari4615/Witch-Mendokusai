using UnityEngine;

namespace WitchMendokusai
{
	[RequireComponent(typeof(UISlot), typeof(CanvasGroup))]
	public class UIHoldingSlot : Singleton<UIHoldingSlot>
	{
		private CanvasGroup canvasGroup = null;
		private UISlot slot = null;

		private Item holdingItem = null;
		public bool IsHolding => holdingItem != null;

		private const float funcATime = 0.2f;
		private float curFuncATime = 0;
		public bool CanFuncA => curFuncATime >= 0;

		private Inventory Inventory => SOManager.Instance.ItemInventory;

		private void Start()
		{
			slot = GetComponent<UISlot>();
			slot.Init();

			canvasGroup = GetComponent<CanvasGroup>();
		}

		public void DoSomething(UIItemSlot targetSlot, bool isLeftClick)
		{
			Item targetItem = Inventory.GetItem(targetSlot.Index);

			if (IsHolding == false)
			{
				if (targetItem == null)
				{
				}
				else
				{
					if (isLeftClick)
						HoldSlot(targetSlot);
					else
						HoldSlotHalf(targetSlot);
				}
			}
			else
			{
				if (targetItem == null)
				{
					if (isLeftClick)
					{
						if (CanFuncA)
							FuncA();
						else
							DropSlot(targetSlot);
					}
					else
						DropSlotOne(targetSlot);
				}
				else
				{
					if (isLeftClick)
						SwapSlot(targetSlot);
					else if (targetItem.Data.ID == holdingItem.Data.ID)
						DropSlotOne(targetSlot);
				}
			}
		}

		private void Update()
		{
			UpdateUI();

			if (curFuncATime >= 0)
				curFuncATime -= Time.deltaTime;
		}

		private void UpdateUI()
		{
			canvasGroup.SetVisible(IsHolding, allowInteraction: false);

			if (IsHolding)
			{
				transform.position = Input.mousePosition;
				slot.SetSlot(holdingItem.Data, holdingItem.Amount);
			}
		}

		#region Actions
		// 들고있지 않은 상태에서, 비어있지 않은 슬롯에 좌클릭
		public void HoldSlot(UIItemSlot targetSlot)
		{
			Item targetItem = Inventory.GetItem(targetSlot.Index);

			if (targetSlot == null || targetItem == null)
				return;

			holdingItem = targetItem;

			Inventory.SetItem(targetSlot.Index, null);

			targetSlot.Inventory.UpdateSlot(targetSlot.Index);

			curFuncATime = funcATime;
		}

		// 들고있지 않은 상태에서, 비어있지 않은 슬롯에서 우클릭
		public void HoldSlotHalf(UIItemSlot targetSlot)
		{
			Item targetItem = Inventory.GetItem(targetSlot.Index);

			if (targetSlot == null)
				return;

			if (targetItem.Amount == 1)
			{
				HoldSlot(targetSlot);
				return;
			}

			int halfAmount = targetItem.Amount / 2;
			holdingItem = new Item(new(), targetItem.Data, halfAmount);

			targetItem.SetAmount(targetItem.Amount - halfAmount);

			targetSlot.Inventory.UpdateSlot(targetSlot.Index);
		}

		// 들고있는 상태에서, 비어있지 않은 슬롯에서 좌클릭
		public void SwapSlot(UIItemSlot targetSlot)
		{
			if (targetSlot == null || holdingItem == null)
				return;

			Item slotItem = Inventory.GetItem(targetSlot.Index);

			if (slotItem.Data.ID == holdingItem.Data.ID)
			{
				int maxAmount = slotItem.MaxAmount;
				int sum = slotItem.Amount + holdingItem.Amount;

				if (sum <= maxAmount)
				{
					slotItem.SetAmount(sum);

					holdingItem.SetAmount(0);
					holdingItem = null;
				}
				else
				{
					slotItem.SetAmount(maxAmount);
					holdingItem.SetAmount(sum - maxAmount);
				}
			}
			else
			{
				Inventory.SetItem(targetSlot.Index, holdingItem);
				holdingItem = slotItem;
			}

			Inventory.UpdateSlot(targetSlot.Index);
		}

		// 들고있는 상태에서, 비어있는 슬롯에 좌클릭
		public void DropSlot(UIItemSlot targetSlot)
		{
			Item dropItem = holdingItem;
			holdingItem = null;

			// Debug.Log("DropSlot: " + dropItem);
			Inventory.SetItem(targetSlot.Index, dropItem);
		}

		// 들고있는 상태에서, (비어있지 않은/비어있는) 슬롯에 우클릭
		public void DropSlotOne(UIItemSlot targetSlot)
		{
			Item targetItem = Inventory.GetItem(targetSlot.Index);

			if (targetItem != null && targetItem.Data.ID == holdingItem.Data.ID)
			{
				if (targetItem.Amount < targetItem.MaxAmount)
				{
					holdingItem.SetAmount(holdingItem.Amount - 1);
					Inventory.SetItemAmount(targetSlot.Index, targetItem.Amount + 1);
				}
				else
				{
				}
			}
			else
			{
				Item newItem = new(new(), holdingItem.Data, 1);
				holdingItem.SetAmount(holdingItem.Amount - 1);
				Inventory.SetItem(targetSlot.Index, newItem);
			}

			if (holdingItem.Amount <= 0)
				holdingItem = null;
		}

		// 들고, 빠르게 비어있는 슬롯에 좌클릭
		public void FuncA()
		{
			if (CanFuncA == false)
				return;

			curFuncATime = 0;

			for (int i = 0; i < Inventory.Capacity; i++)
			{
				Item item = Inventory.GetItem(i);

				if (item != null && item.Data.ID == holdingItem.Data.ID)
				{
					int maxAmount = item.MaxAmount;
					int sum = item.Amount + holdingItem.Amount;

					if (sum <= maxAmount)
					{
						holdingItem.SetAmount(sum);
						Inventory.SetItem(i, null);
						Inventory.UpdateSlot(i);
					}
					else
					{
						item.SetAmount(sum - maxAmount);
						holdingItem.SetAmount(maxAmount);

						Inventory.UpdateSlot(i);
						break;
					}
				}
			}
		}
		#endregion
	}
}