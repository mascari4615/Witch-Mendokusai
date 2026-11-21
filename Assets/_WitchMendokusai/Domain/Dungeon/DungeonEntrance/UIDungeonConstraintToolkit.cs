using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 입장 창 제약 칸. 묶음마다 아이콘과 수치 칩, 꺼진 묶음은 흐림, 오른쪽 위 위험도 합계. 칩 5개까지 한 줄
	/// 칸 전체가 버튼. 누르면 제약 설정 팝업 (UIDungeonConstraintPopup). 첫 클리어 전 잠긴 던전은 흐린 칩과 해금 조건 글
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
		private const string USS_LOCKED = USS_CLASS + "--locked";
		private const string EDIT_TEXT = "눌러서 설정";
		private const string LOCKED_TEXT = "첫 클리어 뒤 열림";

		private Dungeon dungeon;
		private readonly Label riskLabel;
		private readonly VisualElement chipRow;
		private readonly Label editLabel;
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
			editLabel = AddChild(this, new Label(EDIT_TEXT), USS_EDIT);
		}

		public void SetDungeon(Dungeon dungeon) => this.dungeon = dungeon;

		// 묶음마다 칩 하나. 켠 단계가 있으면 그 단계 아이콘과 수치, 없으면 흐린 아이콘만
		public void UpdateUI()
		{
			bool isUnlocked = dungeon.ConstraintsUnlocked;
			EnableInClassList(USS_LOCKED, isUnlocked == false);
			editLabel.text = isUnlocked ? EDIT_TEXT : LOCKED_TEXT;
			List<string> families = DungeonConstraintRules.FamilyOrder(dungeon);
			EnsureChipCount(families.Count);
			for (int i = 0; i < chips.Count; i++)
			{
				bool isUsed = i < families.Count;
				chips[i].style.display = isUsed ? DisplayStyle.Flex : DisplayStyle.None;
				if (isUsed == false)
					continue;

				DungeonConstraint shown = null;
				DungeonConstraint selected = null;
				foreach (DungeonConstraint constraint in dungeon.Constraints)
				{
					if (constraint.FamilyKey != families[i])
						continue;
					shown ??= constraint;
					if (isUnlocked && dungeon.ConstraintSelected[constraint.ID])
						selected = constraint;
				}
				DungeonConstraint target = selected ?? shown;
				chipIcons[i].style.backgroundImage = new StyleBackground(target.Sprite);
				chipValues[i].text = selected != null ? selected.ShortValue : string.Empty;
				chipValues[i].style.display = selected != null ? DisplayStyle.Flex : DisplayStyle.None;
				chips[i].EnableInClassList(USS_CHIP_OFF, selected == null);
			}
			riskLabel.text = "위험도 " + DungeonConstraintRules.TotalRisk(dungeon.SelectedConstraints());
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
}
