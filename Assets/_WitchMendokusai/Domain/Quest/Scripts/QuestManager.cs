using System;
using System.Collections.Generic;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class QuestManager : IQuestManager, IDisposable
	{
		public static QuestManager Instance => DataManager.Instance.QuestManager;

		public QuestBuffer Quests => soManager.QuestBuffer;

		private Dictionary<int, QuestState> questStates = new();
		public void LoadQuestState(Dictionary<int, QuestState> questStates) => this.questStates = questStates;
		public Dictionary<int, QuestState> GetQuestStates() => questStates;
		public void SetQuestState(int questID, QuestState state) => questStates[questID] = state;
		public QuestState GetQuestState(int questID) => questStates[questID];

		private IPublisher<QuestAddedEvent> questAddedPublisher;
		private IDisposable questCompletedSub;
		private IDisposable questAddRequestedSub;
		private IDisposable questUnlockRequestedSub;
		private SOManager soManager;

		[Inject]
		public void Construct(IPublisher<QuestAddedEvent> questAddedPublisher, ISubscriber<QuestCompletedEvent> questCompletedSubscriber, ISubscriber<QuestAddRequestedEvent> questAddRequestedSubscriber, ISubscriber<QuestUnlockRequestedEvent> questUnlockRequestedSubscriber, SOManager soManager)
		{
			this.questAddedPublisher = questAddedPublisher;
			questCompletedSub = questCompletedSubscriber.Subscribe(OnQuestCompleted);
			questAddRequestedSub = questAddRequestedSubscriber.Subscribe(OnQuestAddRequested);
			questUnlockRequestedSub = questUnlockRequestedSubscriber.Subscribe(OnQuestUnlockRequested);
			this.soManager = soManager;
		}

		public void Dispose()
		{
			questCompletedSub?.Dispose();
			questAddRequestedSub?.Dispose();
			questUnlockRequestedSub?.Dispose();
		}

		// POCO Effect 가 발행한 명령 이벤트 구독 — QuestManagerBridge static 폐기 (TASK-WM-107 Slice 1).
		private void OnQuestAddRequested(QuestAddRequestedEvent evt)
		{
			QuestSO questSO = SOHelper.GetQuestSO(evt.QuestSOID);
			AddQuest(RuntimeQuestFactory.FromQuestSO(questSO));
		}

		private void OnQuestUnlockRequested(QuestUnlockRequestedEvent evt) => UnlockQuest(SOHelper.GetQuestSO(evt.QuestSOID));

		public void Init(List<RuntimeQuest> quests)
		{
			Quests.Clear();

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
			questAddedPublisher.Publish(new QuestAddedEvent(quest));
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