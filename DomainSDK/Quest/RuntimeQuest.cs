using System;
using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	public class RuntimeQuest : ISavable<RuntimeQuestSaveData>
	{
		public Guid? Guid { get; private set; }
		public RuntimeQuestState State { get; private set; }

		public int QuestSOID { get; private set; } = -1;
		public string ModQuestId { get; private set; }

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

		// 지금 듣는 이벤트. 시작을 두 번 불러도 한 번만 듣고, 그만둘 때 들은 것을 전부 풂
		private readonly HashSet<GameEventType> listenedEvents = new();

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

			Listen();
			Evaluate();
		}

		/// <summary>목록에서 치울 때. 더는 게임 이벤트에 반응하지 않음</summary>
		public void StopQuest() => StopListening();

		private void Listen()
		{
			if (listenedEvents.Count > 0)
			{
				return;
			}

			foreach (GameEventType gameEventType in GameEvents)
			{
				listenedEvents.Add(gameEventType);
			}
			// 자동 완료는 매 틱 판정. GameEvents 에 더하면 세이브에 실려 불러올 때마다 늘어남
			if (AutoComplete)
			{
				listenedEvents.Add(GameEventType.OnTick);
			}

			foreach (GameEventType gameEventType in listenedEvents)
			{
				GameEventBridge.RegisterCallback(gameEventType, Evaluate);
			}
		}

		private void StopListening()
		{
			foreach (GameEventType gameEventType in listenedEvents)
			{
				GameEventBridge.UnregisterCallback(gameEventType, Evaluate);
			}
			listenedEvents.Clear();
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

			StopListening();

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

			StopListening();

			EventBusBridge.Publish(new QuestCompletedEvent(Guid, QuestSOID, Type));
		}

		public void Load(RuntimeQuestSaveData saveData)
		{
			Guid = saveData.Guid;
			State = saveData.State;

			QuestSOID = saveData.SO_ID;
			ModQuestId = saveData.ModQuestId;

			Name = saveData.Name;
			Description = saveData.Description;

			Type = saveData.Type;
			// 옛 세이브에는 자동 완료가 불러올 때마다 더한 OnTick 이 쌓여 있음
			GameEvents = saveData.GameEvents.Distinct().ToList();
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
				ModQuestId = ModQuestId,

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
