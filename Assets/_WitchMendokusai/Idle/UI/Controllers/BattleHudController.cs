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
		private readonly VisualElement sceneCover;
		private readonly Label sceneCoverLabel;
		private readonly Label opCode;
		private readonly Label opName;
		private readonly VisualElement waveDots;
		private readonly Label waveLabel;
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
		private readonly VisualElement costFill;
		private readonly List<VisualElement> waveDotList = new List<VisualElement>();

		public BattleHudController(
			VisualElement battle,
			VisualTreeAsset waveDotAsset,
			Action openDoll,
			Action toggleMap,
			Action<int> stepStage,
			Action toggleHold,
			Action openGold,
			Action toggleSplit,
			Action openSettings,
			Action toggleAutoCast)
		{
			this.battle = battle;
			this.waveDotAsset = waveDotAsset;
			sceneCover = battle.Q<VisualElement>("scene-cover");
			sceneCoverLabel = battle.Q<Label>("scene-cover-label");
			opCode = battle.Q<Label>("op-code");
			opName = battle.Q<Label>("op-name");
			waveDots = battle.Q<VisualElement>("wave-dots");
			waveLabel = battle.Q<Label>("wave-label");
			stepBack = battle.Q<Button>("step-back");
			stepForward = battle.Q<Button>("step-forward");
			stepLabel = battle.Q<Label>("step-label");
			repeatButton = battle.Q<Button>("repeat-button");
			Button goldChip = battle.Q<Button>("gold-chip");
			goldValue = goldChip.Q<Label>("gold-value");
			splitButton = battle.Q<Button>("split-button");
			Button settingsButton = battle.Q<Button>("settings-button");
			enemyBar = battle.Q<VisualElement>("enemy-bar");
			enemyFill = battle.Q<VisualElement>("enemy-fill");
			enemyLabel = battle.Q<Label>("enemy-label");
			autoCastButton = battle.Q<Button>("auto-cast-button");
			costLabel = battle.Q<Label>("cost-label");
			costFill = battle.Q<VisualElement>("cost-fill");

			sceneCover.style.display = DisplayStyle.None;
			battle.Q<Button>("scene-cover-button").clicked += openDoll;
			battle.Q<VisualElement>("op").RegisterCallback<ClickEvent>(_ => toggleMap());
			stepBack.clicked += () => stepStage(-1);
			stepForward.clicked += () => stepStage(1);
			repeatButton.clicked += toggleHold;
			goldChip.clicked += openGold;
			splitButton.clicked += toggleSplit;
			settingsButton.clicked += openSettings;
			autoCastButton.clicked += toggleAutoCast;
		}

		public void Render(IdleSnapshot snapshot, IdleState state)
		{
			opCode.text = "S-" + snapshot.Stage;
			opName.text = string.Format("등급 {0}/{1}", snapshot.MaxTierNow, snapshot.TierCeiling);
			stepLabel.text = string.Format("{0}구역", snapshot.Stage);
			stepBack.SetEnabled(IdleModel.CanGoToStage(state, snapshot.Stage - 1));
			stepForward.SetEnabled(snapshot.Stage < snapshot.BestStage
				&& IdleModel.CanGoToStage(state, snapshot.Stage + 1));

			bool repeating = snapshot.HoldingStage || snapshot.Repeating;
			repeatButton.text = repeating ? "반복 ●" : "반복 ○";
			repeatButton.EnableInClassList("idle-toggle--on", repeating);
			goldValue.text = BigNumberText.Format(snapshot.Resource);
			autoCastButton.EnableInClassList("idle-icon-button--on", snapshot.AutoCast);
			costLabel.text = string.Format("{0:0}/{1:0}", snapshot.Cost, snapshot.CostMax);
			costFill.style.width = new StyleLength(new Length(
				snapshot.CostMax > 0d ? (float)(snapshot.Cost / snapshot.CostMax * 100d) : 0f,
				LengthUnit.Percent));
			RenderEnemy(snapshot);
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
			enemyBar.style.display = boss ? DisplayStyle.Flex : DisplayStyle.None;
			if (boss)
			{
				enemyLabel.text = string.Format("BOSS S-{0}  {1:P0}", snapshot.Stage, snapshot.TargetHealthRatio);
				enemyFill.style.width = new StyleLength(new Length(
					(float)(snapshot.TargetHealthRatio * 100d), LengthUnit.Percent));
			}

			if (waveDotList.Count != snapshot.KillsPerStage)
			{
				waveDots.Clear();
				waveDotList.Clear();
				for (int index = 0; index < snapshot.KillsPerStage; index++)
				{
					TemplateContainer tree = waveDotAsset.Instantiate();
					VisualElement dot = tree.Q<VisualElement>("wave-dot");
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
			waveLabel.text = string.Format("WAVE {0}/{1}", snapshot.KillsInStage, snapshot.KillsPerStage);
		}
	}
}
