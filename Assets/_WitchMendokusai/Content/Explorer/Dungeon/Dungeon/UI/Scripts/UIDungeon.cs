using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public enum DungeonPanelType
	{
		None = -1,
		DungeonResult = 0,
	}

	public class UIDungeon : UIContentBase<DungeonPanelType>
	{
		[field: Header("_" + nameof(UIDungeon))]
		[SerializeField] private Image progressBar;
		[SerializeField] private TextMeshProUGUI timeText;
		[SerializeField] private TextMeshProUGUI difficultyText;
		[SerializeField] private Image difficultyCircle;
		private UIQuestGrid questGrid;
		private Coroutine loop;

		public override DungeonPanelType DefaultPanel => DungeonPanelType.None;

		public override void Init()
		{
			Panels[DungeonPanelType.DungeonResult] = FindFirstObjectByType<UIDungeonResult>(FindObjectsInactive.Include);

			questGrid = GetComponentInChildren<UIQuestGrid>(true);
			questGrid.Init();
			questGrid.SetFilter(QuestType.Dungeon);
		}

		public void StartLoop()
		{
			if (loop != null)
				StopCoroutine(loop);
			loop = StartCoroutine(Loop());
			CameraManager.Instance.SetContentCameraMode(ContentCameraMode.Dungeon);
		}

		public void StopLoop()
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

		private void UpdateUI()
		{
			UpdateDifficulty(DungeonManager.Instance.Context.CurDifficulty);
			UpdateTime(DungeonManager.Instance.Context.DungeonCurTime);
			questGrid.UpdateUI();
		}

		private void UpdateTime(TimeSpan timeSpan)
		{
			progressBar.fillAmount = 1 - (float)(timeSpan.TotalSeconds / DungeonManager.Instance.Context.InitialDungeonTime.TotalSeconds);
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
				difficultyCircle.fillAmount = (float)(DungeonManager.Instance.Context.DungeonCurTime.TotalSeconds % 180f / 180f);
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
	}
}