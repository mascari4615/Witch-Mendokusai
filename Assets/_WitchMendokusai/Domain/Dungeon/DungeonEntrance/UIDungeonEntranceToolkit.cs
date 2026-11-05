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

		private const string USS_ROOT = "wm-dungeon-entrance";
		private const string USS_ROW_SELECTED = "wm-dungeon-entrance__row--selected";

		private readonly List<Button> dungeonSelectButtons = new();
		private Image heroImage;
		private Label nameLabel;
		private Label descriptionLabel;
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
			root.AddToClassList(USS_ROOT);

			VisualElement top = AddBlock(root, "__top");
			top.Add(new Button(() => PanelGroup.ClosePanel()) { name = "BackButton", text = "←" });
			top.Add(new Label("던전"));

			VisualElement body = AddBlock(root, "__body");
			dungeonSelectButtonsParent = AddBlock(body, "__list");

			heroImage = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
			heroImage.AddToClassList(USS_ROOT + "__hero");
			body.Add(heroImage);

			VisualElement detail = AddBlock(body, "__detail");
			nameLabel = AddLabel(detail, string.Empty, "__name");
			descriptionLabel = AddLabel(detail, string.Empty, "__description");

			AddLabel(detail, "보상", "__label");
			rewardUI = new UIRewardsToolkit();
			detail.Add(rewardUI);

			AddLabel(detail, "제약", "__label");
			constraintUI = new UIDungeonConstraintToolkit();
			detail.Add(constraintUI);

			dungeonEnterButton = new Button(EnterTheDungeon) { name = "EnterButton", text = "입장" };
			dungeonEnterButton.AddToClassList(USS_ROOT + "__enter");
			detail.Add(dungeonEnterButton);
		}

		private static VisualElement AddBlock(VisualElement parent, string suffix)
		{
			VisualElement block = new VisualElement();
			block.AddToClassList(USS_ROOT + suffix);
			parent.Add(block);
			return block;
		}

		private static Label AddLabel(VisualElement parent, string text, string suffix)
		{
			Label label = new Label(text);
			label.AddToClassList(USS_ROOT + suffix);
			parent.Add(label);
			return label;
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
					dungeonSelectButtons[i].text = dungeons[i].Name;
					dungeonSelectButtons[i].style.display = DisplayStyle.Flex;
				}
				else
				{
					dungeonSelectButtons[i].style.display = DisplayStyle.None;
				}
			}
		}

		protected override void OnOpen() => SelectDungeon(0);

		protected override void OnClose()
		{
			Root.panel?.focusController?.focusedElement?.Blur();
		}

		public override void UpdateUI() => UpdateDetail();

		public void SelectDungeon(int index)
		{
			curDungeonIndex = index;
			UpdateDetail();
			dungeonEnterButton.Focus();
		}

		private void UpdateDetail()
		{
			for (int i = 0; i < dungeonSelectButtons.Count; i++)
				dungeonSelectButtons[i].EnableInClassList(USS_ROW_SELECTED, i == curDungeonIndex);

			heroImage.sprite = GetHeroSprite(CurDungeon);
			nameLabel.text = CurDungeon.Name;
			descriptionLabel.text = CurDungeon.Description;

			rewardUI.UpdateUI(CurDungeon.Rewards);

			constraintUI.SetDungeon(CurDungeon);
			constraintUI.UpdateUI();
		}

		// 마지막 웨이브의 첫 몬스터가 대표, 웨이브가 없으면 던전 아이콘
		private static Sprite GetHeroSprite(Dungeon dungeon)
		{
			List<MonsterWave> waves = dungeon.MonsterWaves;
			if (waves == null || waves.Count == 0)
				return dungeon.Sprite;

			Monster[] monsters = waves[waves.Count - 1].Monsters;
			return monsters == null || monsters.Length == 0 ? dungeon.Sprite : monsters[0].Sprite;
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
				int index = dungeonSelectButtons.Count;
				Button row = new Button(() => SelectDungeon(index));
				row.AddToClassList(USS_ROOT + "__row");
				dungeonSelectButtons.Add(row);
				dungeonSelectButtonsParent.Add(row);
			}
		}
	}
}
