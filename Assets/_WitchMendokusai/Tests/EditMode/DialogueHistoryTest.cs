using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — 「이 대화를 본 적 있나」 기록과 그 조건의 회귀 잠금.
	///
	/// 서사에서 제일 자주 쓰는 조건이 여기 달렸다 — 처음 만났을 때만 하는 인사, 이미 들은 이야기 건너뛰기.
	/// 잠그는 것: ① 시작과 끝까지가 다르게 세어진다 ② 저장/복구가 앞뒤 맞는다
	/// ③ 이력이 아직 없을 때 **터지지 않고** 「못 봤다」로 친다 ④ 분기에 실제로 꽂힌다.
	///
	/// ※ 이력은 static 다리로 찾아가므로, 각 시험이 자기 것을 등록하고 **반드시 해제**한다
	///   (안 하면 다음 시험이 앞 시험의 이력을 본다 — 순서에 따라 결과가 바뀌는 시험이 된다).
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueHistoryTest
	{
		private const int GREETING_ID = 4615;

		[Test]
		public void StartedAndCompleted_AreCountedSeparately()
		{
			DialogueHistory history = new();
			history.MarkStarted(GREETING_ID);

			Assert.That(history.HasSeen(GREETING_ID, DialogueSeenKind.Started), Is.True);
			Assert.That(history.HasSeen(GREETING_ID, DialogueSeenKind.Completed), Is.False,
				"도중에 끊은 대화를 「들었다」로 세면 다시 안 보여준다");

			history.MarkCompleted(GREETING_ID);
			Assert.That(history.HasSeen(GREETING_ID, DialogueSeenKind.Completed), Is.True);
		}

		[Test]
		public void UnknownDialogue_IsNotSeen()
		{
			DialogueHistory history = new();

			Assert.That(history.HasSeen(GREETING_ID, DialogueSeenKind.Started), Is.False);
		}

		[Test]
		public void SaveData_RoundTrips()
		{
			DialogueHistory source = new();
			source.MarkStarted(1);
			source.MarkCompleted(2);

			DialogueHistory restored = new();
			restored.FromSaveData(source.ToSaveData());

			Assert.That(restored.HasSeen(1, DialogueSeenKind.Started), Is.True);
			Assert.That(restored.HasSeen(1, DialogueSeenKind.Completed), Is.False);
			Assert.That(restored.HasSeen(2, DialogueSeenKind.Completed), Is.True);
		}

		[Test]
		public void FromSaveData_CompletedImpliesStarted()
		{
			DialogueHistorySaveData handWritten = new() { CompletedDialogueIds = new System.Collections.Generic.List<int> { 7 } };
			DialogueHistory history = new();

			history.FromSaveData(handWritten);

			Assert.That(history.HasSeen(7, DialogueSeenKind.Started), Is.True,
				"끝냈으면 시작한 것이다 — 저장본이 한쪽만 들고 있어도 앞뒤가 맞아야 한다");
		}

		[Test]
		public void FromSaveData_ClearsPreviousEntries()
		{
			DialogueHistory history = new();
			history.MarkCompleted(99);

			history.FromSaveData(new DialogueHistorySaveData());

			Assert.That(history.HasSeen(99, DialogueSeenKind.Started), Is.False,
				"다른 저장을 불러왔는데 앞 판의 기억이 남으면 안 된다");
		}

		[Test]
		public void Criteria_WithoutRegisteredHistory_TreatsAsUnseen()
		{
			DialogueHistoryBridge.Clear(DialogueHistoryBridge.Current);
			DialogueSeenCriteria criteria = new() { DialogueId = GREETING_ID, ExpectedSeen = false };

			Assert.That(criteria.Evaluate(), Is.True,
				"저장을 아직 안 불러온 첫 프레임에 터지면 대화가 통째로 죽는다 — 처음 보는 것으로 친다");
		}

		[Test]
		public void Criteria_ReadsRegisteredHistory()
		{
			DialogueHistory history = new();
			history.MarkCompleted(GREETING_ID);
			DialogueHistoryBridge.Register(history);
			try
			{
				DialogueSeenCriteria seen = new() { DialogueId = GREETING_ID, Kind = DialogueSeenKind.Completed };
				DialogueSeenCriteria firstMeeting = new() { DialogueId = GREETING_ID, ExpectedSeen = false };

				Assert.That(seen.Evaluate(), Is.True);
				Assert.That(firstMeeting.Evaluate(), Is.False, "이미 들었으니 「처음 만남」은 거짓");
			}
			finally
			{
				DialogueHistoryBridge.Clear(history);
			}
		}

		[Test]
		public void Bridge_CaptureWithoutHistory_ReturnsEmptyNotNull()
		{
			DialogueHistoryBridge.Clear(DialogueHistoryBridge.Current);

			DialogueHistorySaveData captured = DialogueHistoryBridge.CaptureSaveData();

			Assert.That(captured.StartedDialogueIds, Is.Not.Null,
				"저장 시점에 대화 시스템이 안 떠 있을 수 있다 — 그때 건너뛰면 다음 저장이 옛 기록을 덮어써 다 사라진다");
			Assert.That(captured.CompletedDialogueIds, Is.Not.Null);
		}

		[Test]
		public void Bridge_SaveAndRestore_RoundTripsThroughRegisteredHistory()
		{
			DialogueHistory saving = new();
			saving.MarkCompleted(GREETING_ID);
			DialogueHistoryBridge.Register(saving);
			DialogueHistorySaveData captured;
			try
			{
				captured = DialogueHistoryBridge.CaptureSaveData();
			}
			finally
			{
				DialogueHistoryBridge.Clear(saving);
			}

			DialogueHistory loading = new();
			DialogueHistoryBridge.Register(loading);
			try
			{
				DialogueHistoryBridge.RestoreSaveData(captured);

				Assert.That(loading.HasSeen(GREETING_ID, DialogueSeenKind.Completed), Is.True,
					"껐다 켜도 「봤다」가 남아야 조건부 대사가 뜻을 갖는다");
			}
			finally
			{
				DialogueHistoryBridge.Clear(loading);
			}
		}

		[Test]
		public void Bridge_RestoreWithoutHistory_DoesNotThrow()
		{
			DialogueHistoryBridge.Clear(DialogueHistoryBridge.Current);

			Assert.That(() => DialogueHistoryBridge.RestoreSaveData(new DialogueHistorySaveData()), Throws.Nothing,
				"불러오기가 대화 시스템보다 먼저 돌 수 있다");
		}

		[Test]
		public void Criteria_DrivesBranchNode()
		{
			DialogueHistory history = new();
			DialogueHistoryBridge.Register(history);
			try
			{
				DialogueGraph graph = ScriptableObject.CreateInstance<DialogueGraph>();
				DialogueStartNode start = new();
				DialogueBranchNode branch = new()
				{
					Condition = new DialogueSeenCriteria { DialogueId = GREETING_ID, ExpectedSeen = false },
				};
				DialogueLine firstMeetingLine = ScriptableObject.CreateInstance<DialogueLine>();
				DialogueLine againLine = ScriptableObject.CreateInstance<DialogueLine>();
				DialogueSpeakNode firstMeetingSpeak = new() { Line = firstMeetingLine };
				DialogueSpeakNode againSpeak = new() { Line = againLine };
				graph.AddNode(start);
				graph.AddNode(branch);
				graph.AddNode(firstMeetingSpeak);
				graph.AddNode(againSpeak);
				graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), branch.FindPort(DialogueBranchNode.PORT_IN));
				graph.Connect(branch.FindPort(DialogueBranchNode.PORT_TRUE), firstMeetingSpeak.FindPort(DialogueSpeakNode.PORT_IN));
				graph.Connect(branch.FindPort(DialogueBranchNode.PORT_FALSE), againSpeak.FindPort(DialogueSpeakNode.PORT_IN));

				Assert.That(new DialogueGraphTraversal(graph).Start().SpeakLine, Is.SameAs(firstMeetingLine),
					"아직 안 들었으면 첫 인사");

				history.MarkCompleted(GREETING_ID);

				Assert.That(new DialogueGraphTraversal(graph).Start().SpeakLine, Is.SameAs(againLine),
					"들은 뒤엔 다른 말을 한다 — 같은 그래프, 다른 결과");
			}
			finally
			{
				DialogueHistoryBridge.Clear(history);
			}
		}
	}
}
