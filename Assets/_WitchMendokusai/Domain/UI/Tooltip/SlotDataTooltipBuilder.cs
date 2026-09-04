using UnityEngine.UIElements;
using WitchMendokusai.Presentation;

namespace WitchMendokusai
{
	/// <summary>
	/// SlotData (uGUI 슬롯 패밀리 공용 UI 뷰모델 — Sprite/Name/Description/DataSO) 툴팁 빌더.
	/// 구 ToolTip/ToolTipPopupManager(SlotData 기반) → 신 TooltipController 수렴의 seam:
	/// 구 호출처가 전달하던 SlotData 를 신 시스템이 그대로 수용 가능하게 한다 (TASK-WM-113-C0).
	/// USS class 는 ItemTooltipBuilder 상수 재사용 (BuildingTooltipBuilder 와 동일 정합).
	/// </summary>
	public class SlotDataTooltipBuilder : ITooltipBuilder
	{
		public void Build(TooltipView view, object data, TooltipMode mode)
		{
			if (data is SlotData slotData == false)
				return;

			if (slotData.IsEmpty)
				return;

			view.AddToClassList(ItemTooltipBuilder.ITEM_CLASS);
			view.AddToClassList(mode == TooltipMode.Detailed
				? ItemTooltipBuilder.MODE_DETAILED_CLASS
				: ItemTooltipBuilder.MODE_SIMPLE_CLASS);

			VisualElement header = new();
			header.AddToClassList(ItemTooltipBuilder.HEADER_CLASS);
			header.pickingMode = PickingMode.Ignore;

			VisualElement icon = new();
			icon.AddToClassList(ItemTooltipBuilder.ICON_CLASS);
			icon.pickingMode = PickingMode.Ignore;
			if (slotData.Sprite != null)
				icon.style.backgroundImage = new StyleBackground(slotData.Sprite);
			header.Add(icon);

			Label nameLabel = new(slotData.Name);
			nameLabel.AddToClassList(ItemTooltipBuilder.NAME_CLASS);
			nameLabel.pickingMode = PickingMode.Ignore;
			header.Add(nameLabel);

			view.Add(header);

			if (string.IsNullOrEmpty(slotData.Description) == false)
			{
				Label descriptionLabel = new(slotData.Description);
				descriptionLabel.AddToClassList(ItemTooltipBuilder.DESCRIPTION_CLASS);
				descriptionLabel.pickingMode = PickingMode.Ignore;
				view.Add(descriptionLabel);
			}
		}
	}
}
