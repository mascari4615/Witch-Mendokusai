using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 던전 입장 NPC 패널 — uGUI UIDungeonEntrance(133L, :UIPanel) 의 Toolkit 병렬 신설
	/// (TASK-WM-113 S3-E). S2 `UIToolkitPanel` base 재사용 + S3-A `ToolkitSlot`(던전선택)
	/// + S3-C `UIRewardsToolkit` + S3-D `UIDungeonConstraintToolkit` + WM-133 panel-root
	/// owner-push `UIServices.Tooltip.ShowAnchored`(역할② 선택던전 상세) 조립. 구
	/// UIDungeonEntrance 의 데이터/로직 1:1 보존(prefab serialized uGUI ref → 코드
	/// VisualElement 트리, 명시 Navigation 직역 X=Toolkit 포커스링 S3-A 결정, 하드코딩
	/// 10버튼 → dungeons.Count lazy). 툴팁 = WM-133 신패턴(static 삭제 → selectedSlot
	/// (VisualElement).GetUIServices().Tooltip, Slot.cs/DevItemSlot.cs 동형). 구
	/// UIDungeonEntrance 잔존(UINPC 가 여전히 실사용 — S3-F 에서 엔트리 교체, E deletion
	/// 최후). **시각 폴리시(USS 레이아웃·anchored 위치 미세)는 S3-E/F Play 시각검증
	/// (사용자 영역, WM-113 로드맵 명시) — 본 클래스는 구조·데이터흐름·compile.**
	/// </summary>
	public class UIDungeonEntranceToolkit : UIToolkitPanel
	{
		public override string Name => "던전 입장";
		public override bool IsFullscreen => true;

		private readonly List<ToolkitSlot> dungeonSelectButtons = new();
		private UIRewardsToolkit rewardUI;
		private UIDungeonConstraintToolkit constraintUI;
		private Button dungeonEnterButton;
		private VisualElement dungeonSelectButtonsParent;

		private int curDungeonIndex = 0;
		private List<Dungeon> dungeons;

		private DungeonManager dungeonManager;

		[Inject]
		public void Construct(DungeonManager dungeonManager) => this.dungeonManager = dungeonManager;

		private Dungeon CurDungeon => dungeons[curDungeonIndex];

		protected override void BuildUI(VisualElement root)
		{
			root.style.flexGrow = 1;
			root.style.flexDirection = FlexDirection.Row;

			dungeonSelectButtonsParent = new VisualElement { name = "DungeonSelectButtons" };
			dungeonSelectButtonsParent.style.flexDirection = FlexDirection.Column;
			root.Add(dungeonSelectButtonsParent);

			VisualElement detail = new VisualElement { name = "Detail" };
			detail.style.flexGrow = 1;
			detail.style.flexDirection = FlexDirection.Column;
			root.Add(detail);

			rewardUI = new UIRewardsToolkit();
			detail.Add(rewardUI);

			constraintUI = new UIDungeonConstraintToolkit();
			detail.Add(constraintUI);

			dungeonEnterButton = new Button(EnterTheDungeon) { name = "EnterButton", text = "입장" };
			detail.Add(dungeonEnterButton);
		}

		public override void SetNPC(NPCObject npc)
		{
			dungeons = NPCUtil.GetDungeons(npc.Data);

			if (dungeons == null || dungeons.Count == 0)
				Debug.LogError("No Dungeon Data");

			EnsureButtonCount(dungeons.Count);

			for (int i = 0; i < dungeonSelectButtons.Count; i++)
			{
				if (i < dungeons.Count)
				{
					dungeonSelectButtons[i].SetSlot(dungeons[i]);
					dungeonSelectButtons[i].style.display = DisplayStyle.Flex;
				}
				else
				{
					dungeonSelectButtons[i].style.display = DisplayStyle.None;
				}
			}

			if (dungeonSelectButtons.Count > 0)
				dungeonSelectButtons[0].Select();
		}

		protected override void OnOpen() => SelectDungeon(0);

		public override void UpdateUI() => UpdateTooltip();

		public void SelectDungeon(int index)
		{
			curDungeonIndex = index;
			UpdateTooltip();
			dungeonEnterButton.Focus();
		}

		private void UpdateTooltip()
		{
			ToolkitSlot selectedSlot = dungeonSelectButtons[curDungeonIndex];
			Vector2 anchor = selectedSlot.worldBound.position;
			selectedSlot.GetUIServices()?.Tooltip?.ShowAnchored(selectedSlot.Data, anchor);

			rewardUI.UpdateUI(CurDungeon.Rewards);

			constraintUI.SetDungeon(CurDungeon);
			constraintUI.UpdateUI();
		}

		public void EnterTheDungeon()
		{
			PanelGroup.ClosePanel();
			dungeonManager.StartDungeon(CurDungeon);
		}

		private void EnsureButtonCount(int count)
		{
			while (dungeonSelectButtons.Count < count)
			{
				ToolkitSlot slot = new ToolkitSlot();
				slot.SetSlotIndex(dungeonSelectButtons.Count);
				slot.SetClickAction((ToolkitSlot clickedSlot) => SelectDungeon(clickedSlot.Index));
				dungeonSelectButtons.Add(slot);
				dungeonSelectButtonsParent.Add(slot);
			}
		}
	}
}
