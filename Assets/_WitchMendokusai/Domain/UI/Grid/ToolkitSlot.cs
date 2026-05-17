using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// UI Toolkit 슬롯 위젯 — uGUI UISlot(233L) 의 Toolkit 병렬 신설 (TASK-WM-113 S3-A).
	/// 구 UISlot 은 미이행 형제 패널(Card/Item/Quest/Skill/Upgrade)이 여전히 실사용 →
	/// 빅뱅 X, ToolkitSlot 은 *신규 병렬* (first-use = 던전엔트런스 S3). 형제 deletion = 최후 E.
	/// SlotData 구동 (C0 에서 TooltipController 가 SlotData builder 보유 → 툴팁 seam 재사용).
	/// uGUI 매핑: Button.onClick → Clickable / ISelectHandler·Select() → focusable+Focus()
	/// / Navigation → Toolkit 포커스링(USS :focus, 명시 Navigation 직역 X).
	/// </summary>
	public class ToolkitSlot : VisualElement
	{
		public const string USS_CLASS = "wm-slot";
		public const string ICON_CLASS = "wm-slot__icon";
		public const string NAME_CLASS = "wm-slot__name";
		public const string AMOUNT_CLASS = "wm-slot__amount";
		public const string DISABLE_CLASS = "wm-slot--disabled";
		public const string EMPTY_CLASS = "wm-slot--empty";

		public int Index { get; private set; } = -1;
		public SlotData Data { get; private set; } = new();
		public bool IsDisable { get; private set; } = false;
		public DataSO DataSO => Data.DataSO;

		private readonly VisualElement icon;
		private readonly Label nameLabel;
		private readonly Label amountLabel;

		private readonly bool showAmountOne;
		private readonly bool blockClickWhenDisable;
		private readonly bool hideIconBackgroundWhenEmpty;

		private Action<ToolkitSlot> selectAction = delegate { };
		private Action<ToolkitSlot> deselectAction = delegate { };
		private Action<ToolkitSlot> clickAction = delegate { };

		public ToolkitSlot(bool showAmountOne = false, bool blockClickWhenDisable = false, bool hideIconBackgroundWhenEmpty = false)
		{
			this.showAmountOne = showAmountOne;
			this.blockClickWhenDisable = blockClickWhenDisable;
			this.hideIconBackgroundWhenEmpty = hideIconBackgroundWhenEmpty;

			AddToClassList(USS_CLASS);
			focusable = true;

			icon = new VisualElement();
			icon.AddToClassList(ICON_CLASS);
			icon.pickingMode = PickingMode.Ignore;
			Add(icon);

			nameLabel = new Label();
			nameLabel.AddToClassList(NAME_CLASS);
			nameLabel.pickingMode = PickingMode.Ignore;
			Add(nameLabel);

			amountLabel = new Label();
			amountLabel.AddToClassList(AMOUNT_CLASS);
			amountLabel.pickingMode = PickingMode.Ignore;
			Add(amountLabel);

			this.AddManipulator(new Clickable(OnClick));
			RegisterCallback<PointerEnterEvent>(OnPointerEnter);
			RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
			RegisterCallback<FocusInEvent>(OnFocusIn);
			RegisterCallback<FocusOutEvent>(OnFocusOut);
		}

		public void SetSlotIndex(int index) => Index = index;

		public void SetSlot(DataSO dataSO, int amount = 1) => SetSlot_(() => Data.SetData(dataSO, amount));

		public void SetSlot(Sprite sprite, string name, string description, int amount = 1)
			=> SetSlot_(() => Data.SetData(sprite, name, description, amount));

		private void SetSlot_(Action action)
		{
			action?.Invoke();
			UpdateUI();
		}

		public void UpdateUI()
		{
			bool hasContent = Data.Sprite != null || string.IsNullOrEmpty(Data.Name) == false;

			if (Data.Sprite != null)
				icon.style.backgroundImage = new StyleBackground(Data.Sprite);
			else
				icon.style.backgroundImage = StyleKeyword.None;

			EnableInClassList(EMPTY_CLASS, hasContent == false);
			if (hideIconBackgroundWhenEmpty)
				icon.style.visibility = hasContent ? Visibility.Visible : Visibility.Hidden;

			nameLabel.text = Data.Name;
			amountLabel.text = (Data.Amount == 1 && showAmountOne == false) ? "" : Data.Amount.ToString();
		}

		public void SetDisable(bool isDisable)
		{
			IsDisable = isDisable;
			EnableInClassList(DISABLE_CLASS, isDisable);
		}

		public void SetSelectAction(Action<ToolkitSlot> action) => selectAction = action;
		public void SetDeselectAction(Action<ToolkitSlot> action) => deselectAction = action;
		public void SetClickAction(Action<ToolkitSlot> action) => clickAction = action;

		public void Select() => Focus();

		private void OnClick()
		{
			if (blockClickWhenDisable && IsDisable)
				return;

			clickAction?.Invoke(this);
			ShowTooltip();
		}

		private void OnFocusIn(FocusInEvent evt)
		{
			selectAction?.Invoke(this);
			ShowTooltip();
		}

		private void OnFocusOut(FocusOutEvent evt)
		{
			deselectAction?.Invoke(this);
		}

		private void OnPointerEnter(PointerEnterEvent evt) => ShowTooltip();

		private void OnPointerLeave(PointerLeaveEvent evt)
		{
			if (TooltipController.TryGetExistingInstance(out TooltipController controller))
				controller.Hide();
		}

		private void ShowTooltip()
		{
			if (Data == null || Data.IsEmpty)
				return;
			if (TooltipController.TryGetExistingInstance(out TooltipController controller))
				controller.Show(Data);
		}
	}
}
