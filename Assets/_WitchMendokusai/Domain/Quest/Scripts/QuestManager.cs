using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class QuestManager : IQuestManager
	{
		public static QuestManager Instance => DataManager.Instance.QuestManager;

		public QuestBuffer Quests => SOManager.Instance.QuestBuffer;

		private Dictionary<int, QuestState> questStates = new();
		public void LoadQuestState(Dictionary<int, QuestState> questStates) => this.questStates = questStates;
		public Dictionary<int, QuestState> GetQuestStates() => questStates;
		public void SetQuestState(int questID, QuestState state) => questStates[questID] = state;
		public QuestState GetQuestState(int questID) => questStates[questID];

		public void Init(List<RuntimeQuest> quests)
		{
			Quests.Clear();

			EventBusBridge.Unsubscribe<QuestCompletedEvent>(OnQuestCompleted);
			EventBusBridge.Subscribe<QuestCompletedEvent>(OnQuestCompleted);

			foreach (RuntimeQuest quest in quests)
			{
				Quests.Add(quest);
				quest.StartQuest();
			}
		}

		private void OnQuestCompleted(QuestCompletedEvent evt)
		{
			RuntimeQuest quest = GetQuest(evt.Guid);
			if (quest == null)
			{
				return;
			}

			Effect.ApplyEffects(quest.CompleteEffects);
			Effect.ApplyEffects(quest.RewardEffects);
			foreach (RewardInfoData rewardData in quest.Rewards)
			{
				Reward.GetReward(rewardData);
			}

			Quests.Remove(quest);

			if (evt.QuestSOID != -1)
			{
				questStates[evt.QuestSOID] = QuestState.Completed;
			}
		}

		public void AddQuest(RuntimeQuest quest)
		{
			Quests.Add(quest);
			EventBusBridge.Publish(new QuestAddedEvent(quest));
		}

		public RuntimeQuest GetQuest(QuestSO questData)
		{
			return Quests.Data.Find(x => x.QuestSOID == questData.ID);
		}

		public RuntimeQuest GetQuest(Guid? guid)
		{
			return Quests.Data.Find(x => x.Guid == guid);
		}

		public void UnlockQuest(QuestSO questData)
		{
			questStates[questData.ID] = QuestState.Unlocked;

			List<EffectInfo> effects = questData.Data.UnlockEffects;

			foreach (EffectInfo effect in effects)
			{
				Effect.ApplyEffect(effect);
			}
		}

		public void CompleteQuest(Guid? guid)
		{
			GetQuest(guid).Complete();
		}

		public void EndQuestWork(Guid? guid)
		{
			GetQuest(guid).EndWork();
		}

		public void RemoveQuests(QuestType questType)
		{
			Quests.Data.RemoveAll(x => x.Type == questType);
		}

		public int GetQuestCount(QuestType questType)
		{
			return Quests.Data.FindAll(x => x.Type == questType).Count;
		}
	}
}