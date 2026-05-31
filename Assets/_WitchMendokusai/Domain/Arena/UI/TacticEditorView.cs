using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 전술 코딩 에디터(프리-매치, v1) — 로스터 유닛의 TacticProgram 을 행 리스트(드롭다운)로 편집.
	/// 편집 로직 = RowListAuthoring(검증됨), 렌더 = UIToolkit. UIRoot.ScreenLayer 부착(코드 빌드, SettingView 패턴).
	/// 행 = [조건종류][타겟우선순위][행동종류] 드롭다운 + [✕][↑][↓]. v1 = 조건 op/값·스킬슬롯 UI 후속.
	/// 인-매치 일시정지 재편집은 후속(같은 뷰 재사용). 위→아래 우선, 첫 충족 행 실행(UO/FF12).
	/// </summary>
	public class TacticEditorView
	{
		public sealed class Entry
		{
			public string Label;
			public RowListAuthoring Authoring;
		}

		private static readonly string[] ConditionChoices = Enum.GetNames(typeof(ConditionKind));
		private static readonly string[] PriorityChoices = Enum.GetNames(typeof(TargetPriority));
		private static readonly string[] ActionChoices = Enum.GetNames(typeof(ActionKind));

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
			root.style.position = Position.Absolute;
			root.style.left = 0;
			root.style.top = 0;
			root.style.right = 0;
			root.style.bottom = 0;
			root.style.alignItems = Align.Center;
			root.style.justifyContent = Justify.Center;
			root.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
			parentLayer.Add(root);

			VisualElement panel = new VisualElement();
			panel.style.minWidth = 620;
			panel.style.paddingLeft = 16;
			panel.style.paddingRight = 16;
			panel.style.paddingTop = 12;
			panel.style.paddingBottom = 12;
			panel.style.backgroundColor = new Color(0.12f, 0.12f, 0.15f, 0.97f);
			panel.style.borderTopLeftRadius = 8;
			panel.style.borderTopRightRadius = 8;
			panel.style.borderBottomLeftRadius = 8;
			panel.style.borderBottomRightRadius = 8;
			root.Add(panel);

			Label title = new Label("전술 코딩 — 마계 투기장");
			title.style.unityFontStyleAndWeight = FontStyle.Bold;
			title.style.fontSize = 18;
			title.style.marginBottom = 8;
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

			Label hint = new Label("위→아래 우선순위. 첫 충족 행 실행. 맨 아래 = fallback(항상) 권장.");
			hint.style.fontSize = 11;
			hint.style.color = new Color(0.7f, 0.7f, 0.7f);
			hint.style.marginTop = 4;
			hint.style.marginBottom = 6;
			panel.Add(hint);

			rowsContainer = new VisualElement();
			panel.Add(rowsContainer);

			// 하단 버튼.
			VisualElement footer = new VisualElement();
			footer.style.flexDirection = FlexDirection.Row;
			footer.style.justifyContent = Justify.SpaceBetween;
			footer.style.marginTop = 10;

			Button addButton = new Button(() => { CurrentAuthoring()?.AddRow(); RebuildRows(); }) { text = "+ 행 추가" };
			footer.Add(addButton);

			Button startButton = new Button(StartMatch) { text = "▶ 매치 시작" };
			startButton.style.unityFontStyleAndWeight = FontStyle.Bold;
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
			row.style.flexDirection = FlexDirection.Row;
			row.style.alignItems = Align.Center;
			row.style.marginBottom = 3;

			Label order = new Label((rowIndex + 1).ToString());
			order.style.width = 22;
			row.Add(order);

			int conditionIndex = rule.Conditions.Count > 0 ? (int)rule.Conditions[0].Kind : (int)ConditionKind.Always;
			DropdownField conditionDropdown = new DropdownField(new List<string>(ConditionChoices), conditionIndex);
			conditionDropdown.style.width = 150;
			conditionDropdown.RegisterValueChangedCallback(_ => authoring.SetConditionKind(rowIndex, (ConditionKind)conditionDropdown.index));
			row.Add(conditionDropdown);

			DropdownField priorityDropdown = new DropdownField(new List<string>(PriorityChoices), (int)rule.Target.Priority);
			priorityDropdown.style.width = 130;
			priorityDropdown.RegisterValueChangedCallback(_ => authoring.SetTargetPriority(rowIndex, (TargetPriority)priorityDropdown.index));
			row.Add(priorityDropdown);

			DropdownField actionDropdown = new DropdownField(new List<string>(ActionChoices), (int)rule.Action.Kind);
			actionDropdown.style.width = 130;
			actionDropdown.RegisterValueChangedCallback(_ => authoring.SetActionKind(rowIndex, (ActionKind)actionDropdown.index));
			row.Add(actionDropdown);

			Button removeButton = new Button(() => { authoring.RemoveRow(rowIndex); RebuildRows(); }) { text = "✕" };
			removeButton.style.width = 28;
			row.Add(removeButton);

			Button upButton = new Button(() => { authoring.MoveRow(rowIndex, -1); RebuildRows(); }) { text = "↑" };
			upButton.style.width = 28;
			row.Add(upButton);

			Button downButton = new Button(() => { authoring.MoveRow(rowIndex, 1); RebuildRows(); }) { text = "↓" };
			downButton.style.width = 28;
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
