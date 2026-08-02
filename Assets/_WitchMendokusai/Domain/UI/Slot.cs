using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	[UxmlElement]
	public partial class Slot : VisualElement
	{
		public const string USS_CLASS = "wm-slot";
		public const string ICON_CLASS = "wm-slot__icon";
		public const string AMOUNT_CLASS = "wm-slot__amount";
		public const string SELECTED_CLASS = "wm-slot--selected";
		public const string EMPTY_CLASS = "wm-slot--empty";

		private readonly VisualElement icon;
		private readonly Label amountLabel;
		private object tooltipData;

		public int Index { get; private set; } = -1;
		public bool IsEmpty { get; private set; } = true;

		public Slot()
		{
			AddToClassList(USS_CLASS);
			AddToClassList(EMPTY_CLASS);
			focusable = true;
			pickingMode = PickingMode.Position;

			icon = new VisualElement();
			icon.AddToClassList(ICON_CLASS);
			icon.pickingMode = PickingMode.Ignore;
			Add(icon);

			amountLabel = new Label();
			amountLabel.AddToClassList(AMOUNT_CLASS);
			amountLabel.pickingMode = PickingMode.Ignore;
			Add(amountLabel);

			RegisterCallback<PointerEnterEvent>(OnPointerEnterTooltip);
			RegisterCallback<PointerLeaveEvent>(OnPointerLeaveTooltip);
		}

		public void SetTooltipData(object data) => tooltipData = data;

		private void OnPointerEnterTooltip(PointerEnterEvent _)
		{
			if (tooltipData == null)
				return;
			// TASK-WM-133 — panel-root owner-push 된 TooltipController(panel-context)
			// 경유. pointer 이벤트 = 부착 보장 시점이라 event-time resolve.
			this.GetUIServices()?.Tooltip?.Show(tooltipData);
		}

		private void OnPointerLeaveTooltip(PointerLeaveEvent _)
		{
			this.GetUIServices()?.Tooltip?.Hide();
		}

		public void SetIndex(int index) => Index = index;

		public void SetIcon(Sprite sprite)
		{
			if (sprite != null)
			{
				icon.style.backgroundImage = new StyleBackground(sprite);
				IsEmpty = false;
				RemoveFromClassList(EMPTY_CLASS);
			}
			else
			{
				icon.style.backgroundImage = StyleKeyword.None;
				IsEmpty = true;
				AddToClassList(EMPTY_CLASS);
			}
		}

		/// <summary>
		/// 그림이 아직 없는 칸을 색으로 채운다 — 아트가 붙기 전에도 「무엇인가」가 구분돼야 한다.
		/// 스프라이트가 생기면 SetIcon 이 덮으므로, 이 호출을 지우는 것만으로 그림 전환이 끝난다.
		/// </summary>
		public void SetTint(Color tint)
		{
			icon.style.backgroundColor = tint;
			IsEmpty = false;
			RemoveFromClassList(EMPTY_CLASS);
		}

		public void SetAmount(int amount)
		{
			amountLabel.text = amount > 1 ? amount.ToString() : string.Empty;
		}

		public new void Clear()
		{
			SetIcon(null);
			SetAmount(0);
		}

		public void SetSelected(bool selected)
		{
			if (selected)
				AddToClassList(SELECTED_CLASS);
			else
				RemoveFromClassList(SELECTED_CLASS);
		}
	}
}
