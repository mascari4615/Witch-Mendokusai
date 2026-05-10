using System;
using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	public static class RuntimeQuestFactory
	{
		public static RuntimeQuest FromQuestSO(QuestSO questSO)
		{
			return FromQuestInfo(questSO.Data, questSO.Name, questSO.Description, questSO.ID);
		}

		public static RuntimeQuest FromQuestInfo(QuestInfo questInfo, string name = null, string description = null, int questSOID = -1)
		{
			RuntimeQuestSaveData saveData = new()
			{
				Guid = System.Guid.NewGuid(),
				State = RuntimeQuestState.InProgress,

				SO_ID = questSOID,

				Name = name,
				Description = description,

				Type = questInfo.Type,
				GameEvents = questInfo.GameEvents.ToList(),
				Criteria = questInfo.Criteria.ConvertAll(c => new RuntimeCriteriaSaveData
				{
					CriteriaInfo = c.ToSaveData(),
					JustOnce = c.JustOnce,
					IsCompleted = false,
				}),
				CompleteEffects = questInfo.CompleteEffects.ConvertAll(effectData => effectData.ToInfoData()),
				RewardEffects = questInfo.RewardEffects.ConvertAll(effectData => effectData.ToInfoData()),
				Rewards = questInfo.Rewards.ConvertAll(rewardData => rewardData.ToInfoData()),

				WorkTime = questInfo.WorkTime,
				AutoWork = questInfo.AutoWork,
				AutoComplete = questInfo.AutoComplete,
			};

			List<RuntimeCriteria> criteria = questInfo.Criteria.ConvertAll(RuntimeCriteriaFactory.FromCriteriaInfo);

			RuntimeQuest runtimeQuest = new(saveData)
			{
				Criteria = criteria,
			};
			runtimeQuest.StartQuest();
			return runtimeQuest;
		}

		public static RuntimeQuest FromSaveData(RuntimeQuestSaveData saveData)
		{
			List<RuntimeCriteria> criteria = saveData.Criteria.ConvertAll(RuntimeCriteriaFactory.FromSaveData);

			RuntimeQuest runtimeQuest = new(saveData)
			{
				Criteria = criteria,
			};
			runtimeQuest.StartQuest();
			return runtimeQuest;
		}
	}
}
