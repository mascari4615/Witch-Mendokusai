using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using VContainer;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UIDungeonResult : UIPanel
	{
		[SerializeField] private SurvivalUITheme theme;
		[SerializeField] private TextMeshProUGUI titleText;
		[SerializeField] private string clearTitle = "게임 결과";
		[SerializeField] private string defeatTitle = "패배";
		[SerializeField] private Color defeatTitleColor = new(0.886f, 0.345f, 0.298f, 1f);
		[SerializeField] private UIRing difficultyRing;
		[SerializeField] private TextMeshProUGUI difficultyText;
		[SerializeField] private TextMeshProUGUI playTimeText;
		[SerializeField] private TextMeshProUGUI levelText;
		[SerializeField] private TextMeshProUGUI killCountText;
		[SerializeField] private TextMeshProUGUI bossKillCountText;
		[SerializeField] private CanvasGroup bossRowGroup;
		[SerializeField] private float emptyBossRowAlpha = 0.45f;
		[SerializeField] private CanvasGroup[] rowGroups;
		[SerializeField] private TextMeshProUGUI nyangText;
		[SerializeField] private CanvasGroup totalGroup;
		[SerializeField] private Image totalOutline;
		[SerializeField] private Color defeatOutline = new(0.43f, 0.42f, 0.46f, 1f);
		[SerializeField] private Color clearOutline = new(0.949f, 0.886f, 0.478f, 1f);
		[SerializeField] private UIBurst totalBurst;
		[SerializeField] private TextMeshProUGUI cardCountText;
		[SerializeField] private Transform cardParent;
		[SerializeField] private Transform itemParent;
		[SerializeField] private UIMiniCard entryTemplate;
		[SerializeField] private Button retryButton;

		[SerializeField] private string battleHudObjectName = "[Block] Lower";

		private readonly List<UIMiniCard> entries = new();
		private CanvasGroup battleHudGroup;

		private DungeonManager dungeonManager;
		private SOManager soManager;
		private TimeManager timeManager;

		[Inject]
		public void Construct(DungeonManager dungeonManager, SOManager soManager, TimeManager timeManager)
		{
			this.dungeonManager = dungeonManager;
			this.soManager = soManager;
			this.timeManager = timeManager;
		}

		public override bool IsFullscreen => true;

		protected override void OnOpen()
		{
			timeManager.Pause(gameObject);
			SetBattleHudVisible(false);
		}

		protected override void OnClose()
		{
			DOTween.Kill(this);
			timeManager.Resume(gameObject);
			SetBattleHudVisible(true);
		}

		private void SetBattleHudVisible(bool isVisible)
		{
			if (battleHudGroup == null)
			{
				GameObject battleHud = GameObject.Find(battleHudObjectName);
				battleHudGroup = battleHud.GetComponent<CanvasGroup>();
				if (battleHudGroup == null)
					battleHudGroup = battleHud.AddComponent<CanvasGroup>();
			}
			battleHudGroup.alpha = isVisible ? 1f : 0f;
			battleHudGroup.blocksRaycasts = isVisible;
			battleHudGroup.interactable = isVisible;
		}

		public void Retry() => dungeonManager.Retry();

		public void Continue()
		{
			dungeonManager.Continue();
		}

		protected override void OnInit()
		{
			retryButton.onClick.AddListener(Retry);
			entryTemplate.gameObject.SetActive(false);
		}

		public override void UpdateUI()
		{
			DungeonRecord record = dungeonManager.Result;
			bool cleared = record.IsCleared;
			float speed = cleared ? 1f : theme.DefeatSpeedScale;

			titleText.text = cleared ? clearTitle : defeatTitle;
			titleText.color = cleared ? Color.white : defeatTitleColor;
			difficultyText.text = DifficultyName(record.Difficulty);
			difficultyRing.color = theme.Difficulty(record.Difficulty);
			totalOutline.color = cleared ? clearOutline : defeatOutline;
			nyangText.color = cleared ? theme.ValueColor : Color.white;
			bossRowGroup.alpha = 1f;

			RebuildEntries();
			Play(record, speed);
		}

		private void RebuildEntries()
		{
			foreach (UIMiniCard entry in entries)
				Destroy(entry.gameObject);
			entries.Clear();

			IReadOnlyList<CardData> selected = soManager.SelectedCardBuffer.Data;
			foreach (IGrouping<int, CardData> group in selected.Where(card => card != null).GroupBy(card => card.ID))
			{
				UIMiniCard entry = Instantiate(entryTemplate, cardParent);
				entry.gameObject.SetActive(true);
				entry.SetCard(group.First(), group.Count());
				entries.Add(entry);
			}
			cardCountText.text = $"{selected.Count}회 강화";

			ItemDataBuffer buffer = soManager.DungeonItemBuffer;
			foreach (ItemData itemData in buffer.Data.Where(item => item != null).GroupBy(item => item.ID).Select(group => group.First()))
			{
				UIMiniCard entry = Instantiate(entryTemplate, itemParent);
				entry.gameObject.SetActive(true);
				entry.SetItem(itemData, buffer.itemCountDic.TryGetValue(itemData.ID, out int count) ? count : 1);
				entries.Add(entry);
			}
		}

		private void Play(DungeonRecord record, float speed)
		{
			DOTween.Kill(this);
			float timeDuration = theme.ResultTimeDuration * speed;
			int seconds = (int)record.PlayTime.TotalSeconds;
			UIEffects.CountUp(playTimeText, seconds, timeDuration, 0f, value => TimeSpan.FromSeconds(value).ToString(@"mm\:ss"));

			float targetFill = record.DifficultyProgress;
			if (UIEffects.Enabled)
			{
				difficultyRing.SetFill(0f);
				float current = 0f;
				DOTween.To(() => current, value => { current = value; difficultyRing.SetFill(value); }, targetFill, timeDuration).SetUpdate(true).SetTarget(this);
			}
			else
			{
				difficultyRing.SetFill(targetFill);
			}

			float rowTime = theme.ResultRowStart * speed;
			int[] rowValues = { record.Level, record.KillCount, record.BossKillCount };
			TextMeshProUGUI[] rowTexts = { levelText, killCountText, bossKillCountText };
			for (int i = 0; i < rowGroups.Length; i++)
			{
				UIEffects.FadeIn(rowGroups[i], theme.HoverDuration, rowTime);
				UIEffects.CountUp(rowTexts[i], rowValues[i], theme.ResultRowCountDuration * speed, rowTime, value => value.ToString());
				rowTime += theme.ResultRowInterval * speed;
			}
			if (record.BossKillCount == 0)
				bossRowGroup.DOFade(emptyBossRowAlpha, theme.HoverDuration).SetDelay(rowTime).SetUpdate(true).SetTarget(this);

			float entryTime = rowTime;
			foreach (UIMiniCard entry in entries)
			{
				UIEffects.PopIn(entry.transform, theme.CardAppearDuration, entryTime);
				entryTime += theme.ResultEntryInterval * speed;
			}

			float totalTime = entryTime + theme.HoverDuration;
			UIEffects.FadeIn(totalGroup, theme.HoverDuration, totalTime);
			UIEffects.CountUp(nyangText, record.Nyang, theme.ResultTotalDuration * speed, totalTime, value => value.ToString("N0"));
			if (UIEffects.Enabled && record.IsCleared)
			{
				DOVirtual.DelayedCall(totalTime + theme.ResultTotalDuration, () =>
				{
					UIEffects.Punch(nyangText.transform, theme.PunchAmount, theme.PunchDuration);
					totalBurst.Play(theme.ValueColor);
				}).SetUpdate(true).SetTarget(this);
			}
		}

		private static string DifficultyName(DungeonDifficulty difficulty) => difficulty switch
		{
			DungeonDifficulty.Easy => "쉬움",
			DungeonDifficulty.Normal => "보통",
			_ => "어려움",
		};
	}
}
