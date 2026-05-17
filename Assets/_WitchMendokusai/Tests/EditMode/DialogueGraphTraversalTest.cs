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
	}
}
