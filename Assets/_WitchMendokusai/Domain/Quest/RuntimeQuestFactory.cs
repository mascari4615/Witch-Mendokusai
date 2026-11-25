using System;
using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	public static class RuntimeQuestFactory
	{
		public static RuntimeQuest FromQuestSO(QuestSO questSO, CriteriaContext context = null)
		{
			return FromQuestInfo(questSO.Data, questSO.Name, questSO.Description, questSO.ID, context);
		}

		public static RuntimeQuest FromQuestInfo(QuestInfo questInfo, string name = null, string description = null, int questSOID = -1, CriteriaContext context = null)
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

			List<RuntimeCriteria> criteria = questInfo.Criteria.ConvertAll(criteriaInfo => RuntimeCriteriaFactory.FromCriteriaInfo(criteriaInfo, context));

			RuntimeQuest runtimeQuest = new(saveData)
			{
				Criteria = criteria,
			};
			runtimeQuest.StartQuest();
			return runtimeQuest;
		}

		// 시작은 부르는 쪽이 목록에 넣은 뒤에 (QuestManager.Init). 여기서 시작하면 조건이 찬 자동 완료가 목록에 들기 전에 끝나 보상이 안 나옴
		public static RuntimeQuest FromSaveData(RuntimeQuestSaveData saveData, CriteriaContext context = null)
		{
			List<RuntimeCriteria> criteria = saveData.Criteria.ConvertAll(criteriaSaveData => RuntimeCriteriaFactory.FromSaveData(criteriaSaveData, context));

			return new RuntimeQuest(saveData)
			{
				Criteria = criteria,
			};
		}
	}
}
