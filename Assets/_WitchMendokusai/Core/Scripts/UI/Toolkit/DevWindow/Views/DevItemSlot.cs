using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 개발자 윈도우 Items 모드 그리드의 슬롯. ItemData 를 직접 보유 (Inventory 비종속).
	/// 클릭 매트릭스 (마인크래프트 크리에이티브):
	/// - 좌클릭 → /give &lt;ref&gt; 1
	/// - Shift+좌 → /give &lt;ref&gt; &lt;MaxAmount&gt;
	/// - 우클릭 → /give &lt;ref&gt; 16
	/// - Ctrl+좌 → HoldingManager.PickFromVoid (인벤 슬롯에 드롭)
	/// 호버 시 TooltipController 가 ItemTooltipBuilder 로 ItemData 툴팁 표시 (재사용).
	/// </summary>
	public class DevItemSlot : Slot
	{
		public const string DEV_ITEM_SLOT_CLASS = "wm-dev-item-slot";

		private const int RIGHT_CLICK_AMOUNT = 16;

		public ItemData Data { get; private set; }

		public DevItemSlot()
		{
			AddToClassList(DEV_ITEM_SLOT_CLASS);
			RegisterCallback<PointerDownEvent>(OnPointerDown);
			RegisterCallback<PointerEnterEvent>(OnPointerEnter);
			RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
		}

		public void Bind(ItemData data)
		{
			Data = data;
			if (data == null)
			{
				Clear();
				return;
			}

			SetIcon(data.Sprite);
			SetAmount(0); // 그리드 슬롯엔 amount 표시 X
		}

		private void OnPointerDown(PointerDownEvent evt)
		{
			if (Data == null)
				return;

			bool isLeft = evt.button == 0;
			bool isRight = evt.button == 1;
			bool isShift = evt.shiftKey;
			bool isCtrl = evt.ctrlKey;

			string reference = $"I_{Data.ID}";

			if (isLeft && isCtrl)
			{
				HoldingManager.Instance.PickFromVoid(Data, 1);
				evt.StopPropagation();
				return;
			}

			DevWindowController controller = DevWindowController.Instance;
			if (controller == null)
				return;

			if (isLeft && isShift)
			{
				controller.InvokeCommand("give", reference, Data.MaxAmount.ToString());
			}
			else if (isLeft)
			{
				controller.InvokeCommand("give", reference, "1");
			}
			else if (isRight)
			{
				controller.InvokeCommand("give", reference, RIGHT_CLICK_AMOUNT.ToString());
			}

			evt.StopPropagation();
		}

		private void OnPointerEnter(PointerEnterEvent evt)
		{
			if (Data == null)
				return;
			TooltipController.Instance.Show(Data);
		}

		private void OnPointerLeave(PointerLeaveEvent evt)
		{
			if (TooltipController.TryGetExistingInstance(out TooltipController controller))
				controller.Hide();
		}
	}
}
