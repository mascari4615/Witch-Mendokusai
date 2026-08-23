using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// ItemData (및 하위 타입 — EquipmentData 등) 툴팁 빌더.
	/// Simple: 아이콘 + 이름 + 설명.
	/// Detailed: stub. 추후 대열 방치 전투 계열식 상세 정보 (등급, 가격, 효과, 재료 등) 확장 예정.
	/// </summary>
	public class ItemTooltipBuilder : ITooltipBuilder
	{
		public const string ITEM_CLASS = "wm-tooltip__item";
		public const string HEADER_CLASS = "wm-tooltip__header";
		public const string ICON_CLASS = "wm-tooltip__icon";
		public const string NAME_CLASS = "wm-tooltip__name";
		public const string DESCRIPTION_CLASS = "wm-tooltip__description";
		public const string GRADE_CLASS = "wm-tooltip__grade";
		public const string MODE_SIMPLE_CLASS = "wm-tooltip--simple";
		public const string MODE_DETAILED_CLASS = "wm-tooltip--detailed";

		public void Build(TooltipView view, object data, TooltipMode mode)
		{
			if (data is ItemData itemData == false)
				return;

			view.AddToClassList(ITEM_CLASS);
			view.AddToClassList(mode == TooltipMode.Detailed ? MODE_DETAILED_CLASS : MODE_SIMPLE_CLASS);

			BuildHeader(view, itemData);
			BuildDescription(view, itemData);

			if (mode == TooltipMode.Detailed)
				BuildDetailedSections(view, itemData);
		}

		private void BuildHeader(TooltipView view, ItemData itemData)
		{
			VisualElement header = new();
			header.AddToClassList(HEADER_CLASS);
			header.pickingMode = PickingMode.Ignore;

			VisualElement icon = new();
			icon.AddToClassList(ICON_CLASS);
			icon.pickingMode = PickingMode.Ignore;
			if (itemData.Sprite != null)
				icon.style.backgroundImage = new StyleBackground(itemData.Sprite);
			header.Add(icon);

			Label nameLabel = new(itemData.Name);
			nameLabel.AddToClassList(NAME_CLASS);
			nameLabel.pickingMode = PickingMode.Ignore;
			header.Add(nameLabel);

			view.Add(header);
		}

		private void BuildDescription(TooltipView view, ItemData itemData)
		{
			if (string.IsNullOrEmpty(itemData.Description))
				return;

			Label descriptionLabel = new(itemData.Description);
			descriptionLabel.AddToClassList(DESCRIPTION_CLASS);
			descriptionLabel.pickingMode = PickingMode.Ignore;
			view.Add(descriptionLabel);
		}

		private void BuildDetailedSections(TooltipView view, ItemData itemData)
		{
			// TODO: 대열 방치 전투 계열식 상세 (등급/가격/효과/레시피). 현재는 최소만.
			Label gradeLabel = new(itemData.Grade.ToString());
			gradeLabel.AddToClassList(GRADE_CLASS);
			gradeLabel.pickingMode = PickingMode.Ignore;
			view.Add(gradeLabel);
		}
	}
}
