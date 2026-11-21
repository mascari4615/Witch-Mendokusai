using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 아래 가운데 스킬창. 앞에 스킬 칸 (Shift 이동, 좌클릭, 우클릭. 2026-11-21 클릭 공격 복구), 던전에서는 뒤에 보유 장비와 레벨 (사용자 2026-11-18 "스킬창에는 장비들")
	// 칸은 예전 uGUI 스킬 칸과 같은 마름모 (사용자 2026-11-18 "기존 마름모 디자인 처럼"). 마을은 UISkillBar, 던전은 UIDungeonRuntime 이 띄움. 스타일은 RunEquipmentHud.uss
	// 모든 칸이 같은 대기 표시 (아래서 차는 덮개, 남은 초, 준비 순간 번쩍임). 대기 없는 장비는 위 꼭지에 상시 (사용자 2026-11-21)
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
		public const string USS_COOLDOWN_CHARGE = USS_COOLDOWN + "--charge";
		public const string USS_REMAIN = USS_ROOT + "__remain";
		public const string USS_REMAIN_EMPTY = USS_REMAIN + "--empty";
		public const string USS_FLASH = USS_ROOT + "__flash";
		public const string USS_FLASH_ON = USS_FLASH + "--on";
		public const string USS_STATE = USS_ROOT + "__state";
		public const string USS_STATE_EMPTY = USS_STATE + "--empty";
		private const string ALWAYS_TEXT = "상시";

		// 이 주기보다 짧은 대기는 덮개만. 숫자와 번쩍임이 1초마다 깜박여 시끄러워서 (USS --wm-run-equipment-number-min-interval)
		private static readonly CustomStyleProperty<float> NUMBER_MIN_INTERVAL_PROPERTY = new("--wm-run-equipment-number-min-interval");
		// 번쩍임을 켜 둔 시간 (밀리초). 끈 뒤 사라지는 시간은 USS transition
		private static readonly CustomStyleProperty<int> FLASH_HOLD_MILLISECONDS_PROPERTY = new("--wm-run-equipment-flash-hold-milliseconds");
		// 1초 아래 남은 시간의 소수 자리 서식 (USS --wm-run-equipment-subsecond-format). 문화권 무관 점 표기
		private static readonly CustomStyleProperty<string> SUBSECOND_FORMAT_PROPERTY = new("--wm-run-equipment-subsecond-format");
		// USS 를 읽기 전에는 숫자와 번쩍임 없음 (덮개만)
		private float numberMinInterval = float.PositiveInfinity;
		private int flashHoldMilliseconds;
		private string subsecondFormat = string.Empty;

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
			RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
		}

		private void OnCustomStyleResolved(CustomStyleResolvedEvent customStyleEvent)
		{
			// USS 에 값이 없으면 바로 실패. 숫자를 코드에 따로 박지 않음
			if (customStyleEvent.customStyle.TryGetValue(NUMBER_MIN_INTERVAL_PROPERTY, out numberMinInterval) == false)
				throw new System.InvalidOperationException("RunEquipmentHud.uss 에 --wm-run-equipment-number-min-interval 없음");
			if (customStyleEvent.customStyle.TryGetValue(FLASH_HOLD_MILLISECONDS_PROPERTY, out flashHoldMilliseconds) == false)
				throw new System.InvalidOperationException("RunEquipmentHud.uss 에 --wm-run-equipment-flash-hold-milliseconds 없음");
			if (customStyleEvent.customStyle.TryGetValue(SUBSECOND_FORMAT_PROPERTY, out subsecondFormat) == false)
				throw new System.InvalidOperationException("RunEquipmentHud.uss 에 --wm-run-equipment-subsecond-format 없음");
		}

		// 시험과 USS 없는 화면용. 스타일 대신 값을 직접 지정
		public void SetCooldownDisplay(float minIntervalForNumber, int flashHoldTime, string subsecondNumberFormat)
		{
			numberMinInterval = minIntervalForNumber;
			flashHoldMilliseconds = flashHoldTime;
			subsecondFormat = subsecondNumberFormat;
		}

		private CooldownDisplay Display => new(numberMinInterval, flashHoldMilliseconds, subsecondFormat);

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

			// 대기 표시는 장비가 그대로여도 매번
			CooldownDisplay display = Display;
			for (int i = 0; i < slots.Count; i++)
			{
				EquipmentData equipment = shownEquipments[i];
				if (equipment == null)
					slots[i].ClearCooldown();
				else if (EquipmentCooldowns.TryGet(equipment, out CooldownState cooldownState))
					slots[i].ApplyCooldown(cooldownState, display);
				else
					slots[i].SetAlways();
			}
		}

		// 앞 칸 스킬. 장비 칸과 같은 대기 표시
		public void RefreshSkills(IReadOnlyList<Skill> skills)
		{
			EnsureCount(skillSlots, skillRow, skills.Count, isSkill: true);
			CooldownDisplay display = Display;
			for (int i = 0; i < skills.Count; i++)
				skillSlots[i].SetSkill(skills[i], display);
		}

		// 새 판을 시작하면 첫 채움을 다시 연출 없이. 지난 판 대기 기억도 지움 (첫 갱신 헛 번쩍임 방지)
		public void ResetShown()
		{
			shownEquipments.Clear();
			shownLevels.Clear();
			pendingPops.Clear();
			hasShownOnce = false;
			foreach (HudSlot slot in slots)
				slot.ClearCooldown();
			foreach (HudSlot slot in skillSlots)
				slot.ClearCooldown();
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

		// USS 에서 읽은 대기 표시 기준 묶음
		private readonly struct CooldownDisplay
		{
			public readonly float NumberMinInterval;
			public readonly int FlashHoldMilliseconds;
			public readonly string SubsecondFormat;

			public CooldownDisplay(float numberMinInterval, int flashHoldMilliseconds, string subsecondFormat)
			{
				NumberMinInterval = numberMinInterval;
				FlashHoldMilliseconds = flashHoldMilliseconds;
				SubsecondFormat = subsecondFormat;
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
			private readonly Label remain = new() { pickingMode = PickingMode.Ignore };
			private readonly VisualElement flash = new() { pickingMode = PickingMode.Ignore };
			private readonly Label state = new() { pickingMode = PickingMode.Ignore };
			// 지난 갱신의 남은 비율. 준비 순간 판정과 같은 값 다시 쓰기 생략
			private float previousRatio = -1f;
			private string shownRemainText = string.Empty;
			private UnityEngine.Sprite shownSkillSprite;

			public HudSlot(bool isSkill)
			{
				Root.AddToClassList(USS_SLOT);
				Root.EnableInClassList(USS_SLOT_SKILL, isSkill);
				frame.AddToClassList(USS_FRAME);
				outline.AddToClassList(USS_OUTLINE);
				icon.AddToClassList(USS_ICON);
				cooldown.AddToClassList(USS_COOLDOWN);
				level.AddToClassList(USS_LEVEL);
				remain.AddToClassList(USS_REMAIN);
				flash.AddToClassList(USS_FLASH);
				state.AddToClassList(USS_STATE);
				state.text = ALWAYS_TEXT;
				// 예전 칸과 같은 순서: 아이콘 위에 쿨다운 덮개
				frame.Add(icon);
				frame.Add(cooldown);
				frame.Add(flash);
				Root.Add(frame);
				Root.Add(outline);
				Root.Add(remain);
				Root.Add(level);
				Root.Add(state);
				ClearCooldown();
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
			}

			public void SetSkill(Skill skill, CooldownDisplay display)
			{
				Root.AddToClassList(USS_SLOT_FILLED);
				if (shownSkillSprite != skill.Data.Sprite)
				{
					shownSkillSprite = skill.Data.Sprite;
					icon.style.backgroundImage = new StyleBackground(shownSkillSprite);
				}
				SetLevel(string.Empty);
				float total = skill.Cooldown.Base;
				float remaining = skill.Cooldown.Remain;
				ApplyCooldown(new CooldownState(total > 0f ? remaining / total : 0f, remaining, total), display);
			}

			// 덮개 높이, 남은 초, 준비 순간 번쩍임. 충전형은 숫자 없이 덮개만 줄어듦
			public void ApplyCooldown(CooldownState cooldownState, CooldownDisplay display)
			{
				state.EnableInClassList(USS_STATE_EMPTY, true);
				cooldown.EnableInClassList(USS_COOLDOWN_CHARGE, cooldownState.IsCharge);
				float ratio = cooldownState.RemainingRatio;
				if (ratio != previousRatio)
					cooldown.style.height = Length.Percent(ratio * 100f);

				bool isLong = cooldownState.IsCharge == false && cooldownState.Interval >= display.NumberMinInterval;
				bool showNumber = isLong && ratio > 0f;
				remain.EnableInClassList(USS_REMAIN_EMPTY, showNumber == false);
				if (showNumber)
				{
					string text = FormatSeconds(cooldownState.RemainingSeconds, display.SubsecondFormat);
					if (text != shownRemainText)
					{
						shownRemainText = text;
						remain.text = text;
					}
				}

				// 준비 순간: 0 에 닿았거나, 갱신 간격 사이에 0 을 지나 다음 주기가 시작됨 (비율이 다시 커짐)
				// 주기형 무기는 준비된 그 프레임에 다음 주기를 잡아서 0 을 거의 못 봄. 짧은 주기는 번쩍임 생략 (덮개만)
				bool becameReady = previousRatio > 0f && (ratio == 0f || ratio > previousRatio);
				if (becameReady && (isLong || cooldownState.IsCharge))
					Flash(display.FlashHoldMilliseconds);
				previousRatio = ratio;
			}

			// 대기 없이 늘 붙어 있는 장비. 위 꼭지에 상시
			public void SetAlways()
			{
				ClearCooldown();
				state.EnableInClassList(USS_STATE_EMPTY, false);
			}

			public void ClearCooldown()
			{
				cooldown.style.height = Length.Percent(0f);
				remain.EnableInClassList(USS_REMAIN_EMPTY, true);
				state.EnableInClassList(USS_STATE_EMPTY, true);
				previousRatio = -1f;
			}

			// 켠 뒤 잠시 두고 끄면 USS transition 으로 사라짐
			private void Flash(int holdMilliseconds)
			{
				flash.AddToClassList(USS_FLASH_ON);
				flash.schedule.Execute(() => flash.RemoveFromClassList(USS_FLASH_ON)).ExecuteLater(holdMilliseconds);
			}

			// 1초 이상은 올림한 정수 (초 단위 표기), 1초 아래는 USS 서식
			private static string FormatSeconds(float seconds, string subsecondFormat)
				=> seconds >= 1f
					? UnityEngine.Mathf.CeilToInt(seconds).ToString(CultureInfo.InvariantCulture)
					: seconds.ToString(subsecondFormat, CultureInfo.InvariantCulture);

			public void Pop() => Root.AddToClassList(USS_SLOT_POP);

			private void SetLevel(string text)
			{
				level.text = text;
				level.EnableInClassList(USS_LEVEL_EMPTY, string.IsNullOrEmpty(text));
			}
		}
	}
}
