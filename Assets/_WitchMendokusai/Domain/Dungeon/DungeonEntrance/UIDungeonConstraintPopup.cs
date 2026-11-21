using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 제약 설정 창. 입장 창 틀 전체를 덮는 화면
	/// - 왼쪽: 제목, 위험도 큰 숫자와 눈금, 최고 기록, 위험도 단계 첫 클리어 보상 (DungeonConstraintRewardRule, 받음 / 이번에 받음 / 못 받음)
	/// - 오른쪽: 제약 표 (줄은 단계, 열은 묶음, 같은 열은 하나만), 초점 칸 설명, 켠 제약 효과 목록, 닫기, 전부 해제, 저장
	/// 고치는 중 값은 사본. 저장만 던전 값에 씀. 닫기와 취소 키는 버림, 바꾼 게 있으면 확인창
	/// </summary>
	public class UIDungeonConstraintPopup : VisualElement
	{
		public const string USS_CLASS = "wm-constraint-popup";
		private const string USS_PART = USS_CLASS + "__";
		// 표 격자 최소 크기. 제약이 적어도 빈 칸으로 표 모양 유지. 열 수는 USS main 폭 (760) 과 짝, 줄 수는 DungeonConstraint.Level 범위
		private const int MIN_COLUMNS = 6;
		private const int MIN_ROWS = 3;

		public event Action OnSaved = delegate { };
		public event Action OnClosed = delegate { };

		public bool IsOpen { get; private set; }

		private sealed class Cell
		{
			public DungeonConstraint Constraint;
			public Button Tile;
			public VisualElement Icon;
			public Label Name;
			public int Row;
			public int Column;
		}

		private Dungeon dungeon;
		private readonly Dictionary<int, bool> draft = new();
		private readonly List<Cell> cells = new();
		private readonly List<Button> footButtons = new();
		private Cell[,] grid = new Cell[0, 0];
		private int focusedOrder;
		private Cell lastCell;

		private readonly Label dungeonName;
		private readonly Label riskValue;
		private readonly VisualElement riskMeter;
		private readonly Label bestRisk;
		private readonly ScrollView rewardList;
		private readonly ScrollView tableScroll;
		private readonly VisualElement table;
		private readonly Label detailName;
		private readonly Label detailLevel;
		private readonly Label detailState;
		private readonly Label detailText;
		private readonly VisualElement effectList;
		private readonly Label effectEmpty;
		private readonly UIConfirmDialog confirm;
		private readonly VisualElement safeFrame;

		public UIDungeonConstraintPopup()
		{
			name = "ConstraintPopup";
			AddToClassList(USS_CLASS);
			AddToClassList(UIMotion.RISE);

			// 막은 화면 끝까지, 내용은 안전 영역 (입장 창 Content 와 같은 칸, SetSafeRect)
			safeFrame = AddChild(this, new VisualElement { pickingMode = PickingMode.Ignore }, USS_PART + "safe");
			VisualElement body = AddChild(safeFrame, new VisualElement { pickingMode = PickingMode.Ignore }, "wm-space-menu");
			body.AddToClassList(USS_PART + "body");

			// 왼쪽 정보 기둥
			VisualElement side = AddChild(body, new VisualElement(), USS_PART + "side");
			AddChild(side, new Label("제약 설정"), USS_PART + "title");
			dungeonName = AddChild(side, new Label(), USS_PART + "dungeon");
			VisualElement riskCard = AddChild(side, new VisualElement(), USS_PART + "risk");
			AddChild(riskCard, new Label("위험도"), USS_PART + "risk-label");
			riskValue = AddChild(riskCard, new Label(), USS_PART + "risk-value");
			riskMeter = AddChild(riskCard, new VisualElement(), USS_PART + "risk-meter");
			bestRisk = AddChild(riskCard, new Label(), USS_PART + "best");
			AddChild(side, new Label("위험도 보상"), USS_PART + "side-header");
			// 구간이 많으면 이 목록만 스크롤
			rewardList = AddChild(side, new ScrollView(ScrollViewMode.Vertical), USS_PART + "rewards");

			// 오른쪽 제약 표와 효과
			VisualElement main = AddChild(body, new VisualElement(), USS_PART + "main");
			VisualElement tableHead = AddChild(main, new VisualElement(), USS_PART + "head");
			AddChild(tableHead, new Label("제약 목록"), USS_PART + "header");
			AddChild(tableHead, new Label("확인 키로 켜고 끄기, 같은 열은 하나만"), USS_PART + "hint");
			// 묶음이 6 열을 넘으면 표만 가로 스크롤. 초점 칸은 보이는 자리로 (OnCellFocused)
			tableScroll = AddChild(main, new ScrollView(ScrollViewMode.Horizontal), USS_PART + "table-scroll");
			table = AddChild(tableScroll, new VisualElement(), USS_PART + "table");

			VisualElement detail = AddChild(main, new VisualElement(), USS_PART + "detail");
			VisualElement detailHead = AddChild(detail, new VisualElement(), USS_PART + "detail-head");
			detailName = AddChild(detailHead, new Label(), USS_PART + "detail-name");
			detailLevel = AddChild(detailHead, new Label(), USS_PART + "level-badge");
			detailState = AddChild(detailHead, new Label(), USS_PART + "detail-state");
			detailText = AddChild(detail, new Label(), USS_PART + "detail-text");

			VisualElement effectHead = AddChild(main, new VisualElement(), USS_PART + "head");
			effectHead.AddToClassList(USS_PART + "head--effects");
			AddChild(effectHead, new Label("켠 제약"), USS_PART + "header");
			effectList = AddChild(main, new VisualElement(), USS_PART + "effects");
			effectEmpty = AddChild(effectList, new Label("켠 제약 없음"), USS_PART + "effect-empty");

			VisualElement foot = AddChild(main, new VisualElement(), USS_PART + "foot");
			footButtons.Add(AddFootButton(foot, "ConstraintCloseButton", "닫기", RequestClose, false));
			footButtons.Add(AddFootButton(foot, "ConstraintClearButton", "전부 해제", ClearAll, false));
			footButtons.Add(AddFootButton(foot, "ConstraintSaveButton", "저장", SaveAndClose, true));

			confirm = AddChild(this, new UIConfirmDialog(), USS_PART + "confirm");
			for (int i = 0; i < footButtons.Count; i++)
			{
				int footIndex = i;
				footButtons[i].RegisterCallback<FocusInEvent>(_ => focusedOrder = cells.Count + footIndex);
			}

			RegisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
			// 창 안 어디를 눌러도 초점은 창 안에. 누른 뒤 초점이 창 밖이면 마지막 자리로
			RegisterCallback<PointerUpEvent>(_ => schedule.Execute(KeepFocusInside));
			UIMotion.SetInstant(this, false);
		}

		/// <summary> 내용 칸을 안전 영역에 맞춤. 입장 창이 Content 크기가 바뀔 때마다 넘김 </summary>
		public void SetSafeRect(Rect rect)
		{
			safeFrame.style.left = rect.x;
			safeFrame.style.top = rect.y;
			safeFrame.style.width = rect.width;
			safeFrame.style.height = rect.height;
		}

		private void KeepFocusInside()
		{
			if (IsOpen == false || confirm.IsOpen)
				return;
			VisualElement focused = focusController?.focusedElement as VisualElement;
			if (focused == null || Contains(focused) == false)
				FocusOrder(focusedOrder, false);
		}

		public bool IsDirty
		{
			get
			{
				foreach (KeyValuePair<int, bool> entry in draft)
				{
					if (dungeon.ConstraintSelected[entry.Key] != entry.Value)
						return true;
				}
				return false;
			}
		}

		public void Open(Dungeon dungeon, string title)
		{
			this.dungeon = dungeon;
			draft.Clear();
			foreach (DungeonConstraint constraint in dungeon.Constraints)
				draft[constraint.ID] = dungeon.ConstraintSelected[constraint.ID];
			dungeonName.text = title;
			BuildTable();
			IsOpen = true;
			UiSfx.Play(UiSfxEvent.WindowOpen);
			UIMotion.Show(this);
			Refresh();
			FocusOrder(0, false);
			// 같은 프레임에 켠 요소는 초점을 못 받을 수 있어 다음 프레임에 한 번 더
			schedule.Execute(() =>
			{
				if (IsOpen && confirm.IsOpen == false)
					FocusOrder(focusedOrder, false);
			});
		}

		/// <summary> 취소 키. 확인창이 있으면 확인창 "계속 편집", 없으면 닫기 요청 </summary>
		public void HandleCancel()
		{
			if (confirm.IsOpen)
				confirm.Cancel();
			else
				RequestClose();
		}

		/// <summary> 소리와 알림 없이 버리고 닫기. 입장 창 자체가 닫힐 때 </summary>
		public void Dismiss()
		{
			if (IsOpen == false)
				return;
			IsOpen = false;
			confirm.Dismiss();
			UIMotion.Hide(this);
		}

		private void RequestClose()
		{
			if (IsDirty == false)
			{
				Close();
				return;
			}
			UiSfx.Play(UiSfxEvent.WindowOpen);
			confirm.Open("저장하지 않고 닫을까요?", "바꾼 제약이 저장되지 않습니다.",
				new UIConfirmDialog.Choice("저장", SaveAndClose, true),
				new UIConfirmDialog.Choice("버리고 닫기", Close),
				new UIConfirmDialog.Choice("계속 편집", () => FocusOrder(focusedOrder, false)));
		}

		private void Close()
		{
			if (IsOpen == false)
				return;
			IsOpen = false;
			UiSfx.Play(UiSfxEvent.WindowClose);
			UIMotion.Hide(this);
			OnClosed();
		}

		private void SaveAndClose()
		{
			foreach (KeyValuePair<int, bool> entry in draft)
				dungeon.ConstraintSelected[entry.Key] = entry.Value;
			OnSaved();
			Close();
		}

		private void Toggle(Cell cell)
		{
			bool isOn = draft[cell.Constraint.ID] == false;
			DungeonConstraintRules.Set(dungeon, draft, cell.Constraint, isOn);
			UiSfx.Play(isOn ? UiSfxEvent.Confirm : UiSfxEvent.Cancel);
			Refresh();
		}

		private void ClearAll()
		{
			foreach (DungeonConstraint constraint in dungeon.Constraints)
				draft[constraint.ID] = false;
			UiSfx.Play(UiSfxEvent.Cancel);
			Refresh();
		}

		// 줄은 있는 단계, 열은 묶음. 데이터만 바꾸면 표가 따라감
		private void BuildTable()
		{
			table.Clear();
			cells.Clear();
			List<string> families = DungeonConstraintRules.FamilyOrder(dungeon);
			int maxLevel = MIN_ROWS;
			foreach (DungeonConstraint constraint in dungeon.Constraints)
				maxLevel = Mathf.Max(maxLevel, constraint.Level);
			List<int> levels = new();
			for (int level = 1; level <= maxLevel; level++)
				levels.Add(level);
			int columnCount = Mathf.Max(MIN_COLUMNS, families.Count);
			grid = new Cell[levels.Count, columnCount];

			for (int row = 0; row < levels.Count; row++)
			{
				VisualElement line = AddChild(table, new VisualElement(), USS_PART + "row");
				VisualElement rowHead = AddChild(line, new VisualElement(), USS_PART + "row-head");
				AddChild(rowHead, new Label("단계"), USS_PART + "row-head-label");
				AddChild(rowHead, new Label(levels[row].ToString()), USS_PART + "row-head-value");
				for (int column = 0; column < columnCount; column++)
				{
					// 묶음 없는 열은 흐린 빈 칸 (격자 모양)
					if (column >= families.Count)
					{
						AddChild(line, new VisualElement(), USS_PART + "cell-empty");
						continue;
					}
					List<DungeonConstraint> matches = dungeon.Constraints.FindAll(candidate => candidate.Level == levels[row] && candidate.FamilyKey == families[column]);
					if (matches.Count > 1)
						Debug.LogError($"제약 묶음 {families[column]} 단계 {levels[row]} 이 {matches.Count}개. 표에는 첫 에셋만 ({dungeon.name})");
					DungeonConstraint constraint = matches.Count > 0 ? matches[0] : null;
					if (constraint == null)
					{
						AddChild(line, new VisualElement(), USS_PART + "cell-empty");
						continue;
					}
					Cell cell = new Cell { Constraint = constraint, Row = row, Column = column };
					cell.Tile = AddChild(line, new Button(() => Toggle(cell)) { name = "ConstraintTile" + constraint.ID }, USS_PART + "tile");
					cell.Icon = AddChild(cell.Tile, new VisualElement { pickingMode = PickingMode.Ignore }, USS_PART + "tile-icon");
					cell.Icon.style.backgroundImage = new StyleBackground(constraint.Sprite);
					AddChild(cell.Tile, new VisualElement { pickingMode = PickingMode.Ignore }, USS_PART + "tile-check");
					AddChild(cell.Tile, new Label("배타") { pickingMode = PickingMode.Ignore }, USS_PART + "tile-block");
					cell.Name = AddChild(cell.Tile, new Label(constraint.Name) { pickingMode = PickingMode.Ignore }, USS_PART + "tile-name");
					cell.Tile.RegisterCallback<FocusInEvent>(_ => OnCellFocused(cell));
					// 마우스를 올린 칸은 설명만 보여 줌. 초점과 확인 키 대상은 그대로, 벗어나면 초점 칸 설명
					cell.Tile.RegisterCallback<PointerEnterEvent>(_ => RefreshDetail(cell));
					cell.Tile.RegisterCallback<PointerLeaveEvent>(_ =>
					{
						if (lastCell != null)
							RefreshDetail(lastCell);
					});
					grid[row, column] = cell;
					cells.Add(cell);
				}
			}
			lastCell = cells.Count > 0 ? cells[0] : null;
		}

		private void Refresh()
		{
			foreach (Cell cell in cells)
			{
				bool isOn = draft[cell.Constraint.ID];
				cell.Tile.EnableInClassList(USS_PART + "tile--on", isOn);
				cell.Tile.EnableInClassList(USS_PART + "tile--blocked", isOn == false && DungeonConstraintRules.IsBlockedBySibling(dungeon, draft, cell.Constraint));
			}

			int risk = DungeonConstraintRules.TotalRisk(dungeon, draft);
			int maxRisk = DungeonConstraintRules.MaxRisk(dungeon);
			riskValue.text = risk.ToString();
			riskMeter.Clear();
			for (int i = 0; i < maxRisk; i++)
				AddChild(riskMeter, new VisualElement(), USS_PART + "risk-pip").EnableInClassList(USS_PART + "risk-pip--on", i < risk);

			RefreshRewards(risk);
			RefreshEffects();
			if (lastCell != null)
				RefreshDetail(lastCell);
		}

		private void RefreshRewards(int risk)
		{
			rewardList.Clear();
			bestRisk.text = dungeon.IsCleared ? "최고 기록 위험도 " + dungeon.BestRisk : "클리어 기록 없음";
			DungeonConstraintRewardRule rule = dungeon.ConstraintRewardRule;
			if (rule == null || rule.Milestones.Count == 0)
			{
				AddChild(rewardList, new Label("이 던전은 위험도 보상 없음. 최고 기록만 남음"), USS_PART + "reward-empty");
				return;
			}
			foreach (DungeonConstraintRewardRule.RiskMilestone milestone in rule.Milestones)
			{
				DungeonRiskMilestoneState state = DungeonRiskRewardRules.State(milestone.Risk, dungeon.ClaimedRiskMilestones, risk);
				VisualElement line = AddChild(rewardList, new VisualElement(), USS_PART + "reward");
				line.EnableInClassList(USS_PART + "reward--reached", state == DungeonRiskMilestoneState.ThisClear);
				line.EnableInClassList(USS_PART + "reward--claimed", state == DungeonRiskMilestoneState.Claimed);
				VisualElement head = AddChild(line, new VisualElement(), USS_PART + "reward-head");
				AddChild(head, new Label("위험도 " + milestone.Risk), USS_PART + "reward-risk");
				AddChild(head, new Label(StateText(state)), USS_PART + "reward-state");
				UIRewardsToolkit rewards = AddChild(line, new UIRewardsToolkit(), USS_PART + "reward-items");
				rewards.UpdateUI(milestone.Rewards);
			}
			// 마지막 구간 위는 상한 없는 도전. 보상 없이 기록만
			int top = DungeonRiskRewardRules.TopMilestone(rule.MilestoneRisks());
			AddChild(rewardList, new Label($"위험도 {top} 초과는 보상 없이 기록만"), USS_PART + "reward-empty");
		}

		private static string StateText(DungeonRiskMilestoneState state) => state switch
		{
			DungeonRiskMilestoneState.Claimed => "받음",
			DungeonRiskMilestoneState.ThisClear => "이번 클리어로",
			_ => string.Empty,
		};

		private void RefreshEffects()
		{
			effectList.Clear();
			bool hasAny = false;
			foreach (DungeonConstraint constraint in dungeon.Constraints)
			{
				if (draft[constraint.ID] == false)
					continue;
				hasAny = true;
				VisualElement line = AddChild(effectList, new VisualElement(), USS_PART + "effect");
				AddChild(line, new Label(constraint.Level.ToString()), USS_PART + "level-badge");
				VisualElement icon = AddChild(line, new VisualElement(), USS_PART + "effect-icon");
				icon.style.backgroundImage = new StyleBackground(constraint.Sprite);
				VisualElement texts = AddChild(line, new VisualElement(), USS_PART + "effect-texts");
				VisualElement head = AddChild(texts, new VisualElement(), USS_PART + "effect-head");
				AddChild(head, new Label(constraint.Name), USS_PART + "effect-name");
				AddChild(head, new Label(constraint.ShortValue), USS_PART + "effect-value");
			}
			if (hasAny == false)
				effectList.Add(effectEmpty);
		}

		private void OnCellFocused(Cell cell)
		{
			focusedOrder = cells.IndexOf(cell);
			lastCell = cell;
			tableScroll.ScrollTo(cell.Tile);
			RefreshDetail(cell);
		}

		private void RefreshDetail(Cell cell)
		{
			bool isOn = draft[cell.Constraint.ID];
			bool isBlocked = isOn == false && DungeonConstraintRules.IsBlockedBySibling(dungeon, draft, cell.Constraint);
			detailName.text = cell.Constraint.Name;
			detailLevel.text = cell.Constraint.Level.ToString();
			detailState.text = isOn ? "켜짐" : isBlocked ? "같은 열 다른 단계 켜짐" : "꺼짐";
			detailText.text = cell.Constraint.Description.Replace("\n", " ");
		}

		// 표 안은 줄과 열, 맨 아래 줄에서 아래로 가면 버튼 줄. Tab 은 순서대로. 끝에서 멈춤
		private void OnNavigationMove(NavigationMoveEvent evt)
		{
			if (confirm.IsOpen)
				return;
			int next = focusedOrder;
			bool onFoot = focusedOrder >= cells.Count;
			Cell current = onFoot ? null : cells[focusedOrder];
			switch (evt.direction)
			{
				case NavigationMoveEvent.Direction.Previous:
					next = Mathf.Max(0, focusedOrder - 1);
					break;
				case NavigationMoveEvent.Direction.Next:
					next = Mathf.Min(cells.Count + footButtons.Count - 1, focusedOrder + 1);
					break;
				case NavigationMoveEvent.Direction.Left:
					next = onFoot ? Mathf.Max(cells.Count, focusedOrder - 1) : OrderOf(FindInRow(current.Row, current.Column, -1)) ?? focusedOrder;
					break;
				case NavigationMoveEvent.Direction.Right:
					next = onFoot ? Mathf.Min(cells.Count + footButtons.Count - 1, focusedOrder + 1) : OrderOf(FindInRow(current.Row, current.Column, 1)) ?? focusedOrder;
					break;
				case NavigationMoveEvent.Direction.Up:
					next = onFoot ? (lastCell != null ? cells.IndexOf(lastCell) : focusedOrder) : OrderOf(FindInColumn(current.Row, current.Column, -1)) ?? focusedOrder;
					break;
				case NavigationMoveEvent.Direction.Down:
					next = onFoot ? focusedOrder : OrderOf(FindInColumn(current.Row, current.Column, 1)) ?? cells.Count;
					break;
				default:
					return;
			}
			// 창 밖 (뒤 입장 창) 으로 초점이 새지 않게 모든 이동을 여기서 끝냄
			evt.StopPropagation();
			focusController?.IgnoreEvent(evt);
			if (next != focusedOrder)
				FocusOrder(next, true);
		}

		private int? OrderOf(Cell cell) => cell == null ? null : cells.IndexOf(cell);

		private Cell FindInRow(int row, int column, int step)
		{
			for (int scanColumn = column + step; scanColumn >= 0 && scanColumn < grid.GetLength(1); scanColumn += step)
			{
				if (grid[row, scanColumn] != null)
					return grid[row, scanColumn];
			}
			return null;
		}

		// 위아래 줄에서 같은 열, 없으면 가장 가까운 열
		private Cell FindInColumn(int row, int column, int step)
		{
			for (int scanRow = row + step; scanRow >= 0 && scanRow < grid.GetLength(0); scanRow += step)
			{
				Cell best = null;
				int bestDistance = int.MaxValue;
				for (int scanColumn = 0; scanColumn < grid.GetLength(1); scanColumn++)
				{
					if (grid[scanRow, scanColumn] == null)
						continue;
					int distance = Mathf.Abs(scanColumn - column);
					if (distance < bestDistance)
					{
						best = grid[scanRow, scanColumn];
						bestDistance = distance;
					}
				}
				if (best != null)
					return best;
			}
			return null;
		}

		private void FocusOrder(int order, bool withSound)
		{
			order = Mathf.Clamp(order, 0, cells.Count + footButtons.Count - 1);
			focusedOrder = order;
			Button target = order < cells.Count ? cells[order].Tile : footButtons[order - cells.Count];
			if (withSound)
				WMFocusNavigation.MoveFocus(target);
			else
				target.Focus();
		}

		private Button AddFootButton(VisualElement parent, string buttonName, string text, Action onClick, bool isMain)
		{
			Button button = AddChild(parent, new Button(onClick) { name = buttonName, text = text }, USS_PART + "button");
			button.EnableInClassList(USS_PART + "button--main", isMain);
			return button;
		}

		private static T AddChild<T>(VisualElement parent, T child, string ussClass) where T : VisualElement
		{
			child.AddToClassList(ussClass);
			parent.Add(child);
			return child;
		}
	}
}
