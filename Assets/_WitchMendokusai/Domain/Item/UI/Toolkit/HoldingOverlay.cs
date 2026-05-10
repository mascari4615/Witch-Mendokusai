using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 글로벌 floating slot. 사용자가 들고 있는 아이템을 마우스 위치에 표시.
	/// 본인은 PointerEvent를 잡지 않음 (pickingMode = Ignore).
	/// </summary>
	public class HoldingOverlay : VisualElement
	{
		public const string USS_CLASS = "wm-holding-overlay";

		private readonly Slot slot;

		public HoldingOverlay()
		{
			AddToClassList(USS_CLASS);
			pickingMode = PickingMode.Ignore;

			slot = new Slot();
			slot.pickingMode = PickingMode.Ignore;
			Add(slot);

			SetVisible(false);
		}

		public void SetItem(Item item)
		{
			if (item == null || item.Data == null)
			{
				slot.Clear();
				SetVisible(false);
				return;
			}

			slot.SetIcon(((ItemData)item.Data).Sprite);
			slot.SetAmount(item.Amount);
			SetVisible(true);
		}

		public void FollowPointer(Vector2 panelPosition)
		{
			style.left = panelPosition.x - 24;
			style.top = panelPosition.y - 24;
		}

		private void SetVisible(bool visible)
		{
			style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
		}
	}
}
