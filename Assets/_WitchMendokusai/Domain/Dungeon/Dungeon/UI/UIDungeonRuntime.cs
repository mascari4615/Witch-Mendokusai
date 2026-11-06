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
		[SerializeField] private string battleHudObjectName = "[Block] Lower";
		private CardManager cardManager;
		private CanvasGroup headerGroup;
		private CanvasGroup battleHudGroup;
		private bool isSelecting;
		private UIQuestGrid questGrid;
		private Coroutine loop;

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

		public override void UpdateUI()
		{
			UpdateDifficulty(dungeonManager.Context.CurDifficulty);
			UpdateTime(dungeonManager.Context.DungeonCurTime);
			questGrid.UpdateUI();
		}
	}
}
