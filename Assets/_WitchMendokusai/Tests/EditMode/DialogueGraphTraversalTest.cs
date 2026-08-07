using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 Phase 2 (tracer-bullet) — 노드 그래프 foundation 이 *Dialogue 도메인* 으로
	/// 일반화됨을 결정적으로 잠금. 지형(Pull)에 이어 대화(Flow traversal)가 같은 substrate
	/// (NodeGraph SO + NodeBase + 연결 + 검증기) 위에서 동작한다는 회귀 게이트.
	///
	/// 순수 <see cref="DialogueGraphTraversal"/> 만 검증 — Unity 런타임/PlayMode/GUI 무관
	/// (DialogueGraph/Node 는 ScriptableObject·POCO, traversal 은 결정적 로직).
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueGraphTraversalTest
	{
		private static DialogueGraph NewGraph()
		{
			return ScriptableObject.CreateInstance<DialogueGraph>();
		}

		private static DialogueLine NewLine()
		{
			return ScriptableObject.CreateInstance<DialogueLine>();
		}

		[Test]
		public void LinearChain_TraversesSpeakNodesInOrderThenEnd()
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = new();
			DialogueLine line1 = NewLine();
			DialogueLine line2 = NewLine();
			DialogueSpeakNode speak1 = new() { Line = line1 };
			DialogueSpeakNode speak2 = new() { Line = line2 };
			graph.AddNode(start);
			graph.AddNode(speak1);
			graph.AddNode(speak2);

			Assert.That(graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), speak1.FindPort(DialogueSpeakNode.PORT_IN)), Is.True);
			Assert.That(graph.Connect(speak1.FindPort(DialogueSpeakNode.PORT_NEXT), speak2.FindPort(DialogueSpeakNode.PORT_IN)), Is.True);

			DialogueGraphTraversal traversal = new(graph);

			DialogueStep step1 = traversal.Start();
			Assert.That(step1.Kind, Is.EqualTo(DialogueStepKind.Speak));
			Assert.That(step1.SpeakLine, Is.SameAs(line1));

			DialogueStep step2 = traversal.Next();
			Assert.That(step2.Kind, Is.EqualTo(DialogueStepKind.Speak));
			Assert.That(step2.SpeakLine, Is.SameAs(line2));

			DialogueStep step3 = traversal.Next();
			Assert.That(step3.Kind, Is.EqualTo(DialogueStepKind.End), "마지막 Speak 의 next 연결 없음 → 대화 종료");
		}

		[Test]
		public void NoStartNode_ImmediatelyEnds()
		{
			DialogueGraph graph = NewGraph();
			DialogueSpeakNode orphan = new() { Line = NewLine() };
			graph.AddNode(orphan);

			DialogueGraphTraversal traversal = new(graph);

			Assert.That(traversal.Start().Kind, Is.EqualTo(DialogueStepKind.End),
				"DialogueStartNode 없으면 진입점 없음 → 즉시 End");
		}

		// --- #6 Choice (TASK-WM-052 Phase 2 #6) ---

		[Test]
		public void Choice_SelectsBranchByIndex()
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = new();
			DialogueLine intro = NewLine();
			DialogueSpeakNode speakIntro = new() { Line = intro };
			DialogueChoiceNode choice = new() { Prompt = "pick", Options = new List<DialogueChoiceOption> { "A", "B" } };
			DialogueLine lineA = NewLine();
			DialogueLine lineB = NewLine();
			DialogueSpeakNode speakA = new() { Line = lineA };
			DialogueSpeakNode speakB = new() { Line = lineB };
			graph.AddNode(start);
			graph.AddNode(speakIntro);
			graph.AddNode(choice);
			graph.AddNode(speakA);
			graph.AddNode(speakB);

			Assert.That(graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), speakIntro.FindPort(DialogueSpeakNode.PORT_IN)), Is.True);
			Assert.That(graph.Connect(speakIntro.FindPort(DialogueSpeakNode.PORT_NEXT), choice.FindPort(DialogueChoiceNode.PORT_IN)), Is.True);
			Assert.That(graph.Connect(choice.FindPort(DialogueChoiceNode.ChoicePortId(0)), speakA.FindPort(DialogueSpeakNode.PORT_IN)), Is.True);
			Assert.That(graph.Connect(choice.FindPort(DialogueChoiceNode.ChoicePortId(1)), speakB.FindPort(DialogueSpeakNode.PORT_IN)), Is.True);

			DialogueGraphTraversal traversal = new(graph);
			Assert.That(traversal.Start().SpeakLine, Is.SameAs(intro));

			DialogueStep choiceStep = traversal.Next();
			Assert.That(choiceStep.Kind, Is.EqualTo(DialogueStepKind.Choice));
			Assert.That(choiceStep.Prompt, Is.EqualTo("pick"));
			Assert.That(choiceStep.Options, Is.EquivalentTo(new[] { "A", "B" }));

			Assert.That(traversal.SelectChoice(5), Is.False, "범위 밖 선택 = false");
			Assert.That(traversal.SelectChoice(1), Is.True);

			DialogueStep branch = traversal.Next();
			Assert.That(branch.Kind, Is.EqualTo(DialogueStepKind.Speak));
			Assert.That(branch.SpeakLine, Is.SameAs(lineB), "choice1 → speakB 분기");
			Assert.That(traversal.Next().Kind, Is.EqualTo(DialogueStepKind.End));
		}

		[Test]
		public void Choice_NoSelection_Ends()
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = new();
			DialogueChoiceNode choice = new() { Prompt = "p", Options = new List<DialogueChoiceOption> { "X" } };
			DialogueSpeakNode after = new() { Line = NewLine() };
			graph.AddNode(start);
			graph.AddNode(choice);
			graph.AddNode(after);

			Assert.That(graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), choice.FindPort(DialogueChoiceNode.PORT_IN)), Is.True);
			Assert.That(graph.Connect(choice.FindPort(DialogueChoiceNode.ChoicePortId(0)), after.FindPort(DialogueSpeakNode.PORT_IN)), Is.True);

			DialogueGraphTraversal traversal = new(graph);
			Assert.That(traversal.Start().Kind, Is.EqualTo(DialogueStepKind.Choice));
			Assert.That(traversal.Next().Kind, Is.EqualTo(DialogueStepKind.End),
				"SelectChoice 안 하면 진행 불가 → End");
		}

		// --- #8 Wait (TASK-WM-052 Phase 2 #8) ---

		[Test]
		public void Wait_Time_EmitsWaitStepThenContinuesOnNext()
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = new();
			DialogueWaitNode wait = new() { Kind = DialogueWaitKind.Time, Seconds = 1.5f };
			DialogueLine afterLine = NewLine();
			DialogueSpeakNode after = new() { Line = afterLine };
			graph.AddNode(start);
			graph.AddNode(wait);
			graph.AddNode(after);

			Assert.That(graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), wait.FindPort(DialogueWaitNode.PORT_IN)), Is.True);
			Assert.That(graph.Connect(wait.FindPort(DialogueWaitNode.PORT_NEXT), after.FindPort(DialogueSpeakNode.PORT_IN)), Is.True);

			DialogueGraphTraversal traversal = new(graph);

			DialogueStep waitStep = traversal.Start();
			Assert.That(waitStep.Kind, Is.EqualTo(DialogueStepKind.Wait));
			Assert.That(waitStep.WaitKind, Is.EqualTo(DialogueWaitKind.Time));
			Assert.That(waitStep.WaitSeconds, Is.EqualTo(1.5f));

			DialogueStep nextStep = traversal.Next();
			Assert.That(nextStep.Kind, Is.EqualTo(DialogueStepKind.Speak), "소비자가 시간 만족 후 Next() 호출 = 다음 노드 진행");
			Assert.That(nextStep.SpeakLine, Is.SameAs(afterLine));
		}

		[Test]
		public void Wait_Event_EmitsWaitStepWithEventId()
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = new();
			DialogueWaitNode wait = new() { Kind = DialogueWaitKind.Event, EventId = "boss-defeated" };
			graph.AddNode(start);
			graph.AddNode(wait);

			Assert.That(graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), wait.FindPort(DialogueWaitNode.PORT_IN)), Is.True);

			DialogueGraphTraversal traversal = new(graph);

			DialogueStep waitStep = traversal.Start();
			Assert.That(waitStep.Kind, Is.EqualTo(DialogueStepKind.Wait));
			Assert.That(waitStep.WaitKind, Is.EqualTo(DialogueWaitKind.Event));
			Assert.That(waitStep.WaitEventId, Is.EqualTo("boss-defeated"));

			Assert.That(traversal.Next().Kind, Is.EqualTo(DialogueStepKind.End), "Wait 의 next 미연결 → 대화 종료");
		}
	}
}
