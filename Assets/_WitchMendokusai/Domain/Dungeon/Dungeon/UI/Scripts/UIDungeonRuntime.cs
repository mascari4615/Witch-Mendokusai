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
		[SerializeField] private Image difficultyCircle;
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
			timeText.text = timeSpan.ToString(@"mm\:ss");
		}

		private void UpdateDifficulty(DungeonDifficulty curDifficulty)
		{
			if (curDifficulty == DungeonDifficulty.Hard)
			{
				difficultyCircle.fillAmount = 1;
			}
			else
			{
				difficultyCircle.fillAmount = (float)(dungeonManager.Context.DungeonCurTime.TotalSeconds % 180f / 180f);
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
