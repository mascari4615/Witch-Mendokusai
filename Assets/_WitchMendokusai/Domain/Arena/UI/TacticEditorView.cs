using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 전술 코딩 에디터(프리-매치) — 로스터 유닛의 TacticProgram 을 행 리스트로 편집.
	/// 편집 로직 = RowListAuthoring(검증됨), 렌더 = UIToolkit. UIRoot.ScreenLayer 부착(코드 빌드, SettingView 패턴).
	///
	/// 행 = [조건종류] ([연산자][값] | [슬롯]) [진영][우선순위][사거리] [행동] ([슬롯]) + [✕][↑][↓].
	/// 괄호 칸은 **그 종류가 실제로 읽을 때만** 뜬다 — 무엇을 읽는지는 <see cref="TacticSchema"/> 가 정하고,
	/// 그건 평가기(TacticConditions)와 같은 자리다. 안 그러면 「채운 값이 무시」되거나 반대로
	/// 「채울 칸이 없어 기본값으로 굳는」다. 실측으로 후자였다: 연산자/값 칸이 없어서 수치 조건이
	/// 전부 (Equal, 0) = 「HP 비율 == 0」 = 죽어야 참이 되어 영영 발동하지 않았다.
	///
	/// 인-매치 일시정지 재편집은 후속(같은 뷰 재사용). 위→아래 우선, 첫 충족 행 실행(UO/FF12).
	/// </summary>
	public class TacticEditorView
	{
		public sealed class Entry
		{
			public string Label;
			public RowListAuthoring Authoring;
		}

		// ★ 드롭다운 칸 번호 ≠ enum 값. 지금은 enum 이 0,1,2… 로 촘촘해서 우연히 같지만,
		//   이 enum 들은 **append-only 계약**이고(`TargetSide.EnemyObjective = 3 // append-only`)
		//   WM-165 는 「모드가 새 ConditionKind/ActionKind 를 등록」하는 걸 목표로 적어뒀다 —
		//   그때 값이 띄엄띄엄해지는 순간 칸 번호를 그대로 캐스팅하면 **엉뚱한 조건이 조용히 저장된다.**
		//   그래서 값 배열을 같이 들고 칸 번호로 *색인*한다(이름 배열과 순서가 같음이 보장된다).
		private static readonly ConditionKind[] ConditionValues = (ConditionKind[])Enum.GetValues(typeof(ConditionKind));
		private static readonly TargetPriority[] PriorityValues = (TargetPriority[])Enum.GetValues(typeof(TargetPriority));
		private static readonly ActionKind[] ActionValues = (ActionKind[])Enum.GetValues(typeof(ActionKind));
		private static readonly TargetSide[] SideValues = (TargetSide[])Enum.GetValues(typeof(TargetSide));
		private static readonly ComparisonOperator[] OperatorValues = (ComparisonOperator[])Enum.GetValues(typeof(ComparisonOperator));

		private static readonly string[] ConditionChoices = Enum.GetNames(typeof(ConditionKind));
		private static readonly string[] PriorityChoices = Enum.GetNames(typeof(TargetPriority));
		private static readonly string[] ActionChoices = Enum.GetNames(typeof(ActionKind));
		private static readonly string[] SideChoices = Enum.GetNames(typeof(TargetSide));
		private static readonly string[] OperatorChoices = Enum.GetNames(typeof(ComparisonOperator));

		// USS 클래스 이름(정본 = Domain/UI/Slot.uss § 전술 코딩 에디터).
		//
		// ★ 왜 코드에 수치가 없나: 이 패널은 순수 C# VisualElement 라 [SerializeField] 가 안 붙는다.
		//   그렇다고 리터럴을 코드에 두면 색·폭을 한 번 다듬을 때마다 재컴파일이다 — 아직 아무도
		//   이 패널이 렌더된 걸 본 적이 없어서(item 10) 첫 사람은 반드시 여러 번 다듬게 된다.
		//   시트는 UIRoot 가 rootVisualElement 에 붙여주고 이 패널은 그 아래 마운트된다 =
		//   **패널이 자기 시트를 로드하지 않는다**(경로 오타가 조용한 null 이 되는 길을 안 만든다).
		//   CLAUDE.md § 코드로 짓는 UIToolkit 은 USS 로 / TASK-WM-206 · WM-179.
		//
		// 이름이 .uss 에 실제로 있는지는 TacticEditorStyleTests 가 기계로 대조한다 —
		// 오타는 예외가 아니라 **그냥 안 예뻐지는** 실패라 눈으로는 못 잡는다.
		private const string CLASS_OVERLAY = "wm-tactic-overlay";
		private const string CLASS_PANEL = "wm-tactic-panel";
		private const string CLASS_TITLE = "wm-tactic-title";
		private const string CLASS_HINT = "wm-tactic-hint";
		private const string CLASS_ROW = "wm-tactic-row";
		private const string CLASS_ORDER = "wm-tactic-order";
		private const string CLASS_CONDITION = "wm-tactic-condition";
		private const string CLASS_OPERATOR = "wm-tactic-operator";
		private const string CLASS_VALUE = "wm-tactic-value";
		private const string CLASS_SLOT = "wm-tactic-slot";
		private const string CLASS_SIDE = "wm-tactic-side";
		private const string CLASS_PRIORITY = "wm-tactic-priority";
		private const string CLASS_RANGE = "wm-tactic-range";
		private const string CLASS_ACTION = "wm-tactic-action";
		private const string CLASS_BUTTON = "wm-tactic-button";
		private const string CLASS_FOOTER = "wm-tactic-footer";
		private const string CLASS_START = "wm-tactic-start";

		// 「그 종류가 안 쓰는 칸」은 지운다(Hidden 이 아니라 None — 자리도 안 차지해야 줄이 안 길어진다).
		private static void SetShown(VisualElement element, bool shown)
		{
			element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
		}

		// 값 → 칸 번호. 못 찾으면 0(첫 칸) — 저장된 값이 사라진 종류일 때 편집기가 안 죽게.
		private static int IndexOf<T>(T[] values, T value)
		{
			int found = Array.IndexOf(values, value);
			return found >= 0 ? found : 0;
		}

		private readonly VisualElement root;
		private readonly VisualElement rowsContainer;
		private readonly List<Entry> entries;
		private readonly Action onStart;
		private int selectedIndex;

		public TacticEditorView(VisualElement parentLayer, List<Entry> entries, Action onStart)
		{
			this.entries = entries ?? new List<Entry>();
			this.onStart = onStart;

			root = new VisualElement { name = nameof(TacticEditorView) };
			root.AddToClassList(CLASS_OVERLAY);
			parentLayer.Add(root);

			VisualElement panel = new VisualElement();
			panel.AddToClassList(CLASS_PANEL);
			root.Add(panel);

			Label title = new Label("전술 코딩 — 마계 투기장");
			title.AddToClassList(CLASS_TITLE);
			panel.Add(title);

			// 유닛 선택.
			List<string> unitLabels = new();
			foreach (Entry entry in this.entries)
				unitLabels.Add(entry.Label);
			DropdownField unitSelector = new DropdownField("유닛", unitLabels, this.entries.Count > 0 ? 0 : -1);
			unitSelector.RegisterValueChangedCallback(_ =>
			{
				selectedIndex = unitSelector.index;
				RebuildRows();
			});
			panel.Add(unitSelector);

			Label hint = new Label("위→아래 우선순위. 첫 충족 행 실행. 맨 아래 = fallback(항상) 권장. 칸은 고른 종류가 쓰는 것만 뜬다.");
			hint.AddToClassList(CLASS_HINT);
			panel.Add(hint);

			rowsContainer = new VisualElement();
			panel.Add(rowsContainer);

			// 하단 버튼.
			VisualElement footer = new VisualElement();
			footer.AddToClassList(CLASS_FOOTER);

			Button addButton = new Button(() => { CurrentAuthoring()?.AddRow(); RebuildRows(); }) { text = "+ 행 추가" };
			footer.Add(addButton);

			Button startButton = new Button(StartMatch) { text = "▶ 매치 시작" };
			startButton.AddToClassList(CLASS_START);
			footer.Add(startButton);

			panel.Add(footer);

			RebuildRows();
		}

		private RowListAuthoring CurrentAuthoring()
		{
			if (selectedIndex < 0 || selectedIndex >= entries.Count)
				return null;
			return entries[selectedIndex].Authoring;
		}

		private void RebuildRows()
		{
			rowsContainer.Clear();
			RowListAuthoring authoring = CurrentAuthoring();
			if (authoring == null)
				return;

			for (int i = 0; i < authoring.RowCount; i++)
				rowsContainer.Add(BuildRow(authoring, i));
		}

		private VisualElement BuildRow(RowListAuthoring authoring, int rowIndex)
		{
			TacticRule rule = authoring.Program.Rules[rowIndex];

			VisualElement row = new VisualElement();
			row.AddToClassList(CLASS_ROW);

			Label order = new Label((rowIndex + 1).ToString());
			order.AddToClassList(CLASS_ORDER);
			row.Add(order);

			TacticCondition condition = rule.Conditions.Count > 0 ? rule.Conditions[0] : new TacticCondition { Kind = ConditionKind.Always };

			DropdownField conditionDropdown = new DropdownField(new List<string>(ConditionChoices), IndexOf(ConditionValues, condition.Kind));
			conditionDropdown.AddToClassList(CLASS_CONDITION);
			row.Add(conditionDropdown);

			// --- 조건이 실제로 읽는 칸들. 안 읽는 종류에선 감춘다. ---
			//
			// ★ 이 칸들이 없던 동안 수치 조건은 전부 (Equal, 0) 으로 굳어 있었다 = 「HP 비율 == 0」,
			//   즉 죽어야 참. 영영 발동하지 않는 줄인데 화면에선 「그 줄이 안 먹네」로만 보인다.
			//   무엇을 보일지는 TacticSchema 가 정한다 — 평가기와 같은 자리를 봐야 어긋나지 않는다.
			DropdownField operatorDropdown = new DropdownField(new List<string>(OperatorChoices), IndexOf(OperatorValues, condition.Operator));
			operatorDropdown.AddToClassList(CLASS_OPERATOR);
			row.Add(operatorDropdown);

			FloatField valueField = new FloatField { value = condition.Value };
			valueField.AddToClassList(CLASS_VALUE);
			row.Add(valueField);

			IntegerField conditionSlotField = new IntegerField { value = condition.SkillSlot };
			conditionSlotField.AddToClassList(CLASS_SLOT);
			conditionSlotField.tooltip = "조건이 볼 스킬 슬롯";
			row.Add(conditionSlotField);

			// SetConditionThreshold / SetConditionSkillSlot 은 조건이 0개면 **조용히 아무것도 안 한다.**
			// 룰이 조건 없이 실려오면(저장본·SO 정의) 사용자가 값을 넣어도 안 먹는 상태가 된다.
			// SetConditionKind 는 없으면 만들어주는 멱등 연산이라, 먼저 한 번 불러 존재를 보장한다.
			void EnsureCondition()
			{
				authoring.SetConditionKind(rowIndex, ConditionValues[conditionDropdown.index]);
			}

			operatorDropdown.RegisterValueChangedCallback(_ =>
			{
				EnsureCondition();
				authoring.SetConditionThreshold(rowIndex, OperatorValues[operatorDropdown.index], valueField.value);
			});
			valueField.RegisterValueChangedCallback(evt =>
			{
				EnsureCondition();
				authoring.SetConditionThreshold(rowIndex, OperatorValues[operatorDropdown.index], evt.newValue);
			});
			conditionSlotField.RegisterValueChangedCallback(evt =>
			{
				EnsureCondition();
				authoring.SetConditionSkillSlot(rowIndex, Mathf.Max(0, evt.newValue));
			});

			// --- 타겟 질의: 진영 / 우선순위 / 사거리. ---
			// 진영이 Enemy 로 굳어 있던 동안 「가장 다친 아군을 치유」 같은 줄은 아예 쓸 수 없었다.
			DropdownField sideDropdown = new DropdownField(new List<string>(SideChoices), IndexOf(SideValues, rule.Target.Side));
			sideDropdown.AddToClassList(CLASS_SIDE);
			// ⚠ EnemyObjective 는 **목표물을 등록한 모드에서만** 후보가 생긴다. 등록처는 현재
			//   TowerDefenseMatch 뿐이고(전수 grep), 투기장은 아무것도 등록하지 않는다 →
			//   투기장에서 이걸 고르면 타겟이 영영 안 잡혀 그 줄은 조용히 안 먹는다.
			//   고를 수 있게는 두되(모드가 늘면 유효해진다) **왜 안 먹는지는 말해준다.**
			//   목록에서 빼지 않는 이유: 지금 빼면 WM-165 가 예고한 레인/넥서스 확장 때 되살릴 것을
			//   기억해야 하는데, 그런 「나중에 되돌릴 숨김」이 오늘 종일 파낸 사고의 모양이었다.
			sideDropdown.tooltip = "EnemyObjective = 목표물(코어·넥서스)을 등록하는 모드에서만 잡힌다. 투기장엔 아직 없다.";
			sideDropdown.RegisterValueChangedCallback(_ => authoring.SetTargetSide(rowIndex, SideValues[sideDropdown.index]));
			row.Add(sideDropdown);

			DropdownField priorityDropdown = new DropdownField(new List<string>(PriorityChoices), IndexOf(PriorityValues, rule.Target.Priority));
			priorityDropdown.AddToClassList(CLASS_PRIORITY);
			priorityDropdown.RegisterValueChangedCallback(_ => authoring.SetTargetPriority(rowIndex, PriorityValues[priorityDropdown.index]));
			row.Add(priorityDropdown);

			FloatField rangeField = new FloatField { value = rule.Target.MaxRange };
			rangeField.AddToClassList(CLASS_RANGE);
			// ⚠ 정지 거리가 아니라 **탐색 반경**이다. 헷갈려서 정지 거리로 쓰면 목표가 반경 밖일 때
			//   타겟이 아예 안 잡혀 유닛이 스폰 지점에 굳는다(TacticBTRunner 에 적힌 실측 회귀).
			rangeField.tooltip = "타겟 탐색 반경(0 = 무제한). 정지 거리가 아니다.";
			rangeField.RegisterValueChangedCallback(evt => authoring.SetTargetRange(rowIndex, Mathf.Max(0f, evt.newValue)));
			row.Add(rangeField);

			// --- 행동 + (UseSkill 일 때만) 슬롯. ---
			DropdownField actionDropdown = new DropdownField(new List<string>(ActionChoices), IndexOf(ActionValues, rule.Action.Kind));
			actionDropdown.AddToClassList(CLASS_ACTION);
			row.Add(actionDropdown);

			IntegerField actionSlotField = new IntegerField { value = rule.Action.SkillSlot };
			actionSlotField.AddToClassList(CLASS_SLOT);
			actionSlotField.tooltip = "시전할 스킬 슬롯";
			actionSlotField.RegisterValueChangedCallback(evt => authoring.SetActionSkillSlot(rowIndex, Mathf.Max(0, evt.newValue)));
			row.Add(actionSlotField);

			// 종류가 바뀌면 「그 종류가 쓰는 칸」도 같이 바뀐다. 행 전체를 다시 짓지 않고
			// 표시만 토글한다 — 다시 지으면 방금 만진 칸의 포커스가 튄다.
			void SyncConditionFields()
			{
				ConditionKind kind = ConditionValues[conditionDropdown.index];
				SetShown(operatorDropdown, TacticSchema.UsesThreshold(kind));
				SetShown(valueField, TacticSchema.UsesThreshold(kind));
				SetShown(conditionSlotField, TacticSchema.UsesSkillSlot(kind));
			}

			void SyncActionFields()
			{
				SetShown(actionSlotField, TacticSchema.UsesSkillSlot(ActionValues[actionDropdown.index]));
			}

			conditionDropdown.RegisterValueChangedCallback(_ =>
			{
				authoring.SetConditionKind(rowIndex, ConditionValues[conditionDropdown.index]);
				SyncConditionFields();
			});
			actionDropdown.RegisterValueChangedCallback(_ =>
			{
				authoring.SetActionKind(rowIndex, ActionValues[actionDropdown.index]);
				SyncActionFields();
			});

			SyncConditionFields();
			SyncActionFields();

			Button removeButton = new Button(() => { authoring.RemoveRow(rowIndex); RebuildRows(); }) { text = "✕" };
			removeButton.AddToClassList(CLASS_BUTTON);
			row.Add(removeButton);

			Button upButton = new Button(() => { authoring.MoveRow(rowIndex, -1); RebuildRows(); }) { text = "↑" };
			upButton.AddToClassList(CLASS_BUTTON);
			row.Add(upButton);

			Button downButton = new Button(() => { authoring.MoveRow(rowIndex, 1); RebuildRows(); }) { text = "↓" };
			downButton.AddToClassList(CLASS_BUTTON);
			row.Add(downButton);

			return row;
		}

		private void StartMatch()
		{
			Close();
			onStart?.Invoke();
		}

		public void Close()
		{
			root.RemoveFromHierarchy();
		}
	}
}
