using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;
using BigNumberText = WitchMendokusai.Numerics.BigNumberText;

namespace WitchMendokusai.Idle.UI
{
	internal sealed class BattleHudController
	{
		private readonly VisualElement battle;
		private readonly VisualTreeAsset waveDotAsset;
		private readonly UIContentSO content;
		private double lastIncomePerSecond;
		private readonly Func<int, bool> canGoToStage;
		private readonly VisualElement sceneCover;
		private readonly Label sceneCoverLabel;
		private readonly Label opCode;
		private readonly Label opName;
		private readonly VisualElement waveDots;
		private readonly Label waveLabel;
		private readonly Label dungeonTimer;
		private readonly Button dungeonLeave;
		private readonly Button stepBack;
		private readonly Button stepForward;
		private readonly Label stepLabel;
		private readonly Button repeatButton;
		private readonly Label goldValue;
		private readonly Button splitButton;
		private readonly VisualElement enemyBar;
		private readonly VisualElement enemyFill;
		private readonly Label enemyLabel;
		private readonly Button autoCastButton;
		private readonly Label costLabel;
		private readonly Button speedButton;
		private readonly VisualElement costFill;
		private readonly List<VisualElement> waveDotList = new List<VisualElement>();

		public BattleHudController(
			VisualElement battle,
			VisualTreeAsset waveDotAsset,
			UIContentSO content,
			Func<int, bool> canGoToStage,
			Action openDoll,
			Action toggleMap,
			Action<int> stepStage,
			Action toggleHold,
			Action openGold,
			Action toggleSplit,
			Action openSettings,
			Action toggleAutoCast,
			Action cycleSpeed,
			Action leaveDungeon,
			Action<VisualElement, Func<string>> hookTooltip)
		{
			this.battle = battle;
			this.waveDotAsset = waveDotAsset;
			this.content = content;
			this.canGoToStage = canGoToStage;
			sceneCover = battle.RequireQ<VisualElement>("scene-cover");
			sceneCoverLabel = battle.RequireQ<Label>("scene-cover-label");
			opCode = battle.RequireQ<Label>("op-code");
			opName = battle.RequireQ<Label>("op-name");
			waveDots = battle.RequireQ<VisualElement>("wave-dots");
			waveLabel = battle.RequireQ<Label>("wave-label");
			dungeonTimer = battle.RequireQ<Label>("dungeon-timer");
			dungeonTimer.style.display = DisplayStyle.None;
			dungeonLeave = battle.RequireQ<Button>("dungeon-leave");
			dungeonLeave.style.display = DisplayStyle.None;
			dungeonLeave.clicked += leaveDungeon;
			stepBack = battle.RequireQ<Button>("step-back");
			stepForward = battle.RequireQ<Button>("step-forward");
			stepLabel = battle.RequireQ<Label>("step-label");
			repeatButton = battle.RequireQ<Button>("repeat-button");
			Button goldChip = battle.RequireQ<Button>("gold-chip");
			goldValue = goldChip.RequireQ<Label>("gold-value");
			// 칩은 잔액만 보여줌. 초당 수입은 툴팁으로 (마우스 호버, 손가락은 눌러서)
			hookTooltip(goldChip, () => content.GoldIncomeText(BigNumberText.Format(lastIncomePerSecond)));
			splitButton = battle.RequireQ<Button>("split-button");
			Button settingsButton = battle.RequireQ<Button>("settings-button");
			enemyBar = battle.RequireQ<VisualElement>("enemy-bar");
			enemyFill = battle.RequireQ<VisualElement>("enemy-fill");
			enemyLabel = battle.RequireQ<Label>("enemy-label");
			autoCastButton = battle.RequireQ<Button>("auto-cast-button");
			speedButton = battle.RequireQ<Button>("speed-button");
			speedButton.clicked += cycleSpeed;
			costLabel = battle.RequireQ<Label>("cost-label");
			costFill = battle.RequireQ<VisualElement>("cost-fill");

			sceneCover.style.display = DisplayStyle.None;
			battle.RequireQ<Button>("scene-cover-button").clicked += openDoll;
			battle.RequireQ<VisualElement>("op").RegisterCallback<ClickEvent>(_ => toggleMap());
			stepBack.clicked += () => stepStage(-1);
			stepForward.clicked += () => stepStage(1);
			repeatButton.clicked += toggleHold;
			goldChip.clicked += openGold;
			splitButton.clicked += toggleSplit;
			settingsButton.clicked += openSettings;
			autoCastButton.clicked += toggleAutoCast;
		}

		public void Render(IdleSnapshot snapshot)
		{
			opCode.text = content.OperationCodeText(snapshot.Stage);
			opName.text = content.BattleGradeText(snapshot.MaxTierNow, snapshot.TierCeiling);
			stepLabel.text = content.StageText(snapshot.Stage);
			stepBack.SetEnabled(canGoToStage(snapshot.Stage - 1));
			stepForward.SetEnabled(snapshot.Stage < snapshot.BestStage
				&& canGoToStage(snapshot.Stage + 1));

			bool repeating = snapshot.HoldingStage || snapshot.Repeating;
			repeatButton.text = content.RepeatText(repeating);
			repeatButton.EnableInClassList("idle-toggle--on", repeating);
			goldValue.text = BigNumberText.Format(snapshot.Resource);
			lastIncomePerSecond = snapshot.IncomePerSecond;
			autoCastButton.EnableInClassList("idle-icon-button--on", snapshot.AutoCast);
			speedButton.text = content.SpeedChipText(snapshot.Speed);
			costLabel.text = content.CostText(snapshot.Cost, snapshot.CostMax);
			costFill.style.width = new StyleLength(new Length(
				snapshot.CostMax > 0d ? (float)(snapshot.Cost / snapshot.CostMax * 100d) : 0f,
				LengthUnit.Percent));
			RenderEnemy(snapshot);
			RenderDungeon(snapshot);
		}

		/// <summary>던전 판 도는 동안만 칸 이름과 남은 시간, 나가기 (changes/idle-dungeon-run v2)</summary>
		private void RenderDungeon(IdleSnapshot snapshot)
		{
			IdleDungeonRunView run = snapshot.DungeonRun;
			DisplayStyle shown = run.Active ? DisplayStyle.Flex : DisplayStyle.None;
			DisplayStyle mainShown = run.Active ? DisplayStyle.None : DisplayStyle.Flex;
			opCode.style.display = mainShown;
			opName.style.display = mainShown;
			waveDots.style.display = mainShown;
			waveLabel.style.display = mainShown;
			stepBack.parent.parent.style.display = mainShown;
			dungeonTimer.style.display = shown;
			dungeonLeave.style.display = shown;
			if (run.Active)
			{
				dungeonTimer.text = content.DungeonRunText(content.DungeonCellText(run.Kind, run.Difficulty, run.Stage), run.SecondsLeft);
				dungeonLeave.text = content.DungeonLeaveText;
			}
		}

		public void SetAlternateScene(bool shown, string caption)
		{
			sceneCover.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
			sceneCoverLabel.text = caption;
			battle.EnableInClassList("idle-battle--alt", shown);
		}

		public void SetSplit(bool split)
		{
			battle.EnableInClassList("idle-battle--full", split == false);
			splitButton.EnableInClassList("idle-split-button--collapsed", split == false);
		}

		private void RenderEnemy(IdleSnapshot snapshot)
		{
			bool boss = snapshot.KillsInStage >= snapshot.KillsPerStage - 1;
			double healthRatio = snapshot.TargetHealthRatio;
			int stageNumber = snapshot.Stage;
			if (snapshot.DungeonRun.Active)
			{
				boss = false;
				stageNumber = snapshot.DungeonRun.Stage + 1;
				foreach (IdleFoeView foe in snapshot.Foes)
				{
					if (foe.Boss)
					{
						boss = true;
						healthRatio = foe.HealthRatio;
						break;
					}
				}
			}
			enemyBar.style.display = boss ? DisplayStyle.Flex : DisplayStyle.None;
			if (boss)
			{
				enemyLabel.text = content.BossHealthText(stageNumber, healthRatio);
				enemyFill.style.width = new StyleLength(new Length(
					(float)(healthRatio * 100d), LengthUnit.Percent));
			}

			if (waveDotList.Count != snapshot.KillsPerStage)
			{
				waveDots.Clear();
				waveDotList.Clear();
				for (int index = 0; index < snapshot.KillsPerStage; index++)
				{
					TemplateContainer tree = waveDotAsset.Instantiate();
					VisualElement dot = tree.RequireQ<VisualElement>("wave-dot");
					dot.RemoveFromHierarchy();
					dot.EnableInClassList("idle-wave-dot--boss", index == snapshot.KillsPerStage - 1);
					waveDots.Add(dot);
					waveDotList.Add(dot);
				}
			}

			for (int index = 0; index < waveDotList.Count; index++)
			{
				waveDotList[index].EnableInClassList("idle-wave-dot--done", index < snapshot.KillsInStage);
			}
			waveLabel.text = content.WaveText(snapshot.KillsInStage, snapshot.KillsPerStage);
		}
	}
}
