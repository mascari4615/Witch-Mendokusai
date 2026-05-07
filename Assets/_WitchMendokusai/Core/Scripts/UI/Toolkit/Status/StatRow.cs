using UnityEngine.UIElements;

namespace WitchMendokusai
{
	public class StatRow : VisualElement
	{
		public const string USS_CLASS = "wm-stat-row";
		public const string USS_ICON = "wm-stat-row__icon";
		public const string USS_NAME = "wm-stat-row__name";
		public const string USS_VALUE = "wm-stat-row__value";

		private readonly UnitStatType statType;
		private readonly UnitStatData statData;
		private readonly VisualElement icon;
		private readonly Label nameLabel;
		private readonly Label valueLabel;

		public UnitStatType StatType => statType;

		public StatRow(UnitStatType type)
		{
			statType = type;
			statData = SOHelper.Get<UnitStatData>((int)type);

			AddToClassList(USS_CLASS);

			icon = new VisualElement();
			icon.AddToClassList(USS_ICON);
			icon.pickingMode = PickingMode.Ignore;
			Add(icon);

			if (statData != null && statData.Sprite != null)
				icon.style.backgroundImage = new StyleBackground(statData.Sprite);

			nameLabel = new Label(statData?.Name ?? type.ToString());
			nameLabel.AddToClassList(USS_NAME);
			nameLabel.pickingMode = PickingMode.Ignore;
			Add(nameLabel);

			valueLabel = new Label();
			valueLabel.AddToClassList(USS_VALUE);
			valueLabel.pickingMode = PickingMode.Ignore;
			Add(valueLabel);
		}

		public void Refresh(UnitStat unitStat)
		{
			if (unitStat == null)
			{
				valueLabel.text = "—";
				return;
			}
			valueLabel.text = unitStat[statType].ToString();
		}
	}
}
