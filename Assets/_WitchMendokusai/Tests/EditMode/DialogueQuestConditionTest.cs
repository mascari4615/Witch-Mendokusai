using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — 「그 의뢰 끝냈으면」 조건의 회귀 잠금.
	///
	/// 잠그는 것: ① 원고 글자가 진짜 조건이 된다 ② 완료/미완/열림이 갈린다
	/// ③ **창구가 없거나 모르는 퀘스트면 「잠김」으로 친다**(안 준 보상을 받은 척하는 대사 방지).
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueQuestConditionTest
	{
		private const int ERRAND = 5000;

		private sealed class FakeQuestStates : IDialogueQuestStateSource
		{
			private readonly Dictionary<int, QuestState> states = new();

			public FakeQuestStates Set(int questId, QuestState state)
			{
				states[questId] = state;
				return this;
			}

			public bool TryGetQuestState(int questId, out QuestState state) => states.TryGetValue(questId, out state);
		}

		[Test]
		public void WrittenQuestCondition_BecomesARealCondition()
		{
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join("\n",
				"## 마을",
				"> ?퀘스트완료 5000 -> 고맙다",
				"> 링: \"아직이야?\"",
				"## 고맙다",
				"> 링: \"고마워!\""));

			Assert.That(parsed.HasIssues, Is.False);
			Assert.That(parsed.Sections[0].Entries[0].Condition.Kind, Is.EqualTo(DialogueScriptConditionKind.QuestState));
			Assert.That(parsed.Sections[0].Entries[0].Condition.QuestState, Is.EqualTo(QuestState.Completed));
		}

		[Test]
		public void CompletedAndNotDone_AreOpposite()
		{
			DialogueQuestCriteria done = new() { QuestId = ERRAND, ExpectedState = QuestState.Completed };
			DialogueQuestCriteria notDone = new() { QuestId = ERRAND, ExpectedState = QuestState.Completed, ExpectedMatch = false };

			DialogueQuestBridge.Register(new FakeQuestStates().Set(ERRAND, QuestState.Completed));
			try
			{
				Assert.That(done.Evaluate(), Is.True);
				Assert.That(notDone.Evaluate(), Is.False);
			}
			finally
			{
				DialogueQuestBridge.Clear(DialogueQuestBridge.Current);
			}
		}

		[Test]
		public void UnlockedIsNotCompleted()
		{
			DialogueQuestCriteria done = new() { QuestId = ERRAND, ExpectedState = QuestState.Completed };
			DialogueQuestCriteria open = new() { QuestId = ERRAND, ExpectedState = QuestState.Unlocked };

			DialogueQuestBridge.Register(new FakeQuestStates().Set(ERRAND, QuestState.Unlocked));
			try
			{
				Assert.That(done.Evaluate(), Is.False, "받기만 한 의뢰를 끝낸 것으로 보면 안 된다");
				Assert.That(open.Evaluate(), Is.True);
			}
			finally
			{
				DialogueQuestBridge.Clear(DialogueQuestBridge.Current);
			}
		}

		[Test]
		public void UnknownQuest_CountsAsLocked()
		{
			DialogueQuestBridge.Register(new FakeQuestStates());
			try
			{
				DialogueQuestCriteria done = new() { QuestId = 99999, ExpectedState = QuestState.Completed };
				Assert.That(done.Evaluate(), Is.False, "모르는 번호로 물었다고 대화가 죽으면 안 된다");
			}
			finally
			{
				DialogueQuestBridge.Clear(DialogueQuestBridge.Current);
			}
		}

		[Test]
		public void WithoutTheBridge_CountsAsLocked()
		{
			DialogueQuestBridge.Clear(DialogueQuestBridge.Current);
			DialogueQuestCriteria done = new() { QuestId = ERRAND, ExpectedState = QuestState.Completed };

			Assert.That(done.Evaluate(), Is.False,
				"「끝냈다」로 잘못 보면 안 준 보상을 받은 것처럼 구는 대사가 나온다");
		}
	}
}
