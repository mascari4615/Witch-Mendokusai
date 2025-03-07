using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class QuestManager
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

			foreach (RuntimeQuest quest in quests)
			{
				Quests.Add(quest);
				quest.StartQuest();
			}
		}

		public void AddQuest(RuntimeQuest quest)
		{
			Quests.Add(quest);
		}

		public RuntimeQuest GetQuest(QuestSO questData)
		{
			return Quests.Datas.Find(x => x.SO?.ID == questData.ID);
		}

		public RuntimeQuest GetQuest(Guid? guid)
		{
			return Quests.Datas.Find(x => x.Guid == guid);
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

		public void RemoveQuest(RuntimeQuest quest)
		{
			Guid? guid = quest.Guid;
			RuntimeQuest runtimeQuest = GetQuest(guid);

			if (Quests.Remove(runtimeQuest) == false)
			{
				Debug.Log("Quest not found");
			}
		}

		public void RemoveQuests(QuestType questType)
		{
			Quests.Datas.RemoveAll(x => x.Type == questType);
		}

		public int GetQuestCount(QuestType questType)
		{
			return Quests.Datas.FindAll(x => x.Type == questType).Count;
		}
	}
}