using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 아래 가운데 스킬창. 앞에 Shift 이동 스킬, 던전에서는 뒤에 보유 장비와 레벨 (사용자 2026-11-18 "스킬창에는 장비들")
	// 칸은 예전 uGUI 스킬 칸과 같은 마름모 (사용자 2026-11-18 "기존 마름모 디자인 처럼"). 마을은 UISkillBar, 던전은 UIDungeonRuntime 이 띄움
	// 클릭과 우클릭 스킬은 모든 장면에서 꺼져서 이 창에 없음. 스타일은 RunEquipmentHud.uss
	public class RunEquipmentHud : VisualElement
	{
		public const string USS_ROOT = "wm-run-equipment";
		public const string USS_SKILLS = USS_ROOT + "__skills";
		public const string USS_SLOTS = USS_ROOT + "__slots";
		public const string USS_SLOT = USS_ROOT + "__slot";
		public const string USS_SLOT_FILLED = USS_SLOT + "--filled";
		public const string USS_SLOT_SIGNATURE = USS_SLOT + "--signature";
		public const string USS_SLOT_SKILL = USS_SLOT + "--skill";
		public const string USS_SLOT_POP = USS_SLOT + "--pop";
		public const string USS_FRAME = USS_ROOT + "__frame";
		public const string USS_OUTLINE = USS_ROOT + "__outline";
		public const string USS_ICON = USS_ROOT + "__icon";
		public const string USS_LEVEL = USS_ROOT + "__level";
		public const string USS_LEVEL_EMPTY = USS_LEVEL + "--empty";
		public const string USS_COOLDOWN = USS_ROOT + "__cooldown";

		private readonly VisualElement skillRow = new() { name = "Skills", pickingMode = PickingMode.Ignore };
		private readonly VisualElement slotRow = new() { name = "Equipments", pickingMode = PickingMode.Ignore };
		private readonly List<HudSlot> skillSlots = new();
		private readonly List<HudSlot> slots = new();
		private readonly List<EquipmentData> shownEquipments = new();
		private readonly List<int> shownLevels = new();
		private readonly HashSet<int> pendingPops = new();
		private bool isVisible = true;
		private bool hasShownOnce;

		public VisualElement SlotRow => slotRow;
		public VisualElement SkillRow => skillRow;

		public RunEquipmentHud()
		{
			name = "RunEquipmentHud";
			pickingMode = PickingMode.Ignore;
			AddToClassList(USS_ROOT);
			skillRow.AddToClassList(USS_SKILLS);
			slotRow.AddToClassList(USS_SLOTS);
			Add(skillRow);
			Add(slotRow);
		}

		// 장비 목록은 덱 번호 순서, 0 번은 인형 고유 장비. null 은 빈 칸. slotCount 는 고유 장비 포함 전체 칸 수
		public void RefreshEquipments(IReadOnlyList<EquipmentData> equipments, IReadOnlyList<int> levels, int slotCount)
		{
			EnsureCount(slots, slotRow, slotCount, isSkill: false);

			for (int i = 0; i < slots.Count; i++)
			{
				EquipmentData equipment = i < equipments.Count ? equipments[i] : null;
				int level = equipment != null ? levels[i] : 0;
				EquipmentData previous = i < shownEquipments.Count ? shownEquipments[i] : null;
				int previousLevel = i < shownLevels.Count ? shownLevels[i] : 0;
				if (equipment == previous && level == previousLevel && hasShownOnce)
					continue;

				slots[i].SetEquipment(equipment, level);
				slots[i].Root.EnableInClassList(USS_SLOT_SIGNATURE, i == 0);
				// 처음 채울 때는 연출 없음. 판 도중 새로 생긴 장비만 튐
				if (hasShownOnce && equipment != null && previous == null)
					pendingPops.Add(i);
			}

			shownEquipments.Clear();
			shownLevels.Clear();
			for (int i = 0; i < slots.Count; i++)
			{
				shownEquipments.Add(i < equipments.Count ? equipments[i] : null);
				shownLevels.Add(i < equipments.Count && equipments[i] != null ? levels[i] : 0);
			}
			hasShownOnce = true;
			PlayPendingPops();
		}

		// 앞 칸 스킬. 남은 재사용 시간 비율만큼 아래에서 위로 어둡게 덮음
		public void RefreshSkills(IReadOnlyList<Skill> skills)
		{
			EnsureCount(skillSlots, skillRow, skills.Count, isSkill: true);
			for (int i = 0; i < skills.Count; i++)
				skillSlots[i].SetSkill(skills[i]);
		}

		// 새 판을 시작하면 첫 채움을 다시 연출 없이
		public void ResetShown()
		{
			shownEquipments.Clear();
			shownLevels.Clear();
			pendingPops.Clear();
			hasShownOnce = false;
		}

		public void SetVisible(bool visible)
		{
			isVisible = visible;
			style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
			PlayPendingPops();
		}

		// 카드 선택 중에는 창이 숨어 있어서 연출 보류, 다시 보일 때 재생
		private void PlayPendingPops()
		{
			if (isVisible == false || pendingPops.Count == 0)
				return;

			foreach (int index in pendingPops)
			{
				if (index < slots.Count)
					slots[index].Pop();
			}
			pendingPops.Clear();
		}

		private static void EnsureCount(List<HudSlot> list, VisualElement parent, int count, bool isSkill)
		{
			while (list.Count < count)
			{
				HudSlot slot = new(isSkill);
				list.Add(slot);
				parent.Add(slot.Root);
			}
			while (list.Count > count)
			{
				list[^1].Root.RemoveFromHierarchy();
				list.RemoveAt(list.Count - 1);
			}
		}

		private class HudSlot
		{
			public readonly VisualElement Root = new() { pickingMode = PickingMode.Ignore };
			// 45도 돌린 마름모 바탕. 쿨다운 덮개를 마름모 모양으로 자르고, 아이콘은 안에서 되돌려 바로 세움
			private readonly VisualElement frame = new() { pickingMode = PickingMode.Ignore };
			private readonly VisualElement outline = new() { pickingMode = PickingMode.Ignore };
			private readonly VisualElement icon = new() { pickingMode = PickingMode.Ignore };
			private readonly VisualElement cooldown = new() { pickingMode = PickingMode.Ignore };
			private readonly Label level = new() { pickingMode = PickingMode.Ignore };

			public HudSlot(bool isSkill)
			{
				Root.AddToClassList(USS_SLOT);
				Root.EnableInClassList(USS_SLOT_SKILL, isSkill);
				frame.AddToClassList(USS_FRAME);
				outline.AddToClassList(USS_OUTLINE);
				icon.AddToClassList(USS_ICON);
				cooldown.AddToClassList(USS_COOLDOWN);
				level.AddToClassList(USS_LEVEL);
				// 예전 칸과 같은 순서: 아이콘 위에 쿨다운 덮개
				frame.Add(icon);
				frame.Add(cooldown);
				Root.Add(frame);
				Root.Add(outline);
				Root.Add(level);
				// 튀는 연출은 USS 전환. 커진 뒤 전환이 끝나면 원래 크기로 돌아감
				Root.RegisterCallback<TransitionEndEvent>(_ => Root.RemoveFromClassList(USS_SLOT_POP));
				Root.RegisterCallback<TransitionCancelEvent>(_ => Root.RemoveFromClassList(USS_SLOT_POP));
			}

			public void SetEquipment(EquipmentData equipment, int levelValue)
			{
				bool isFilled = equipment != null;
				Root.EnableInClassList(USS_SLOT_FILLED, isFilled);
				icon.style.backgroundImage = isFilled ? new StyleBackground(equipment.Sprite) : new StyleBackground(StyleKeyword.None);
				SetLevel(isFilled ? levelValue.ToString() : string.Empty);
				cooldown.style.height = Length.Percent(0f);
			}

			public void SetSkill(Skill skill)
			{
				Root.AddToClassList(USS_SLOT_FILLED);
				icon.style.backgroundImage = new StyleBackground(skill.Data.Sprite);
				SetLevel(string.Empty);
				float ratio = skill.Cooldown.Base > 0f ? skill.Cooldown.Remain / skill.Cooldown.Base : 0f;
				cooldown.style.height = Length.Percent(ratio * 100f);
			}

			public void Pop() => Root.AddToClassList(USS_SLOT_POP);

			private void SetLevel(string text)
			{
				level.text = text;
				level.EnableInClassList(USS_LEVEL_EMPTY, string.IsNullOrEmpty(text));
			}
		}
	}
}
