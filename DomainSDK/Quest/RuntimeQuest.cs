using System;
using System.Collections.Generic;

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
		public List<RuntimeCriteria> Criteria { get; set; }
		public List<EffectInfoData> CompleteEffects { get; private set; }
		public List<EffectInfoData> RewardEffects { get; private set; }
		public List<RewardInfoData> Rewards { get; private set; }

		public float WorkTime { get; private set; }
		public bool AutoWork { get; private set; }
		public bool AutoComplete { get; private set; }

		public RuntimeQuest(RuntimeQuestSaveData saveData)
		{
			Load(saveData);
		}

		public void StartQuest()
		{
			if (State == RuntimeQuestState.Completed || State == RuntimeQuestState.Working)
			{
				return;
			}

			if (AutoComplete)
			{
				GameEvents.Add(GameEventType.OnTick);
			}

			foreach (GameEventType gameEventType in GameEvents)
			{
				GameEventBridge.RegisterCallback(gameEventType, Evaluate);
			}

			Evaluate();
		}

		public void Evaluate()
		{
			if (State == RuntimeQuestState.Completed)
			{
				return;
			}

			if (Type == QuestType.VillageRequest)
			{
				if (State >= RuntimeQuestState.Working)
				{
					return;
				}
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
				{
					StartWork();
				}
			}
			else
			{
				State = RuntimeQuestState.CanComplete;

				if (AutoComplete)
				{
					Complete();
				}
			}
		}

		public void StartWork(int workerID = WorkConstants.NONE_WORKER_ID)
		{
			State = RuntimeQuestState.Working;

			foreach (GameEventType gameEventType in GameEvents)
			{
				GameEventBridge.UnregisterCallback(gameEventType, Evaluate);
			}

			EventBusBridge.Publish(new QuestWorkStartedEvent(Guid, workerID, WorkTime));
		}

		public void EndWork()
		{
			State = RuntimeQuestState.CanComplete;

			if (AutoComplete)
			{
				Complete();
			}
		}

		public void Complete()
		{
			State = RuntimeQuestState.Completed;

			if (QuestSOID != -1 && Type == QuestType.Research)
			{
				GameEventBridge.Raise(GameEventType.OnResearchComplete);
			}

			foreach (GameEventType gameEventType in GameEvents)
			{
				GameEventBridge.UnregisterCallback(gameEventType, Evaluate);
			}

			EventBusBridge.Publish(new QuestCompletedEvent(Guid, QuestSOID, Type));
		}

		public void Load(RuntimeQuestSaveData saveData)
		{
			Guid = saveData.Guid;
			State = saveData.State;

			QuestSOID = saveData.SO_ID;

			Name = saveData.Name;
			Description = saveData.Description;

			Type = saveData.Type;
			GameEvents = new List<GameEventType>(saveData.GameEvents);
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
				AutoComplete = AutoComplete,
			};
		}
	}
}
