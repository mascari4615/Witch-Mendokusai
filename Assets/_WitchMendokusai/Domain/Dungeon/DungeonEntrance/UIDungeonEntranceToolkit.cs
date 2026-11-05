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
		private const int SECONDS_PER_MINUTE = 60;

		private readonly List<Button> dungeonSelectButtons = new();
		private readonly List<Label> dungeonRowNames = new();
		private readonly List<VisualElement> dungeonRowIcons = new();
		private Image monsterImage;
		private Label nameLabel;
		private Label timeLabel;
		private Label descriptionLabel;
		private Label backdropTextLabel;
		private Label numberLabel;
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

			backdropTextLabel = AddLabel(root, string.Empty, "__backdrop-text");
			backdropTextLabel.pickingMode = PickingMode.Ignore;
			AddBlock(root, "__deco").pickingMode = PickingMode.Ignore;
			numberLabel = AddLabel(root, string.Empty, "__number");
			numberLabel.pickingMode = PickingMode.Ignore;
			AddBlock(root, "__hero").pickingMode = PickingMode.Ignore;
			AddBlock(root, "__mist").pickingMode = PickingMode.Ignore;

			VisualElement top = AddBlock(root, "__top");
			top.Add(AddRoundButton("BackButton", "←"));
			AddLabel(top, "던전", "__title");

			Button closeButton = AddRoundButton("CloseButton", "×");
			closeButton.AddToClassList(USS_ROOT + "__close");
			root.Add(closeButton);

			dungeonSelectButtonsParent = AddBlock(root, "__list");

			VisualElement detail = AddBlock(root, "__detail");
			AddBlock(detail, "__card").pickingMode = PickingMode.Ignore;
			VisualElement summary = AddBlock(detail, "__summary");
			nameLabel = AddLabel(summary, string.Empty, "__name");
			timeLabel = AddLabel(summary, string.Empty, "__tag");
			descriptionLabel = AddLabel(summary, string.Empty, "__description");

			VisualElement rewards = AddSection(detail, "__section--rewards");
			AddHeader(rewards, "보상과 출현");
			VisualElement tiles = AddBlock(rewards, "__tiles");
			rewardUI = new UIRewardsToolkit();
			tiles.Add(rewardUI);
			VisualElement monster = AddBlock(tiles, "__monster");
			monsterImage = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
			monsterImage.AddToClassList(USS_ROOT + "__monster-image");
			monster.Add(monsterImage);

			VisualElement constraints = AddSection(detail, "__section--constraints");
			AddHeader(constraints, "제약");
			constraintUI = new UIDungeonConstraintToolkit();
			constraints.Add(constraintUI);

			dungeonEnterButton = new Button(EnterTheDungeon) { name = "EnterButton" };
			dungeonEnterButton.AddToClassList(USS_ROOT + "__enter");
			AddBlock(dungeonEnterButton, "__enter-glow").pickingMode = PickingMode.Ignore;
			AddBlock(dungeonEnterButton, "__enter-ring").pickingMode = PickingMode.Ignore;
			AddLabel(dungeonEnterButton, "→", "__enter-arrow").pickingMode = PickingMode.Ignore;
			AddLabel(dungeonEnterButton, "입장", "__enter-text").pickingMode = PickingMode.Ignore;
			detail.Add(dungeonEnterButton);
		}

		private static VisualElement AddSection(VisualElement parent, string suffix)
		{
			VisualElement section = AddBlock(parent, "__section");
			section.AddToClassList(USS_ROOT + suffix);
			return section;
		}

		private void ApplyTheme()
		{
			foreach (DungeonEntranceTheme theme in System.Enum.GetValues(typeof(DungeonEntranceTheme)))
				Root.EnableInClassList(USS_ROOT + DungeonEntranceThemes.GetClassSuffix(theme), theme == DungeonEntranceThemes.Current);
		}

		private Button AddRoundButton(string buttonName, string text)
		{
			Button button = new Button(() => PanelGroup.ClosePanel()) { name = buttonName, text = text };
			button.AddToClassList(USS_ROOT + "__round");
			return button;
		}

		private static void AddHeader(VisualElement parent, string text)
		{
			VisualElement header = AddBlock(parent, "__header");
			AddLabel(header, text, "__header-text");
			AddBlock(header, "__header-line");
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
					dungeonRowNames[i].text = GetRowName(dungeons[i].Name);
					dungeonRowIcons[i].style.backgroundImage = new StyleBackground(dungeons[i].Sprite);
					dungeonSelectButtons[i].style.display = DisplayStyle.Flex;
				}
				else
				{
					dungeonSelectButtons[i].style.display = DisplayStyle.None;
				}
			}
		}

		protected override void OnOpen()
		{
			ApplyTheme();
			DungeonEntranceThemes.OnChanged -= ApplyTheme;
			DungeonEntranceThemes.OnChanged += ApplyTheme;
			SelectDungeon(0);
		}

		protected override void OnClose()
		{
			DungeonEntranceThemes.OnChanged -= ApplyTheme;
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

			monsterImage.sprite = GetMonsterSprite(CurDungeon);
			nameLabel.text = GetTitle(CurDungeon.Name);
			backdropTextLabel.text = nameLabel.text;
			numberLabel.text = GetRowNumber(curDungeonIndex);
			timeLabel.text = CurDungeon.TimeBySecond / SECONDS_PER_MINUTE + "분";
			timeLabel.style.display = CurDungeon.TimeBySecond > 0 ? DisplayStyle.Flex : DisplayStyle.None;
			descriptionLabel.text = CurDungeon.Description;

			rewardUI.UpdateUI(CurDungeon.Rewards);

			constraintUI.SetDungeon(CurDungeon);
			constraintUI.UpdateUI();
		}

		private static string GetRowNumber(int index) => (index + 1).ToString("00");

		// 데이터 이름의 괄호: 목록은 풀어서, 제목은 시간 태그로
		private static string GetRowName(string dungeonName) => dungeonName.Replace("(", string.Empty).Replace(")", string.Empty);

		private static string GetTitle(string dungeonName)
		{
			int bracketIndex = dungeonName.IndexOf(" (", System.StringComparison.Ordinal);
			return bracketIndex < 0 ? dungeonName : dungeonName[..bracketIndex];
		}

		// 마지막 웨이브의 첫 몬스터가 대표, 웨이브가 없으면 던전 아이콘
		private static Sprite GetMonsterSprite(Dungeon dungeon)
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

				AddBlock(row, "__row-plate").pickingMode = PickingMode.Ignore;
				AddBlock(row, "__row-pointer").pickingMode = PickingMode.Ignore;
				AddLabel(row, GetRowNumber(index), "__row-number").pickingMode = PickingMode.Ignore;

				VisualElement seal = AddBlock(row, "__row-seal");
				seal.pickingMode = PickingMode.Ignore;
				VisualElement icon = AddBlock(seal, "__row-icon");
				icon.pickingMode = PickingMode.Ignore;
				Label rowName = AddLabel(row, string.Empty, "__row-name");
				rowName.pickingMode = PickingMode.Ignore;

				dungeonRowIcons.Add(icon);
				dungeonRowNames.Add(rowName);
				dungeonSelectButtons.Add(row);
				dungeonSelectButtonsParent.Add(row);
			}
		}
	}
}
