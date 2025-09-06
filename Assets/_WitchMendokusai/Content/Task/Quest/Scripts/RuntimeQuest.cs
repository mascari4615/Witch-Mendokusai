using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public enum RuntimeQuestState
	{
		InProgress,
		CanWork,
		Working,
		CanComplete,
		Completed,
	}

	public class RuntimeQuest : ISavable<RuntimeQuestSaveData>
	{
		public Guid? Guid { get; private set; }
		public RuntimeQuestState State { get; private set; }

		public QuestSO SO { get; private set; }

		public string Name { get; private set; }
		public string Description { get; private set; }

		public QuestGroup Group { get; private set; }
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
			SO = questSO;
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

			Group = questInfo.Group;
			GameEvents = questInfo.GameEvents.ToList();
			Criteria = questInfo.Criteria.ConvertAll(criteriaData => new RuntimeCriteria(criteriaData));
			CompleteEffects = questInfo.CompleteEffects.ConvertAll(effectData => new EffectInfoData(effectData));
			RewardEffects = questInfo.RewardEffects.ConvertAll(effectData => new EffectInfoData(effectData));
			Rewards = questInfo.Rewards.ConvertAll(rewardData => new RewardInfoData(rewardData));

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

			if (Group == QuestGroup.VillageRequest)
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

			if (Group == QuestGroup.VillageRequest)
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

		public void StartWork(int workerID = WorkManager.NONE_WORKER_ID)
		{
			State = RuntimeQuestState.Working;

			foreach (GameEventType gameEventType in GameEvents)
				GameEventManager.Instance.UnregisterCallback(gameEventType, Evaluate);
			Work work = new(workerID, WorkType.QuestWork, Guid, WorkTime);
			DataManager.Instance.WorkManager.AddWork(work);
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

			QuestManager.Instance.RemoveQuest(this);

			if (SO != null)
			{
				QuestManager.Instance.SetQuestState(SO.ID, QuestState.Completed);
				if (Group == QuestGroup.Achievement)
					UIManager.Instance?.Popup(SO);
			}

			foreach (GameEventType gameEventType in GameEvents)
				GameEventManager.Instance.UnregisterCallback(gameEventType, Evaluate);
			Effect.ApplyEffects(CompleteEffects);
			GetReward();
		}

		private void GetReward()
		{
			Effect.ApplyEffects(RewardEffects);

			foreach (RewardInfoData rewardData in Rewards)
				Reward.GetReward(rewardData);
		}

		public float GetProgress()
		{
			if (State == RuntimeQuestState.Working)
			{
				if (DataManager.Instance.WorkManager.TryGetWorkByQuestGuid(Guid, out Work work))
				{
					return work.GetProgress();
				}
				return 0;
			}

			if (Criteria.Count == 0)
				return 1;

			float progress = 0;
			foreach (RuntimeCriteria runtimeCriteria in Criteria)
				progress += runtimeCriteria.GetProgress();
			return progress /= Criteria.Count;
		}

		public string GetProgressText()
		{
			if (State == RuntimeQuestState.Working)
			{
				if (DataManager.Instance.WorkManager.TryGetWorkByQuestGuid(Guid, out Work work))
				{
					return work.GetProgress().ToString("P0");
				}
				return string.Empty;
			}

			if (Criteria.Count == 0)
				return "100%";

			float curValue = 0;
			float targetValue = 0;
			foreach (RuntimeCriteria runtimeCriteria in Criteria)
			{
				curValue += runtimeCriteria.GetCurValue();
				targetValue += runtimeCriteria.GetTargetValue();
			}
			return $"{curValue} / {targetValue}";
		}

		public void Load(RuntimeQuestSaveData saveData)
		{
			Guid = saveData.Guid;
			State = saveData.State;

			SO = saveData.SO_ID != -1 ? GetQuestSO(saveData.SO_ID) : null;

			Group = saveData.Group;
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

				SO_ID = SO != null ? SO.ID : -1,

				Group = Group,
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