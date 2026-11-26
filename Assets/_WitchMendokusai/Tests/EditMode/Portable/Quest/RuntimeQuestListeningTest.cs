using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	// 퀘스트가 게임 이벤트를 듣기 시작하고 그만두는 것. 치운 퀘스트가 계속 들으면 없는 퀘스트가 판정됨
	public sealed class RuntimeQuestListeningTest
	{
		private sealed class FakeGameEvents : IGameEventBridge
		{
			private readonly Dictionary<GameEventType, Action> callbacks = new();

			public void Raise(GameEventType gameEventType)
			{
				if (callbacks.TryGetValue(gameEventType, out Action action))
				{
					action?.Invoke();
				}
			}

			public void RegisterCallback(GameEventType gameEventType, Action action)
			{
				callbacks.TryGetValue(gameEventType, out Action existing);
				callbacks[gameEventType] = existing + action;
			}

			public void UnregisterCallback(GameEventType gameEventType, Action action)
			{
				callbacks.TryGetValue(gameEventType, out Action existing);
				callbacks[gameEventType] = existing - action;
			}

			public int CountOf(GameEventType gameEventType)
			{
				callbacks.TryGetValue(gameEventType, out Action action);
				return action == null ? 0 : action.GetInvocationList().Length;
			}
		}

		private sealed class SwitchCriteria : Criteria
		{
			public bool IsMet;
			public int EvaluatedCount;

			public override int GetCurValue() => IsMet ? 1 : 0;
			public override int GetTargetValue() => 1;

			public override bool Evaluate()
			{
				EvaluatedCount++;
				return IsMet;
			}
		}

		private FakeGameEvents gameEvents;
		private SwitchCriteria criteria;

		[SetUp]
		public void SetUp()
		{
			gameEvents = new FakeGameEvents();
			GameEventBridge.Register(gameEvents);
			criteria = new SwitchCriteria();
		}

		[TearDown]
		public void TearDown() => GameEventBridge.Register(null);

		private RuntimeQuest CreateQuest(bool autoComplete, params GameEventType[] events)
		{
			RuntimeQuest quest = new(new RuntimeQuestSaveData
			{
				Guid = System.Guid.NewGuid(),
				State = RuntimeQuestState.InProgress,
				SO_ID = -1,
				GameEvents = new List<GameEventType>(events),
				CompleteEffects = new List<EffectInfoData>(),
				RewardEffects = new List<EffectInfoData>(),
				Rewards = new List<RewardInfoData>(),
				AutoComplete = autoComplete,
			});
			quest.Criteria = new List<RuntimeCriteria> { new RuntimeCriteria(new RuntimeCriteriaSaveData(), criteria) };
			return quest;
		}

		[Test]
		public void Starting_twice_listens_once()
		{
			RuntimeQuest quest = CreateQuest(true, GameEventType.OnResearchComplete);

			quest.StartQuest();
			quest.StartQuest();

			Assert.AreEqual(1, gameEvents.CountOf(GameEventType.OnResearchComplete));
			Assert.AreEqual(1, gameEvents.CountOf(GameEventType.OnTick));
		}

		[Test]
		public void Starting_does_not_grow_what_is_saved()
		{
			RuntimeQuest quest = CreateQuest(true, GameEventType.OnResearchComplete);

			quest.StartQuest();

			CollectionAssert.AreEqual(new[] { GameEventType.OnResearchComplete }, quest.Save().GameEvents);
		}

		[Test]
		public void Saves_that_already_grew_listen_once()
		{
			RuntimeQuest quest = CreateQuest(true, GameEventType.OnTick, GameEventType.OnTick, GameEventType.OnTick);

			quest.StartQuest();

			Assert.AreEqual(1, gameEvents.CountOf(GameEventType.OnTick));
		}

		[Test]
		public void Stopped_quest_hears_nothing()
		{
			RuntimeQuest quest = CreateQuest(true, GameEventType.OnResearchComplete);
			quest.StartQuest();
			quest.StartQuest();

			quest.StopQuest();
			int evaluatedCount = criteria.EvaluatedCount;
			gameEvents.Raise(GameEventType.OnResearchComplete);
			gameEvents.Raise(GameEventType.OnTick);

			Assert.AreEqual(0, gameEvents.CountOf(GameEventType.OnResearchComplete));
			Assert.AreEqual(0, gameEvents.CountOf(GameEventType.OnTick));
			Assert.AreEqual(evaluatedCount, criteria.EvaluatedCount);
		}

		[Test]
		public void Auto_complete_still_finishes_on_tick_and_stops_listening()
		{
			RuntimeQuest quest = CreateQuest(true);
			quest.StartQuest();
			Assert.AreEqual(RuntimeQuestState.InProgress, quest.State);

			criteria.IsMet = true;
			gameEvents.Raise(GameEventType.OnTick);

			Assert.AreEqual(RuntimeQuestState.Completed, quest.State);
			Assert.AreEqual(0, gameEvents.CountOf(GameEventType.OnTick));
		}
	}
}
