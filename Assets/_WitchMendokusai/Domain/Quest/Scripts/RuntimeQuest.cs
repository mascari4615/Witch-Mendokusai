using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	public class RuntimeQuest : ISavable<RuntimeQuestSaveData>
	{
		public Guid? Guid { get; private set; }
		public RuntimeQuestState State { get; private set; }

		public int QuestSOID { get; private set; } = -1;

		public string Name { get; private set; }
		public string Description { get; private set; }

		public QuestType Type { get; private set; }
		public List<GameEventType> GameEvents { get; private set; }
		public List<RuntimeCriteria> Criteria { get; private set; }
		public List<EffectInfoData> CompleteEffects { get; private set; }
		public List<EffectInfoData> RewardEffects { get; private set; }
		public List<RewardInfoData> Rewards { get; private set; }

		public float WorkTime { get; private set; }
		public bool AutoWork { get; private set; }
		public bool AutoComplete { get; private set; }

		public RuntimeQuest(RuntimeQuestSaveData saveData)
		{
			Load(saveData);

			StartQuest();
		}

		public RuntimeQuest(QuestSO questSO)
		{
			Debug.Log(nameof(RuntimeQuest) + " " + questSO.ID);
			QuestSOID = questSO.ID;
			Initialize(questSO.Data, questSO.Name, questSO.Description);
			StartQuest();
		}

		public RuntimeQuest(QuestInfo questInfo, string name = null, string description = null)
		{
			Initialize(questInfo, name, description);
			StartQuest();
		}

		private void Initialize(QuestInfo questInfo, string name = null, string description = null)
		{
			Guid = System.Guid.NewGuid();

			Name = name;
			Description = description;

			Type = questInfo.Type;
			GameEvents = questInfo.GameEvents.ToList();
			Criteria = questInfo.Criteria.ConvertAll(criteriaData => new RuntimeCriteria(criteriaData));
			CompleteEffects = questInfo.CompleteEffects.ConvertAll(effectData => effectData.ToInfoData());
			RewardEffects = questInfo.RewardEffects.ConvertAll(effectData => effectData.ToInfoData());
			Rewards = questInfo.Rewards.ConvertAll(rewardData => rewardData.ToInfoData());

			WorkTime = questInfo.WorkTime;
			AutoWork = questInfo.AutoWork;
			AutoComplete = questInfo.AutoComplete;
		}

		public void StartQuest()
		{
			if (AutoComplete)
				GameEvents.Add(GameEventType.OnTick);
			foreach (GameEventType gameEventType in GameEvents)
				GameEventManager.Instance.RegisterCallback(gameEventType, Evaluate);
			Evaluate();
		}

		public void Evaluate()
		{
			if (State == RuntimeQuestState.Completed)
				return;

			if (Type == QuestType.VillageRequest)
			{
				if (State >= RuntimeQuestState.Working)
					return;
			}

			foreach (RuntimeCriteria criteria in Criteria)
			{
				criteria.Evaluate();
				if (criteria.IsCompleted == false)
				{
					State = RuntimeQuestState.InProgress;
					return;
				}
			}

			if (Type == QuestType.VillageRequest)
			{
				State = RuntimeQuestState.CanWork;
				if (AutoWork)
					StartWork();
			}
			else
			{
				State = RuntimeQuestState.CanComplete;

				if (AutoComplete)
					Complete();
			}
		}

		public void StartWork(int workerID = WorkConstants.NONE_WORKER_ID)
		{
			State = RuntimeQuestState.Working;

			foreach (GameEventType gameEventType in GameEvents)
				GameEventManager.Instance.UnregisterCallback(gameEventType, Evaluate);

			EventBus.Instance.Publish(new QuestWorkStartedEvent(Guid, workerID, WorkTime));
		}

		public void EndWork()
		{
			State = RuntimeQuestState.CanComplete;

			if (AutoComplete)
				Complete();
		}

		public void Complete()
		{
			State = RuntimeQuestState.Completed;

			if (QuestSOID != -1 && Type == QuestType.Research)
			{
				GameEventManager.Instance.Raise(GameEventType.OnResearchComplete);
			}

			foreach (GameEventType gameEventType in GameEvents)
				GameEventManager.Instance.UnregisterCallback(gameEventType, Evaluate);

			EventBus.Instance.Publish(new QuestCompletedEvent(Guid, QuestSOID, Type));
		}

		public void Load(RuntimeQuestSaveData saveData)
		{
			Guid = saveData.Guid;
			State = saveData.State;

			QuestSOID = saveData.SO_ID;

			Name = saveData.Name;
			Description = saveData.Description;

			Type = saveData.Type;
			GameEvents = saveData.GameEvents;
			Criteria = saveData.Criteria.ConvertAll(criteriaData => new RuntimeCriteria(criteriaData));
			CompleteEffects = saveData.CompleteEffects;
			RewardEffects = saveData.RewardEffects;
			Rewards = saveData.Rewards;

			WorkTime = saveData.WorkTime;
			AutoWork = saveData.AutoWork;
			AutoComplete = saveData.AutoComplete;
		}

		public RuntimeQuestSaveData Save()
		{
			return new RuntimeQuestSaveData
			{
				Guid = Guid,
				State = State,

				SO_ID = QuestSOID,

				Name = Name,
				Description = Description,

				Type = Type,
				GameEvents = GameEvents,
				Criteria = Criteria.ConvertAll(criteria => criteria.Save()),
				CompleteEffects = CompleteEffects,
				RewardEffects = RewardEffects,
				Rewards = Rewards,

				WorkTime = WorkTime,
				AutoWork = AutoWork,
				AutoComplete = AutoComplete
			};
		}
	}
}