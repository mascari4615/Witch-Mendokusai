using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 제약 설정 팝업. 제약 전체를 아이콘 칸 격자로, 칸 모서리에 위험도, 켠 칸은 표시
	/// 초점 칸의 효과 설명, 위험도 합계와 보상 변화, 전부 해제
	/// 키보드: 방향키 이동, 확인 키로 켜고 끔, 취소 키로 닫기 (입장 창 HandleCancelInput)
	/// 입장 창 전체 틀 (Root) 위에 덮음. 뒤는 어두운 막 (USS). BlurPanel 은 게임 화면을 흐려 그려 뒤 입장 창을 가려서 안 씀
	/// </summary>
	public class UIDungeonConstraintPopup : VisualElement
	{
		public const string USS_CLASS = "wm-constraint-popup";
		private const string USS_DIALOG = USS_CLASS + "__dialog";
		private const string USS_HEAD = USS_CLASS + "__head";
		private const string USS_TITLE = USS_CLASS + "__title";
		private const string USS_TOTAL = USS_CLASS + "__total";
		private const string USS_TOTAL_LABEL = USS_CLASS + "__total-label";
		private const string USS_TOTAL_VALUE = USS_CLASS + "__total-value";
		private const string USS_GRID = USS_CLASS + "__grid";
		private const string USS_TILE = USS_CLASS + "__tile";
		private const string USS_TILE_ON = USS_TILE + "--on";
		private const string USS_TILE_ICON = USS_CLASS + "__tile-icon";
		private const string USS_TILE_RISK = USS_CLASS + "__tile-risk";
		private const string USS_TILE_CHECK = USS_CLASS + "__tile-check";
		private const string USS_TILE_NAME = USS_CLASS + "__tile-name";
		private const string USS_DETAIL = USS_CLASS + "__detail";
		private const string USS_DETAIL_NAME = USS_CLASS + "__detail-name";
		private const string USS_DETAIL_STATE = USS_CLASS + "__detail-state";
		private const string USS_DETAIL_TEXT = USS_CLASS + "__detail-text";
		private const string USS_FOOT = USS_CLASS + "__foot";
		private const string USS_SUMMARY = USS_CLASS + "__summary";
		private const string USS_BUTTON = USS_CLASS + "__button";
		private const string USS_BUTTON_MAIN = USS_BUTTON + "--main";

		public event Action OnClosed = delegate { };
		/// <summary> 제약을 켜거나 끌 때마다. 뒤 입장 칸 칩을 바로 맞춤 </summary>
		public event Action OnChanged = delegate { };

		public bool IsOpen { get; private set; }

		private Dungeon dungeon;
		private int focusedIndex;
		private int lastTileIndex;
		private readonly VisualElement grid;
		private readonly Label totalValue;
		private readonly Label detailName;
		private readonly Label detailState;
		private readonly Label detailText;
		private readonly Label summary;
		private readonly Button clearButton;
		private readonly Button confirmButton;
		private readonly List<Button> tiles = new();
		private readonly List<VisualElement> tileIcons = new();
		private readonly List<Label> tileRisks = new();
		private readonly List<Label> tileNames = new();

		public UIDungeonConstraintPopup()
		{
			name = "ConstraintPopup";
			AddToClassList(USS_CLASS);
			AddToClassList(UIMotion.RISE);

			VisualElement center = AddChild(this, new VisualElement { pickingMode = PickingMode.Ignore }, "wm-space-center");
			VisualElement dialog = AddChild(center, new VisualElement(), USS_DIALOG);

			VisualElement head = AddChild(dialog, new VisualElement(), USS_HEAD);
			AddChild(head, new Label("제약 설정"), USS_TITLE);
			VisualElement total = AddChild(head, new VisualElement(), USS_TOTAL);
			AddChild(total, new Label("위험도"), USS_TOTAL_LABEL);
			totalValue = AddChild(total, new Label(), USS_TOTAL_VALUE);

			grid = AddChild(dialog, new VisualElement(), USS_GRID);

			VisualElement detail = AddChild(dialog, new VisualElement(), USS_DETAIL);
			VisualElement detailHead = AddChild(detail, new VisualElement(), USS_HEAD);
			detailName = AddChild(detailHead, new Label(), USS_DETAIL_NAME);
			detailState = AddChild(detailHead, new Label(), USS_DETAIL_STATE);
			detailText = AddChild(detail, new Label(), USS_DETAIL_TEXT);

			VisualElement foot = AddChild(dialog, new VisualElement(), USS_FOOT);
			summary = AddChild(foot, new Label(), USS_SUMMARY);
			clearButton = AddChild(foot, new Button(ClearAll) { name = "ConstraintClearButton", text = "전부 해제" }, USS_BUTTON);
			confirmButton = AddChild(foot, new Button(Close) { name = "ConstraintConfirmButton", text = "확인" }, USS_BUTTON);
			confirmButton.AddToClassList(USS_BUTTON_MAIN);
			clearButton.RegisterCallback<FocusInEvent>(_ => focusedIndex = dungeon.Constraints.Count);
			confirmButton.RegisterCallback<FocusInEvent>(_ => focusedIndex = dungeon.Constraints.Count + 1);

			RegisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
			// 막을 눌러도 초점이 팝업 밖으로 빠지지 않게 마지막 칸으로 되돌림
			RegisterCallback<PointerDownEvent>(evt =>
			{
				if (evt.target == this)
					FocusIndex(focusedIndex, false);
			});
			UIMotion.SetInstant(this, false);
		}

		public void Open(Dungeon dungeon)
		{
			this.dungeon = dungeon;
			EnsureTileCount(dungeon.Constraints.Count);
			IsOpen = true;
			UiSfx.Play(UiSfxEvent.WindowOpen);
			UIMotion.Show(this);
			lastTileIndex = 0;
			Refresh();
			FocusIndex(0, false);
		}

		public void Close()
		{
			if (IsOpen == false)
				return;
			UiSfx.Play(UiSfxEvent.WindowClose);
			Dismiss();
			OnClosed();
		}

		/// <summary> 소리와 닫힘 알림 없이 닫기. 입장 창 자체가 닫힐 때 </summary>
		public void Dismiss()
		{
			if (IsOpen == false)
				return;
			IsOpen = false;
			UIMotion.Hide(this);
		}

		private void Toggle(int index)
		{
			int id = dungeon.Constraints[index].ID;
			bool isOn = dungeon.ConstraintSelected[id] == false;
			dungeon.ConstraintSelected[id] = isOn;
			UiSfx.Play(isOn ? UiSfxEvent.Confirm : UiSfxEvent.Cancel);
			focusedIndex = index;
			Refresh();
			OnChanged();
		}

		private void ClearAll()
		{
			foreach (DungeonConstraint constraint in dungeon.Constraints)
				dungeon.ConstraintSelected[constraint.ID] = false;
			UiSfx.Play(UiSfxEvent.Cancel);
			Refresh();
			OnChanged();
		}

		private void Refresh()
		{
			for (int i = 0; i < tiles.Count; i++)
			{
				bool isUsed = i < dungeon.Constraints.Count;
				tiles[i].style.display = isUsed ? DisplayStyle.Flex : DisplayStyle.None;
				if (isUsed == false)
					continue;
				DungeonConstraint constraint = dungeon.Constraints[i];
				tileIcons[i].style.backgroundImage = new StyleBackground(constraint.Sprite);
				tileRisks[i].text = constraint.Risk.ToString();
				tileNames[i].text = constraint.Name;
				tiles[i].EnableInClassList(USS_TILE_ON, dungeon.ConstraintSelected[constraint.ID]);
			}

			int totalRisk = DungeonConstraintRisk.Total(dungeon);
			totalValue.text = totalRisk.ToString();
			// 제약은 보상 배율에 영향 없음 (현재 규칙). 위협 단계가 보상을 바꿈
			summary.text = "켠 제약 " + dungeon.SelectedConstraints().Count + "개, 보상 변화 없음";
			RefreshDetail();
		}

		private void RefreshDetail()
		{
			int index = Mathf.Clamp(focusedIndex, 0, dungeon.Constraints.Count - 1);
			DungeonConstraint constraint = dungeon.Constraints[index];
			bool isOn = dungeon.ConstraintSelected[constraint.ID];
			detailName.text = constraint.Name;
			detailState.text = isOn ? "켜짐" : "꺼짐";
			detailText.text = constraint.Description.Replace("\n", " ");
		}

		private void OnNavigationMove(NavigationMoveEvent evt)
		{
			int tileCount = dungeon.Constraints.Count;
			int lastIndex = tileCount + 1;
			int next = focusedIndex;
			switch (evt.direction)
			{
				case NavigationMoveEvent.Direction.Left:
				case NavigationMoveEvent.Direction.Previous:
					next = Mathf.Max(0, focusedIndex - 1);
					break;
				case NavigationMoveEvent.Direction.Right:
				case NavigationMoveEvent.Direction.Next:
					next = Mathf.Min(lastIndex, focusedIndex + 1);
					break;
				case NavigationMoveEvent.Direction.Down:
					next = focusedIndex < tileCount ? tileCount : focusedIndex;
					break;
				case NavigationMoveEvent.Direction.Up:
					next = focusedIndex >= tileCount ? lastTileIndex : focusedIndex;
					break;
				default:
					return;
			}
			// 팝업 밖 (뒤 입장 창) 으로 초점이 새지 않게 모든 이동을 여기서 끝냄
			evt.StopPropagation();
			focusController?.IgnoreEvent(evt);
			if (next != focusedIndex)
				FocusIndex(next, true);
		}

		private void FocusIndex(int index, bool withSound)
		{
			focusedIndex = index;
			int tileCount = dungeon.Constraints.Count;
			Button target = index < tileCount ? tiles[index] : index == tileCount ? clearButton : confirmButton;
			if (withSound)
				WMFocusNavigation.MoveFocus(target);
			else
				target.Focus();
			if (index < tileCount)
			{
				lastTileIndex = index;
				RefreshDetail();
			}
		}

		private void EnsureTileCount(int count)
		{
			while (tiles.Count < count)
			{
				int index = tiles.Count;
				Button tile = AddChild(grid, new Button(() => Toggle(index)) { name = "ConstraintTile" + index }, USS_TILE);
				tileIcons.Add(AddChild(tile, new VisualElement { pickingMode = PickingMode.Ignore }, USS_TILE_ICON));
				tileRisks.Add(AddChild(tile, new Label { pickingMode = PickingMode.Ignore }, USS_TILE_RISK));
				AddChild(tile, new VisualElement { pickingMode = PickingMode.Ignore }, USS_TILE_CHECK);
				tileNames.Add(AddChild(tile, new Label { pickingMode = PickingMode.Ignore }, USS_TILE_NAME));
				tile.RegisterCallback<FocusInEvent>(_ =>
				{
					focusedIndex = index;
					lastTileIndex = index;
					RefreshDetail();
				});
				// 마우스를 올린 칸이 곧 초점 칸. 확인 키와 설명이 같은 칸을 가리키게
				tile.RegisterCallback<PointerEnterEvent>(_ => tile.Focus());
				tiles.Add(tile);
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
