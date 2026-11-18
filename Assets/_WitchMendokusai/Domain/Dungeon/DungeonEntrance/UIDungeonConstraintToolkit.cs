using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 입장 창 제약 칸. 제약마다 아이콘과 수치 칩, 꺼진 제약은 흐림, 오른쪽 위 위험도 합계
	/// 칸 전체가 버튼. 누르면 제약 설정 팝업 (UIDungeonConstraintPopup)
	/// DungeonManager 는 켜진 제약만 판에 적용 (DungeonContext.Constraints)
	/// </summary>
	public class UIDungeonConstraintToolkit : Button
	{
		public const string USS_CLASS = "wm-dungeon-constraint";
		// 머리줄은 입장 창 다른 칸과 같은 클래스. 테마 USS 를 같이 받음
		private const string USS_HEAD = "wm-dungeon-entrance__header";
		private const string USS_TITLE = "wm-dungeon-entrance__header-text";
		private const string USS_TITLE_LINE = "wm-dungeon-entrance__header-line";
		private const string USS_RISK = USS_CLASS + "__risk";
		private const string USS_CHIPS = USS_CLASS + "__chips";
		private const string USS_CHIP = USS_CLASS + "__chip";
		private const string USS_CHIP_OFF = USS_CHIP + "--off";
		private const string USS_CHIP_ICON = USS_CLASS + "__chip-icon";
		private const string USS_CHIP_VALUE = USS_CLASS + "__chip-value";
		private const string USS_EDIT = USS_CLASS + "__edit";

		private Dungeon dungeon;
		private readonly Label riskLabel;
		private readonly VisualElement chipRow;
		private readonly List<VisualElement> chips = new();
		private readonly List<VisualElement> chipIcons = new();
		private readonly List<Label> chipValues = new();

		public UIDungeonConstraintToolkit(Action onOpen) : base(onOpen)
		{
			name = "ConstraintButton";
			AddToClassList(USS_CLASS);

			VisualElement head = AddChild(this, new VisualElement(), USS_HEAD);
			AddChild(head, new Label("제약"), USS_TITLE);
			AddChild(head, new VisualElement(), USS_TITLE_LINE);
			riskLabel = AddChild(head, new Label(), USS_RISK);

			chipRow = AddChild(this, new VisualElement(), USS_CHIPS);
			AddChild(this, new Label("눌러서 설정"), USS_EDIT);
		}

		public void SetDungeon(Dungeon dungeon) => this.dungeon = dungeon;

		public void UpdateUI()
		{
			EnsureChipCount(dungeon.Constraints.Count);
			for (int i = 0; i < chips.Count; i++)
			{
				bool isUsed = i < dungeon.Constraints.Count;
				chips[i].style.display = isUsed ? DisplayStyle.Flex : DisplayStyle.None;
				if (isUsed == false)
					continue;

				DungeonConstraint constraint = dungeon.Constraints[i];
				chipIcons[i].style.backgroundImage = new StyleBackground(constraint.Sprite);
				chipValues[i].text = constraint.ShortValue;
				chips[i].EnableInClassList(USS_CHIP_OFF, dungeon.ConstraintSelected[constraint.ID] == false);
			}
			riskLabel.text = "위험도 " + DungeonConstraintRisk.Total(dungeon);
		}

		private void EnsureChipCount(int count)
		{
			while (chips.Count < count)
			{
				VisualElement chip = AddChild(chipRow, new VisualElement { pickingMode = PickingMode.Ignore }, USS_CHIP);
				chipIcons.Add(AddChild(chip, new VisualElement { pickingMode = PickingMode.Ignore }, USS_CHIP_ICON));
				chipValues.Add(AddChild(chip, new Label { pickingMode = PickingMode.Ignore }, USS_CHIP_VALUE));
				chips.Add(chip);
			}
		}

		private static T AddChild<T>(VisualElement parent, T child, string ussClass) where T : VisualElement
		{
			child.AddToClassList(ussClass);
			parent.Add(child);
			return child;
		}
	}

	/// <summary> 켠 제약의 위험도 합. 입장 칸과 설정 팝업이 같이 씀 </summary>
	public static class DungeonConstraintRisk
	{
		public static int Total(Dungeon dungeon)
		{
			int total = 0;
			foreach (DungeonConstraint constraint in dungeon.Constraints)
			{
				if (dungeon.ConstraintSelected[constraint.ID])
					total += constraint.Risk;
			}
			return total;
		}
	}
}
