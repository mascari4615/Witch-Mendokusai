using System;
using System.Collections.Generic;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class QuestManager : IQuestManager, IDisposable
	{
		// 마도서 첫 페이지의 퀘스트 번호. 다음 페이지는 앞 페이지의 완료 효과 (UnlockQuest, AddQuest) 가 엶
		public const int FIRST_MAGIC_BOOK_PAGE_ID = 7000;
		// 새 게임이 여는 첫 퀘스트. 정사 챕터 첫 노드만. 시제품 챕터 (6000 번대) 는 따로 엶
		public const int FIRST_STORY_QUEST_ID = 5000;

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

		// 진행도 셈. 전에는 static 확장 (RuntimeQuestExtensions) 이 DataManager.Instance 를 찾던 것. 일하는 중이면 그 일의 진행, 아니면 조건의 평균
		public float ProgressOf(RuntimeQuest quest)
		{
			if (quest.State == RuntimeQuestState.Working)
			{
				if (dataManager.WorkManager.TryGetWorkByQuestGuid(quest.Guid, out Work work))
				{
					return work.GetProgress();
				}
				return 0f;
			}

			if (quest.Criteria.Count == 0)
				return 1f;

			float progress = 0f;
			foreach (RuntimeCriteria runtimeCriteria in quest.Criteria)
			{
				progress += runtimeCriteria.GetProgress();
			}
			return progress / quest.Criteria.Count;
		}

		public string ProgressTextOf(RuntimeQuest quest)
		{
			if (quest.State == RuntimeQuestState.Working)
			{
				if (dataManager.WorkManager.TryGetWorkByQuestGuid(quest.Guid, out Work work))
				{
					return work.GetProgress().ToString("P0");
				}
				return string.Empty;
			}

			if (quest.Criteria.Count == 0)
				return "100%";

			// 조건이 하나면 그 값 (단위 포함). 여럿이면 단위가 섞여 더할 수 없어 평균 진행률
			if (quest.Criteria.Count == 1)
				return CriteriaText.ProgressOf(quest.Criteria[0]);
			return Mathf.Min(ProgressOf(quest), 1f).ToString("P0");
		}

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
			// 그냥 비우면 목록에 없는 퀘스트가 게임 이벤트를 계속 들음
			foreach (RuntimeQuest oldQuest in Quests.Data)
			{
				oldQuest.StopQuest();
			}
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
			quest.StartQuest();
			// 목록 화면은 넣을 때 그려져 시작 전 상태라 다시
			Quests.UpdateUI();
		}

		public RuntimeQuest GetQuest(QuestSO questData)
		{
			return Quests.Data.Find(x => x.QuestSOID == questData.ID);
		}

		public RuntimeQuest GetQuest(Guid? guid)
		{
			return Quests.Data.Find(x => x.Guid == guid);
		}

		// 지금 채우는 마도서 페이지. 페이지는 한 번에 하나만 열림. 다 채웠거나 아직 시작 전이면 null
		public RuntimeQuest CurrentMagicBookPage => Quests.Data.Find(x => x.Type == QuestType.MagicBookPage);

		// 마도서 첫 페이지를 실행 중 퀘스트로. 잠겨 있을 때만 (새 게임, 페이지가 생기기 전의 세이브). 이미 열었거나 마친 세이브는 그대로
		public void StartMagicBook(QuestSO firstPage)
		{
			if (GetQuestState(firstPage.ID) != QuestState.Locked)
			{
				return;
			}
			UnlockQuest(firstPage);
			AddQuest(RuntimeQuestFactory.FromQuestSO(firstPage, CreateCriteriaContext()));
		}

		// 정사 챕터 첫 노드. 잠겨 있을 때만 (새 게임). 다른 여는 길이 없어 잠긴 채 남으면 이야기가 막힘
		public void UnlockFirstStoryQuest()
		{
			if (GetQuestState(FIRST_STORY_QUEST_ID) != QuestState.Locked)
			{
				return;
			}
			UnlockQuest(SOHelper.GetQuestSO(FIRST_STORY_QUEST_ID));
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
			foreach (RuntimeQuest quest in Quests.Data.FindAll(x => x.Type == questType))
			{
				quest.StopQuest();
			}
			Quests.Data.RemoveAll(x => x.Type == questType);
		}

		public int GetQuestCount(QuestType questType)
		{
			return Quests.Data.FindAll(x => x.Type == questType).Count;
		}
	}
}