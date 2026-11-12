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
		private const string USS_MODE_SELECTED = "wm-dungeon-entrance__mode-button--selected";
		private const string USS_THREAT_LOCKED = "wm-dungeon-entrance__mode-button--locked";
		private const string MULTIPLIER_FORMAT = "0.##";
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
		private VisualElement constraintSection;
		private Button dungeonEnterButton;
		private Button backButton;
		private VisualElement dungeonSelectButtonsParent;
		private readonly Dictionary<DungeonRunMode, Button> runModeButtons = new();
		private readonly List<Button> horizontalFocusOrder = new();
		private DungeonRunMode selectedRunMode = DungeonRunMode.Equipped;
		private VisualElement threatRow;
		private Button threatPrevButton;
		private Button threatNextButton;
		private Label threatLevelLabel;
		private Label threatEffectLabel;
		private int selectedThreatLevel = 0;

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
			backButton = AddRoundButton("BackButton", "←");
			top.Add(backButton);
			root.RegisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
			AddLabel(top, "던전", "__title");

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

			constraintSection = AddSection(detail, "__section--constraints");
			AddHeader(constraintSection, "제약");
			constraintUI = new UIDungeonConstraintToolkit();
			constraintSection.Add(constraintUI);

			dungeonEnterButton = new Button(EnterTheDungeon) { name = "EnterButton" };
			dungeonEnterButton.AddToClassList(USS_ROOT + "__enter");
			AddBlock(dungeonEnterButton, "__enter-glow").pickingMode = PickingMode.Ignore;
			AddBlock(dungeonEnterButton, "__enter-ring").pickingMode = PickingMode.Ignore;
			AddLabel(dungeonEnterButton, "→", "__enter-arrow").pickingMode = PickingMode.Ignore;
			AddLabel(dungeonEnterButton, "입장", "__enter-text").pickingMode = PickingMode.Ignore;
			detail.Add(dungeonEnterButton);

			VisualElement modeRow = AddBlock(root, "__mode");
			Button equippedButton = AddRunModeButton(modeRow, DungeonRunMode.Equipped, "장착 모드");
			Button randomButton = AddRunModeButton(modeRow, DungeonRunMode.Random, "랜덤 모드");
			horizontalFocusOrder.AddRange(new[] { backButton, equippedButton, randomButton, dungeonEnterButton });
			SelectRunMode(selectedRunMode);
			BuildThreatRow(root);

			Button closeButton = AddRoundButton("CloseButton", "×");
			closeButton.AddToClassList(USS_ROOT + "__close");
			root.Add(closeButton);
		}

		private Button AddRunModeButton(VisualElement parent, DungeonRunMode mode, string text)
		{
			Button button = new Button(() => { UiSfx.Play(UiSfxEvent.Tab); SelectRunMode(mode); }) { name = mode + "ModeButton", text = text };
			button.AddToClassList(USS_ROOT + "__mode-button");
			parent.Add(button);
			runModeButtons[mode] = button;
			return button;
		}

		private void SelectRunMode(DungeonRunMode mode)
		{
			selectedRunMode = mode;
			foreach (KeyValuePair<DungeonRunMode, Button> entry in runModeButtons)
				entry.Value.EnableInClassList(USS_MODE_SELECTED, entry.Key == mode);
		}

		private void BuildThreatRow(VisualElement root)
		{
			threatRow = AddBlock(root, "__threat");
			threatPrevButton = AddThreatStepButton("ThreatPrevButton", "<", -1);
			threatLevelLabel = AddLabel(threatRow, string.Empty, "__threat-level");
			threatNextButton = AddThreatStepButton("ThreatNextButton", ">", 1);
			threatEffectLabel = AddLabel(threatRow, string.Empty, "__threat-effect");
		}

		private Button AddThreatStepButton(string buttonName, string text, int direction)
		{
			Button button = new Button(() => StepThreat(direction)) { name = buttonName, text = text };
			button.AddToClassList(USS_ROOT + "__mode-button");
			threatRow.Add(button);
			return button;
		}

		private void StepThreat(int direction)
		{
			UiSfx.Play(UiSfxEvent.Slider);
			selectedThreatLevel = DungeonThreatRules.StepSelection(selectedThreatLevel, direction, CurDungeon.ClearedThreatCount, dungeonManager.GetMaxThreatLevel(CurDungeon));
			UpdateThreatRow();
		}

		private void UpdateThreatRow()
		{
			int maxLevel = dungeonManager.GetMaxThreatLevel(CurDungeon);
			int highestUnlocked = DungeonThreatRules.HighestUnlocked(CurDungeon.ClearedThreatCount, maxLevel);
			selectedThreatLevel = Mathf.Clamp(selectedThreatLevel, 0, highestUnlocked);

			bool isVisible = maxLevel > 0;
			threatRow.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
			horizontalFocusOrder.Remove(threatPrevButton);
			horizontalFocusOrder.Remove(threatNextButton);
			if (isVisible == false)
				return;
			horizontalFocusOrder.InsertRange(horizontalFocusOrder.IndexOf(dungeonEnterButton), new[] { threatPrevButton, threatNextButton });

			threatLevelLabel.text = $"위협 단계 {selectedThreatLevel} / {maxLevel}";
			threatPrevButton.EnableInClassList(USS_THREAT_LOCKED, selectedThreatLevel == 0); // zero-id-ok: 위협 단계 0, ID 아님
			threatNextButton.EnableInClassList(USS_THREAT_LOCKED, selectedThreatLevel == highestUnlocked);
			string unlockHint = selectedThreatLevel == highestUnlocked && highestUnlocked < maxLevel ? "\n다음 단계: 이 단계 승리 필요" : string.Empty;
			threatEffectLabel.text = DescribeThreat(dungeonManager.ResolveThreat(CurDungeon, selectedThreatLevel)) + unlockHint;
		}

		private static string DescribeThreat(DungeonThreatEffect threat)
		{
			List<string> parts = new();
			AddMultiplierPart(parts, "적 체력", threat.EnemyHealth);
			AddMultiplierPart(parts, "적 피해", threat.EnemyDamage);
			if (threat.EliteExtraPerGap > 0)
				parts.Add($"정예 추가 +{threat.EliteExtraPerGap}");
			AddMultiplierPart(parts, "적 속도", threat.EnemySpeed);
			AddMultiplierPart(parts, "웨이브 물량", threat.WaveRate);
			parts.Add($"보상 x{threat.RewardMultiplier.ToString(MULTIPLIER_FORMAT)}");
			return string.Join(", ", parts);
		}

		private static void AddMultiplierPart(List<string> parts, string statName, float multiplier)
		{
			if (Mathf.Approximately(multiplier, 1f) == false)
				parts.Add($"{statName} x{multiplier.ToString(MULTIPLIER_FORMAT)}");
		}

		private void MoveHorizontalFocus(int direction)
		{
			int index = horizontalFocusOrder.IndexOf(Root.focusController?.focusedElement as Button);
			if (index < 0)
			{
				WMFocusNavigation.MoveFocus(horizontalFocusOrder[direction < 0 ? 0 : horizontalFocusOrder.Count - 1]);
				return;
			}
			int nextIndex = Mathf.Clamp(index + direction, 0, horizontalFocusOrder.Count - 1);
			if (nextIndex != index)
				WMFocusNavigation.MoveFocus(horizontalFocusOrder[nextIndex]);
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
			Button button = new Button(() => { UiSfx.Play(UiSfxEvent.Cancel); PanelGroup.ClosePanel(); }) { name = buttonName, text = text };
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
			// 데모 릴리스 빌드는 개발용 던전 미노출 (사용자 2026-11-12)
			if (Debug.isDebugBuild == false)
				dungeons = dungeons.FindAll(dungeon => dungeon.DevBuildOnly == false);

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

		private void OnNavigationMove(NavigationMoveEvent evt)
		{
			switch (evt.direction)
			{
				case NavigationMoveEvent.Direction.Up:
					UiSfx.Play(UiSfxEvent.FocusMove);
					SelectDungeon((curDungeonIndex + dungeons.Count - 1) % dungeons.Count);
					break;
				case NavigationMoveEvent.Direction.Down:
					UiSfx.Play(UiSfxEvent.FocusMove);
					SelectDungeon((curDungeonIndex + 1) % dungeons.Count);
					break;
				case NavigationMoveEvent.Direction.Left:
					MoveHorizontalFocus(-1);
					break;
				case NavigationMoveEvent.Direction.Right:
					MoveHorizontalFocus(1);
					break;
				default:
					return;
			}
			evt.StopPropagation();
			Root.focusController?.IgnoreEvent(evt);
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
			constraintSection.style.display = CurDungeon.Constraints.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;

			UpdateThreatRow();
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
			UiSfx.Play(UiSfxEvent.Confirm);
			PanelGroup.ClosePanel();
			dungeonManager.StartDungeon(CurDungeon, runMode: selectedRunMode, threatLevel: selectedThreatLevel);
		}

		private void EnsureButtonCount(int count)
		{
			while (dungeonSelectButtons.Count < count)
			{
				int index = dungeonSelectButtons.Count;
				Button row = new Button(() => { UiSfx.Play(UiSfxEvent.FocusMove); SelectDungeon(index); });
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
