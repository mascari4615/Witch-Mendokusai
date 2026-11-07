using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace WitchMendokusai
{
	public class UIDungeonRuntime : UIPanel
	{
		[field: Header("_" + nameof(UIDungeonRuntime))]
		[SerializeField] private GameObject header; // UI 상단 헤더

		[SerializeField] private Image progressBar;
		[SerializeField] private TextMeshProUGUI timeText;
		[SerializeField] private TextMeshProUGUI difficultyText;
		[SerializeField] private UIRing difficultyRing;
		[SerializeField] private TextMeshProUGUI difficultyRemainText;
		[SerializeField] private Image difficultyHalo;
		[SerializeField] private float haloPulseSpeed = 2.5f;
		[SerializeField] private float haloMinAlpha = 0.1f;
		[SerializeField] private float haloMaxAlpha = 0.3f;
		[SerializeField] private float difficultyStepSeconds = 180f;
		[SerializeField] private float hardFontSize = 44f;
		[SerializeField] private float normalFontSize = 52f;
		[SerializeField] private SurvivalUITheme theme;
		[SerializeField] private UIBurst difficultyBurst;
		private DungeonDifficulty lastDifficulty;
		[SerializeField] private TextMeshProUGUI nextEventText;
		[SerializeField] private string eliteLabel = "다음 정예";
		[SerializeField] private string swarmLabel = "다음 웨이브";
		[SerializeField] private Color nextEventNormalColor = new(0.902f, 0.902f, 0.914f, 1f);
		[SerializeField] private Color nextEventSoonColor = new(0.878f, 0.604f, 0.18f, 1f);
		private MonsterSpawner monsterSpawner;
		[SerializeField] private string battleHudObjectName = "[Block] Lower";
		private CardManager cardManager;
		private CanvasGroup headerGroup;
		private CanvasGroup battleHudGroup;
		private bool isSelecting;
		private UIQuestGrid questGrid;
		private Coroutine loop;
		private RunEquipmentHud runEquipmentHud;

		private DungeonManager dungeonManager;

		[Inject]
		public void Construct(DungeonManager dungeonManager)
		{
			this.dungeonManager = dungeonManager;
		}

		public override bool IsFullscreen => false;

		protected override void OnInit()
		{
			questGrid = GetComponentInChildren<UIQuestGrid>(true);
			questGrid.Init();
			questGrid.SetFilter(QuestType.Dungeon);
		}

		protected override void OnOpen()
		{
			StartLoop();
		}

		protected override void OnClose()
		{
			StopLoop();
			isSelecting = false;
			SetHudVisible(true);
			runEquipmentHud?.RemoveFromHierarchy();
		}

		private void Update()
		{
			if (cardManager == null)
				cardManager = FindAnyObjectByType<CardManager>(FindObjectsInactive.Include);
			bool selecting = cardManager.CurPanelType != CardPanelType.None;
			if (selecting == isSelecting)
				return;
			isSelecting = selecting;
			SetHudVisible(selecting == false);
		}

		private void SetHudVisible(bool isVisible)
		{
			if (headerGroup == null)
				headerGroup = EnsureGroup(header);
			if (battleHudGroup == null)
				battleHudGroup = EnsureGroup(GameObject.Find(battleHudObjectName));
			headerGroup.alpha = isVisible ? 1f : 0f;
			battleHudGroup.alpha = isVisible ? 1f : 0f;
			battleHudGroup.blocksRaycasts = isVisible;
			battleHudGroup.interactable = isVisible;
			headerGroup.interactable = isVisible;
			runEquipmentHud?.SetVisible(isVisible);
		}

		private void UpdateRunEquipment()
		{
			RunLoadout loadout = dungeonManager.RunLoadout;
			if (loadout.UsesRunSlots == false)
			{
				runEquipmentHud?.RemoveFromHierarchy();
				return;
			}
			runEquipmentHud ??= new RunEquipmentHud();
			if (runEquipmentHud.parent == null)
				UIRoot.Instance.HudLayer.Add(runEquipmentHud);
			runEquipmentHud.Refresh(loadout.Acquired, dungeonManager.RunEquipmentSlotCount);
		}

		private static CanvasGroup EnsureGroup(GameObject target)
		{
			CanvasGroup group = target.GetComponent<CanvasGroup>();
			return group != null ? group : target.AddComponent<CanvasGroup>();
		}

		private void StartLoop()
		{
			if (loop != null)
				StopCoroutine(loop);
			loop = StartCoroutine(Loop());
		}

		private void StopLoop()
		{
			if (loop != null)
				StopCoroutine(loop);
		}

		private IEnumerator Loop()
		{
			WaitForSeconds wait = new(.05f);

			while (true)
			{
				UpdateUI();
				yield return wait;
			}
		}

		private void UpdateTime(TimeSpan timeSpan)
		{
			progressBar.fillAmount = 1 - (float)(timeSpan.TotalSeconds / dungeonManager.Context.InitialDungeonTime.TotalSeconds);
			timeText.text = timeSpan == TimeSpan.Zero && dungeonManager.CurDungeon.ObjectiveType == DungeonObjectiveType.Boss ? "보스전" : timeSpan.ToString(@"mm\:ss");
		}

		private void UpdateDifficulty(DungeonDifficulty curDifficulty)
		{
			bool isHard = curDifficulty == DungeonDifficulty.Hard;
			Color difficultyColor = theme.Difficulty(curDifficulty);
			difficultyRing.color = difficultyColor;
			difficultyRing.SetFill(isHard ? 1f : dungeonManager.Context.DifficultyProgress);
			difficultyText.fontSize = isHard ? hardFontSize : normalFontSize;
			double elapsedSeconds = dungeonManager.Context.ElapsedTime.TotalSeconds;
			difficultyRemainText.text = isHard ? string.Empty : TimeSpan.FromSeconds(difficultyStepSeconds - elapsedSeconds % difficultyStepSeconds).ToString(@"m\:ss");
			Color haloColor = difficultyColor;
			haloColor.a = UIEffects.Enabled ? Mathf.Lerp(haloMinAlpha, haloMaxAlpha, Mathf.PingPong(Time.unscaledTime * haloPulseSpeed, 1f)) : haloMinAlpha;
			difficultyHalo.color = haloColor;
			if (curDifficulty != lastDifficulty)
			{
				lastDifficulty = curDifficulty;
				difficultyBurst.Play(difficultyColor);
				UIEffects.Punch(difficultyText.transform, theme.PunchAmount, theme.PunchDuration);
			}

			switch (curDifficulty)
			{
				case DungeonDifficulty.Easy:
					difficultyText.text = "쉬움";
					break;
				case DungeonDifficulty.Normal:
					difficultyText.text = "보통";
					break;
				case DungeonDifficulty.Hard:
					difficultyText.text = "어려움";
					break;
				default:
					break;
			}
		}

		private void UpdateNextEvent()
		{
			if (monsterSpawner == null)
				monsterSpawner = FindAnyObjectByType<MonsterSpawner>(FindObjectsInactive.Include);
			SurvivalEventEntry nextEvent = monsterSpawner.NextEvent;
			nextEventText.gameObject.SetActive(nextEvent != null);
			if (nextEvent == null)
				return;
			float remain = Mathf.Max(0f, nextEvent.Time - (float)dungeonManager.Context.ElapsedTime.TotalSeconds);
			bool isElite = nextEvent.Type == SurvivalEventType.Elite;
			string label = isElite ? eliteLabel : swarmLabel;
			UITermIcon.Attach(nextEventText, theme, isElite ? SurvivalTerm.NextElite : SurvivalTerm.NextWave);
			string remainText = TimeSpan.FromSeconds(remain).ToString(@"m\:ss");
			nextEventText.text = $"{label} {remainText}";
			nextEventText.color = remain <= nextEvent.AnnounceLead ? nextEventSoonColor : nextEventNormalColor;
		}

		public override void UpdateUI()
		{
			UpdateDifficulty(dungeonManager.Context.CurDifficulty);
			UpdateTime(dungeonManager.Context.DungeonCurTime);
			UpdateNextEvent();
			UpdateRunEquipment();
			questGrid.UpdateUI();
		}
	}
}
