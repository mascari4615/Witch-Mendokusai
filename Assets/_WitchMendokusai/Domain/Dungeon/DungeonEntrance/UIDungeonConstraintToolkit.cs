using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 입장 창 제약 칸. 묶음마다 아이콘과 수치 칩, 꺼진 묶음은 흐림, 오른쪽 위 위험도 합계
	/// 칩은 CHIP_MAX 개까지 한 줄 (오른쪽 아래 글과 안 겹치는 수). 켠 묶음 먼저, 넘친 켠 묶음은 "+N"
	/// 칸 전체가 버튼. 누르면 제약 설정 팝업 (UIDungeonConstraintPopup). 해금 안 된 제약은 꺼진 칩, 하나도 안 열렸으면 안내 글
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
		private const string USS_MORE = USS_CLASS + "__more";
		private const int CHIP_MAX = 3;
		private const string USS_LOCKED = USS_CLASS + "--locked";
		private const string EDIT_TEXT = "눌러서 설정";
		private const string LOCKED_TEXT = "조건을 채우면 열림";

		private Dungeon dungeon;
		private readonly Label riskLabel;
		private readonly VisualElement chipRow;
		private readonly Label editLabel;
		private readonly Label moreLabel;
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
			moreLabel = new Label { pickingMode = PickingMode.Ignore };
			moreLabel.AddToClassList(USS_MORE);
			chipRow.Add(moreLabel);
			editLabel = AddChild(this, new Label(EDIT_TEXT), USS_EDIT);
		}

		public void SetDungeon(Dungeon dungeon) => this.dungeon = dungeon;

		// 묶음마다 칩 하나. 켠 단계가 있으면 그 단계 아이콘과 수치, 없으면 흐린 아이콘만
		// 켠 묶음 먼저 (묶음 순서 유지), 다음 꺼진 묶음. 앞에서 CHIP_MAX 개만, 넘친 켠 묶음 수는 "+N"
		public void UpdateUI()
		{
			bool anyUnlocked = dungeon.UnlockedConstraints.Count > 0;
			EnableInClassList(USS_LOCKED, anyUnlocked == false);
			editLabel.text = anyUnlocked ? EDIT_TEXT : LOCKED_TEXT;

			List<(DungeonConstraint Shown, DungeonConstraint Selected)> entries = new();
			foreach (string family in DungeonConstraintRules.FamilyOrder(dungeon))
			{
				DungeonConstraint shown = null;
				DungeonConstraint selected = null;
				foreach (DungeonConstraint constraint in dungeon.Constraints)
				{
					if (constraint.FamilyKey != family)
						continue;
					shown ??= constraint;
					if (dungeon.IsConstraintUnlocked(constraint) && dungeon.ConstraintSelected[constraint.ID])
						selected = constraint;
				}
				entries.Add((shown, selected));
			}
			List<(DungeonConstraint Shown, DungeonConstraint Selected)> ordered = entries.FindAll(entry => entry.Selected != null);
			ordered.AddRange(entries.FindAll(entry => entry.Selected == null));

			int shownCount = Math.Min(CHIP_MAX, ordered.Count);
			EnsureChipCount(shownCount);
			for (int i = 0; i < chips.Count; i++)
			{
				bool isUsed = i < shownCount;
				chips[i].style.display = isUsed ? DisplayStyle.Flex : DisplayStyle.None;
				if (isUsed == false)
					continue;
				DungeonConstraint selected = ordered[i].Selected;
				DungeonConstraint target = selected ?? ordered[i].Shown;
				chipIcons[i].style.backgroundImage = new StyleBackground(target.Sprite);
				chipValues[i].text = selected != null ? selected.ShortValue : string.Empty;
				chipValues[i].style.display = selected != null ? DisplayStyle.Flex : DisplayStyle.None;
				chips[i].EnableInClassList(USS_CHIP_OFF, selected == null);
			}

			int hiddenSelected = 0;
			for (int i = shownCount; i < ordered.Count; i++)
			{
				if (ordered[i].Selected != null)
					hiddenSelected++;
			}
			moreLabel.text = "+" + hiddenSelected;
			moreLabel.style.display = hiddenSelected > 0 ? DisplayStyle.Flex : DisplayStyle.None;
			moreLabel.BringToFront();
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
