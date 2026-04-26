using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// Building 툴팁 빌더. Simple: 아이콘 + 이름 + 설명.
	/// Detailed: 추후 Size/Cost/Mascot 노출.
	/// </summary>
	public class BuildingTooltipBuilder : ITooltipBuilder
	{
		public void Build(TooltipView view, object data, TooltipMode mode)
		{
			if (data is Building building == false)
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
			if (building.Sprite != null)
				icon.style.backgroundImage = new StyleBackground(building.Sprite);
			header.Add(icon);

			Label nameLabel = new(building.Name);
			nameLabel.AddToClassList(ItemTooltipBuilder.NAME_CLASS);
			nameLabel.pickingMode = PickingMode.Ignore;
			header.Add(nameLabel);

			view.Add(header);

			if (string.IsNullOrEmpty(building.Description) == false)
			{
				Label descriptionLabel = new(building.Description);
				descriptionLabel.AddToClassList(ItemTooltipBuilder.DESCRIPTION_CLASS);
				descriptionLabel.pickingMode = PickingMode.Ignore;
				view.Add(descriptionLabel);
			}
		}
	}
}
