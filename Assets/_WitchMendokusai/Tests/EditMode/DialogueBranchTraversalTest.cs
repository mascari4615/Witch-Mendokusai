using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 Phase 2 #7 — 조건 분기 노드의 회귀 잠금.
	///
	/// 분기는 *기존 <see cref="Criteria"/>* 를 조건 언어로 그대로 쓴다. 테스트는 게임 상태에
	/// 의존하지 않도록 참/거짓을 고정한 <see cref="FixedCriteria"/> 를 넣는다 — 실 구현체
	/// (<see cref="ItemCountCriteria"/> 등)와 같은 자리에 들어가는 다형 타입이라 seam 이 동일하다.
	///
	/// 잠그는 것: ① 참/거짓 포트 선택 ② 분기가 *스텝을 안 만든다*(연출 코드 불변의 근거)
	/// ③ 분기 연쇄 ④ 분기 고리 = 무한루프 대신 예외 ⑤ 조건 미할당 = 예외 ⑥ 출력 미연결 = 종료.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueBranchTraversalTest
	{
		/// <summary>참/거짓을 고정한 조건 — 게임 상태 없이 분기만 결정적으로 검증하기 위한 테스트 대역.</summary>
		private sealed class FixedCriteria : Criteria
		{
			private readonly bool result;

			public FixedCriteria(bool result)
			{
				this.result = result;
			}

			public override int GetCurValue() => result ? 1 : 0;
			public override int GetTargetValue() => 1;
			public override bool Evaluate() => result;
		}

		private static DialogueGraph NewGraph()
		{
			return ScriptableObject.CreateInstance<DialogueGraph>();
		}

		private static DialogueLine NewLine()
		{
			return ScriptableObject.CreateInstance<DialogueLine>();
		}

		private static void ConnectStart(DialogueGraph graph, DialogueStartNode start, DialogueBranchNode branch)
		{
			Assert.That(graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), branch.FindPort(DialogueBranchNode.PORT_IN)), Is.True);
		}

		[Test]
		public void ConditionTrue_FollowsTruePort()
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = new();
			DialogueBranchNode branch = new() { Condition = new FixedCriteria(true) };
			DialogueLine metLine = NewLine();
			DialogueLine unmetLine = NewLine();
			DialogueSpeakNode metSpeak = new() { Line = metLine };
			DialogueSpeakNode unmetSpeak = new() { Line = unmetLine };
			graph.AddNode(start);
			graph.AddNode(branch);
			graph.AddNode(metSpeak);
			graph.AddNode(unmetSpeak);

			ConnectStart(graph, start, branch);
			Assert.That(graph.Connect(branch.FindPort(DialogueBranchNode.PORT_TRUE), metSpeak.FindPort(DialogueSpeakNode.PORT_IN)), Is.True);
			Assert.That(graph.Connect(branch.FindPort(DialogueBranchNode.PORT_FALSE), unmetSpeak.FindPort(DialogueSpeakNode.PORT_IN)), Is.True);

			DialogueStep step = new DialogueGraphTraversal(graph).Start();

			Assert.That(step.Kind, Is.EqualTo(DialogueStepKind.Speak), "분기는 스텝을 안 만든다 — 첫 스텝이 곧 분기 *뒤* 의 대사");
			Assert.That(step.SpeakLine, Is.SameAs(metLine));
		}

		[Test]
		public void ConditionFalse_FollowsFalsePort()
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = new();
			DialogueBranchNode branch = new() { Condition = new FixedCriteria(false) };
			DialogueLine metLine = NewLine();
			DialogueLine unmetLine = NewLine();
			DialogueSpeakNode metSpeak = new() { Line = metLine };
			DialogueSpeakNode unmetSpeak = new() { Line = unmetLine };
			graph.AddNode(start);
			graph.AddNode(branch);
			graph.AddNode(metSpeak);
			graph.AddNode(unmetSpeak);

			ConnectStart(graph, start, branch);
			Assert.That(graph.Connect(branch.FindPort(DialogueBranchNode.PORT_TRUE), metSpeak.FindPort(DialogueSpeakNode.PORT_IN)), Is.True);
			Assert.That(graph.Connect(branch.FindPort(DialogueBranchNode.PORT_FALSE), unmetSpeak.FindPort(DialogueSpeakNode.PORT_IN)), Is.True);

			DialogueStep step = new DialogueGraphTraversal(graph).Start();

			Assert.That(step.Kind, Is.EqualTo(DialogueStepKind.Speak));
			Assert.That(step.SpeakLine, Is.SameAs(unmetLine));
		}

		[Test]
		public void MidDialogue_BranchEmitsNoExtraStep()
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = new();
			DialogueLine introLine = NewLine();
			DialogueLine afterLine = NewLine();
			DialogueSpeakNode intro = new() { Line = introLine };
			DialogueBranchNode branch = new() { Condition = new FixedCriteria(true) };
			DialogueSpeakNode after = new() { Line = afterLine };
			graph.AddNode(start);
			graph.AddNode(intro);
			graph.AddNode(branch);
			graph.AddNode(after);

			Assert.That(graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), intro.FindPort(DialogueSpeakNode.PORT_IN)), Is.True);
			Assert.That(graph.Connect(intro.FindPort(DialogueSpeakNode.PORT_NEXT), branch.FindPort(DialogueBranchNode.PORT_IN)), Is.True);
			Assert.That(graph.Connect(branch.FindPort(DialogueBranchNode.PORT_TRUE), after.FindPort(DialogueSpeakNode.PORT_IN)), Is.True);

			DialogueGraphTraversal traversal = new(graph);

			Assert.That(traversal.Start().SpeakLine, Is.SameAs(introLine));
			Assert.That(traversal.Next().SpeakLine, Is.SameAs(afterLine),
				"소비자(DialogueRunner)는 Next 한 번으로 분기 뒤 대사를 받는다 = 연출 코드 변경 0");
			Assert.That(traversal.Next().Kind, Is.EqualTo(DialogueStepKind.End));
		}

		[Test]
		public void ChainedBranches_ResolvedWithinOneStep()
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = new();
			DialogueBranchNode first = new() { Condition = new FixedCriteria(true) };
			DialogueBranchNode second = new() { Condition = new FixedCriteria(false) };
			DialogueLine targetLine = NewLine();
			DialogueSpeakNode target = new() { Line = targetLine };
			graph.AddNode(start);
			graph.AddNode(first);
			graph.AddNode(second);
			graph.AddNode(target);

			ConnectStart(graph, start, first);
			Assert.That(graph.Connect(first.FindPort(DialogueBranchNode.PORT_TRUE), second.FindPort(DialogueBranchNode.PORT_IN)), Is.True);
			Assert.That(graph.Connect(second.FindPort(DialogueBranchNode.PORT_FALSE), target.FindPort(DialogueSpeakNode.PORT_IN)), Is.True);

			DialogueStep step = new DialogueGraphTraversal(graph).Start();

			Assert.That(step.Kind, Is.EqualTo(DialogueStepKind.Speak));
			Assert.That(step.SpeakLine, Is.SameAs(targetLine), "분기가 연달아 있어도 스텝 하나 안에서 다 해소된다");
		}

		[Test]
		public void UnconnectedBranchOutput_EndsDialogue()
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = new();
			DialogueBranchNode branch = new() { Condition = new FixedCriteria(false) };
			DialogueSpeakNode metSpeak = new() { Line = NewLine() };
			graph.AddNode(start);
			graph.AddNode(branch);
			graph.AddNode(metSpeak);

			ConnectStart(graph, start, branch);
			Assert.That(graph.Connect(branch.FindPort(DialogueBranchNode.PORT_TRUE), metSpeak.FindPort(DialogueSpeakNode.PORT_IN)), Is.True);

			DialogueStep step = new DialogueGraphTraversal(graph).Start();

			Assert.That(step.Kind, Is.EqualTo(DialogueStepKind.End),
				"거짓 쪽이 안 이어져 있으면 그 분기는 대화 종료 (Choice 미연결 선례와 동형)");
		}

		[Test]
		public void MissingCondition_Throws()
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = new();
			DialogueBranchNode branch = new();
			graph.AddNode(start);
			graph.AddNode(branch);

			ConnectStart(graph, start, branch);

			DialogueGraphTraversal traversal = new(graph);

			Assert.That(() => traversal.Start(), Throws.TypeOf<InvalidOperationException>(),
				"조건 미할당은 데이터 오류 — 기본값으로 덮으면 왜 이 대사가 나왔는지 못 되짚는다 (FastFail)");
		}

		[Test]
		public void BranchLoop_ThrowsInsteadOfHanging()
		{
			// ★ 고리는 Connect 로는 못 만든다 — 입력 포트 하나에 연결 하나(단일 input 의미)라
			//   되돌아오는 연결이 진입 연결을 *밀어낸다*. 그래서 고리는 손으로 편집된 .asset /
			//   옛 데이터에서만 나온다(NodeGraphValidator 가 그 케이스를 상정한 이유와 같다).
			//   이 테스트는 그 상태를 그대로 재현한다 — Connections override = 하드 편집 대역.
			HandEditedGraph graph = ScriptableObject.CreateInstance<HandEditedGraph>();
			DialogueStartNode start = new();
			DialogueBranchNode first = new() { Condition = new FixedCriteria(true) };
			DialogueBranchNode second = new() { Condition = new FixedCriteria(true) };
			graph.AddNode(start);
			graph.AddNode(first);
			graph.AddNode(second);

			graph.AddHandConnection(start.Id, DialogueStartNode.PORT_NEXT, first.Id, DialogueBranchNode.PORT_IN);
			graph.AddHandConnection(first.Id, DialogueBranchNode.PORT_TRUE, second.Id, DialogueBranchNode.PORT_IN);
			graph.AddHandConnection(second.Id, DialogueBranchNode.PORT_TRUE, first.Id, DialogueBranchNode.PORT_IN);

			DialogueGraphTraversal traversal = new(graph);

			Assert.That(() => traversal.Start(), Throws.TypeOf<InvalidOperationException>(),
				"분기 고리는 프레임을 멈춘다 — 노드 수만큼만 건너뛰고 터진다");
		}

		[Test]
		public void Connect_AllowsFlowMerge_AndThusRealCycles()
		{
			// 2026-08-08 정정: 예전엔 흐름 입력도 「하나만」이라 되돌아오는 연결이 진입 연결을 밀어냈고,
			// 그래서 닿는 고리를 못 만들었다. 이제 흐름은 여럿이 모일 수 있다(대화의 합류가 기본형이라서).
			// = 정상 편집 경로로도 고리가 만들어질 수 있다 → 위의 고리 방어가 손편집 전용이 아니게 됐다.
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = new();
			DialogueBranchNode first = new() { Condition = new FixedCriteria(true) };
			DialogueBranchNode second = new() { Condition = new FixedCriteria(true) };
			graph.AddNode(start);
			graph.AddNode(first);
			graph.AddNode(second);

			ConnectStart(graph, start, first);
			Assert.That(graph.Connect(first.FindPort(DialogueBranchNode.PORT_TRUE), second.FindPort(DialogueBranchNode.PORT_IN)), Is.True);
			Assert.That(graph.Connect(second.FindPort(DialogueBranchNode.PORT_TRUE), first.FindPort(DialogueBranchNode.PORT_IN)), Is.True);

			Assert.That(graph.Connections.Count, Is.EqualTo(3),
				"되돌아오는 연결이 진입 연결을 밀어내지 않는다 — 흐름 입력은 여럿이 정상");

			DialogueGraphTraversal traversal = new(graph);
			Assert.That(() => traversal.Start(), Throws.TypeOf<InvalidOperationException>(),
				"그래서 고리 방어가 실제로 필요해졌다");
		}

		[Test]
		public void FlowMerge_TwoBranchesIntoOneLine()
		{
			// 대화에서 제일 흔한 모양 — 갈라졌다가 같은 자리로 모인다.
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = new();
			DialogueBranchNode branch = new() { Condition = new FixedCriteria(true) };
			DialogueLine mergedLine = NewLine();
			DialogueSpeakNode merged = new() { Line = mergedLine };
			graph.AddNode(start);
			graph.AddNode(branch);
			graph.AddNode(merged);

			ConnectStart(graph, start, branch);
			Assert.That(graph.Connect(branch.FindPort(DialogueBranchNode.PORT_TRUE), merged.FindPort(DialogueSpeakNode.PORT_IN)), Is.True);
			Assert.That(graph.Connect(branch.FindPort(DialogueBranchNode.PORT_FALSE), merged.FindPort(DialogueSpeakNode.PORT_IN)), Is.True);

			Assert.That(new DialogueGraphTraversal(graph).Start().SpeakLine, Is.SameAs(mergedLine),
				"참이든 거짓이든 같은 자리로 모인다");
		}

		/// <summary>
		/// 손편집된 자산 대역 — 단일 input 규칙을 거치지 않는 연결 목록을 직접 들고 있다.
		/// `NodeGraph.Connections` 가 virtual 인 이유(도메인 파생 연결)를 테스트에서 그대로 쓴다.
		/// </summary>
		private sealed class HandEditedGraph : DialogueGraph
		{
			private readonly List<NodeConnection> handConnections = new();

			public override IReadOnlyList<NodeConnection> Connections => handConnections;

			public void AddHandConnection(string sourceNodeId, string sourcePortId, string targetNodeId, string targetPortId)
			{
				handConnections.Add(new NodeConnection(sourceNodeId, sourcePortId, targetNodeId, targetPortId));
			}
		}
	}
}
