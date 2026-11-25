using System;
using System.Collections.Generic;
using MessagePipe;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 세이브의 퀘스트를 불러와 목록에 넣는 것. 목록에 들기 전에 끝나면 관리자가 못 찾아 완료 효과와 보상이 안 나옴
	public sealed class QuestLoadTest
	{
		private sealed class Channel<T> : IPublisher<T>, ISubscriber<T>, IDisposable
		{
			private readonly List<IMessageHandler<T>> handlers = new();

			public void Publish(T message) => handlers.ForEach(handler => handler.Handle(message));

			public IDisposable Subscribe(IMessageHandler<T> handler, params MessageHandlerFilter<T>[] filters)
			{
				handlers.Add(handler);
				return this;
			}

			public void Dispose() => handlers.Clear();
		}

		private sealed class CountingEffectRunner : IEffectRunner
		{
			public int AppliedCount { get; private set; }

			public void ApplyEffects(List<EffectInfo> effectInfos) => AppliedCount += effectInfos.Count;
			public void ApplyEffects(List<EffectInfoData> effectInfoData) => AppliedCount += effectInfoData.Count;
			public void ApplyEffect(EffectInfo effectInfo) => AppliedCount++;
			public void BindDataManager(DataManager dataManager) { }
			public void BindScene(UIManager uiManager, DialogueRunner dialogueRunner) { }
		}

		private sealed class SilentGameEvents : IGameEventBridge
		{
			public void Raise(GameEventType gameEventType) { }
			public void RegisterCallback(GameEventType gameEventType, Action action) { }
			public void UnregisterCallback(GameEventType gameEventType, Action action) { }
		}

		private SOManager soManager;
		private QuestBuffer questBuffer;
		private CountingEffectRunner effectRunner;
		private Channel<QuestCompletedEvent> completed;
		private QuestManager questManager;

		[SetUp]
		public void SetUp()
		{
			GameEventBridge.Register(new SilentGameEvents());
			EventBusBridge.UseTransport(new InMemoryEventTransport());

			questBuffer = ScriptableObject.CreateInstance<QuestBuffer>();
			soManager = ScriptableObject.CreateInstance<SOManager>();
			SerializedObject serialized = new(soManager);
			serialized.FindProperty("<QuestBuffer>k__BackingField").objectReferenceValue = questBuffer;
			serialized.ApplyModifiedPropertiesWithoutUndo();

			effectRunner = new CountingEffectRunner();
			completed = new Channel<QuestCompletedEvent>();
			EventBusBridge.Subscribe<QuestCompletedEvent>(completed.Publish);

			questManager = new QuestManager();
			questManager.Construct(new Channel<QuestAddedEvent>(), completed, new Channel<QuestAddRequestedEvent>(), new Channel<QuestUnlockRequestedEvent>(), soManager, effectRunner, null);
		}

		[TearDown]
		public void TearDown()
		{
			EventBusBridge.Unsubscribe<QuestCompletedEvent>(completed.Publish);
			questManager.Dispose();
			EventBusBridge.UseTransport(new InMemoryEventTransport());
			GameEventBridge.Register(null);
			UnityEngine.Object.DestroyImmediate(soManager);
			UnityEngine.Object.DestroyImmediate(questBuffer);
		}

		// 조건이 없는 퀘스트는 언제나 조건이 찬 것
		private static RuntimeQuestSaveData SavedQuest(bool autoComplete) => new()
		{
			Guid = Guid.NewGuid(),
			State = RuntimeQuestState.InProgress,
			SO_ID = -1,
			Type = QuestType.Normal,
			GameEvents = new List<GameEventType>(),
			Criteria = new List<RuntimeCriteriaSaveData>(),
			CompleteEffects = new List<EffectInfoData> { new() },
			RewardEffects = new List<EffectInfoData> { new() },
			Rewards = new List<RewardInfoData>(),
			AutoComplete = autoComplete,
		};

		[Test]
		public void Saved_quest_already_met_pays_once_and_leaves_the_list()
		{
			RuntimeQuest quest = RuntimeQuestFactory.FromSaveData(SavedQuest(autoComplete: true));
			questManager.Init(new List<RuntimeQuest> { quest });

			Assert.AreEqual(RuntimeQuestState.Completed, quest.State);
			Assert.AreEqual(2, effectRunner.AppliedCount, "완료 효과 하나와 보상 효과 하나");
			Assert.IsNull(questManager.GetQuest(quest.Guid), "끝난 퀘스트는 목록에 남지 않음");
		}

		[Test]
		public void Saved_quest_waiting_for_the_player_stays_in_the_list()
		{
			RuntimeQuest quest = RuntimeQuestFactory.FromSaveData(SavedQuest(autoComplete: false));
			questManager.Init(new List<RuntimeQuest> { quest });

			Assert.AreEqual(RuntimeQuestState.CanComplete, quest.State);
			Assert.AreEqual(0, effectRunner.AppliedCount);
			Assert.AreSame(quest, questManager.GetQuest(quest.Guid));
		}
	}
}
