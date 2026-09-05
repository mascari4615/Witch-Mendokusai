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
		private IEffectRunner effectRunner;
		private PlayerProvider playerProvider;
		// TASK-WM-107 Slice 2C-3 — DataManager↔QuestManager 순환 회피: [Inject] pull 대신 소유자(DataManager.Construct) push.
		private DataManager dataManager;
		public void BindDataManager(DataManager dataManager) => this.dataManager = dataManager;

		[Inject]
		public void Construct(IPublisher<QuestAddedEvent> questAddedPublisher, ISubscriber<QuestCompletedEvent> questCompletedSubscriber, ISubscriber<QuestAddRequestedEvent> questAddRequestedSubscriber, ISubscriber<QuestUnlockRequestedEvent> questUnlockRequestedSubscriber, SOManager soManager, IEffectRunner effectRunner, PlayerProvider playerProvider)
		{
			this.questAddedPublisher = questAddedPublisher;
			questCompletedSub = questCompletedSubscriber.Subscribe(OnQuestCompleted);
			questAddRequestedSub = questAddRequestedSubscriber.Subscribe(OnQuestAddRequested);
			questUnlockRequestedSub = questUnlockRequestedSubscriber.Subscribe(OnQuestUnlockRequested);
			this.soManager = soManager;
			this.effectRunner = effectRunner;
			this.playerProvider = playerProvider;
		}

		public void Dispose()
		{
			questCompletedSub?.Dispose();
			questAddRequestedSub?.Dispose();
			questUnlockRequestedSub?.Dispose();
		}

		// TASK-WM-107 Slice 2C-4 — ctx 조립 단일 지점. 모든 RuntimeQuestFactory 호출처(OnQuestAddRequested /
		// UINPCMenu / DungeonManager / SaveManager Load)가 여기서 ctx 획득 = DRY, POCO Criteria Bridge 의존 완전 폐기.
		public CriteriaContext CreateCriteriaContext() => new(soManager, playerProvider, dataManager);

		// POCO Effect 가 발행한 명령 이벤트 구독 — QuestManagerBridge static 폐기 (TASK-WM-107 Slice 1).
		private void OnQuestAddRequested(QuestAddRequestedEvent evt)
		{
			QuestSO questSO = SOHelper.GetQuestSO(evt.QuestSOID);
			AddQuest(RuntimeQuestFactory.FromQuestSO(questSO, CreateCriteriaContext()));
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

			// 모드(IMod)가 등록한 quest 를 게임에 설치 — 등록 콘텐츠 inert 해소 (TASK-WM-188 deepening).
			ModQuestInstaller.InstallInto(Quests, ModLoader.Content.RegisteredQuests);
		}

		private void OnQuestCompleted(QuestCompletedEvent evt)
		{
			RuntimeQuest quest = GetQuest(evt.Guid);
			if (quest == null)
			{
				return;
			}

			effectRunner.ApplyEffects(quest.CompleteEffects);
			effectRunner.ApplyEffects(quest.RewardEffects);
			foreach (RewardInfoData rewardData in quest.Rewards)
			{
				Reward.GetReward(rewardData, soManager.ItemInventory, dataManager.GameStat);
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
				effectRunner.ApplyEffect(effect);
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