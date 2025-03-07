using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UIQuestToolTip : MonoBehaviour, IUI
	{
		[SerializeField] private Button workButton;
		[SerializeField] private Button rewardButton;
		[SerializeField] private UIRewards rewardUI;
		[SerializeField] private UIQuestTooltipCriteria questCriteriaUI;

		private RuntimeQuest curQuest;

		public void Init()
		{
			if (rewardUI != null)
				rewardUI.Init();

			if (questCriteriaUI != null)
				questCriteriaUI.Init();

			if (workButton != null)
				workButton.onClick.AddListener(() =>
				{
					// TODO: 어떤 인형이 일을 할지
					if (curQuest.State != RuntimeQuestState.CanWork)
						return;

					curQuest.StartWork(0);
					UpdateUI();
				});

			if (rewardButton != null)
				rewardButton.onClick.AddListener(() =>
				{
					if (curQuest.State != RuntimeQuestState.CanComplete)
						return;

					curQuest.Complete();
					UpdateUI();
				});
		}

		public void SetQuest(RuntimeQuest newQuest)
		{
			// Debug.Log($"SetQuest {newQuest}");
			curQuest = newQuest;
		}

		public void UpdateUI()
		{
			if (curQuest?.SO)
			{
				// Debug.Log($"A {curQuest.State == RuntimeQuestState.CanWork}, {curQuest.State == RuntimeQuestState.CanComplete}");
				if (workButton != null)
					workButton.gameObject.SetActive(curQuest.State == RuntimeQuestState.CanWork);
				if (rewardButton != null)
					rewardButton.gameObject.SetActive(curQuest.State == RuntimeQuestState.CanComplete);
			}
			else
			{
				// Debug.Log($"B");
				if (workButton != null)
					workButton.gameObject.SetActive(false);
				if (rewardButton != null)
					rewardButton.gameObject.SetActive(false);
			}

			if (rewardUI != null)
				rewardUI.UpdateUI(curQuest?.Rewards);

			if (questCriteriaUI != null)
				questCriteriaUI.SetCriteria(curQuest);
		}
	}
}