using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — 대화 그래프 정적 검사의 회귀 잠금.
	///
	/// 여기서 잡는 결함들은 **예외를 안 낸다** — 빈 말풍선이 뜨거나, 고르자마자 대화가 끝나거나,
	/// 영원히 안 풀리는 대기에 걸린다. 화면으로 잡으려면 모든 가지를 눌러 봐야 하므로
	/// 안 눌러 본 가지는 영영 안 잡힌다. 그래서 이 검사가 정본 방어선이다.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueGraphValidatorTest
	{
		private sealed class FixedCriteria : Criteria
		{
			public override int GetCurValue() => 1;
			public override int GetTargetValue() => 1;
			public override bool Evaluate() => true;
		}

		private static DialogueGraph NewGraph() => ScriptableObject.CreateInstance<DialogueGraph>();
		private static DialogueLine NewLine() => ScriptableObject.CreateInstance<DialogueLine>();

		/// <summary>start + 노드 하나를 이어붙인 최소 그래프 — 각 테스트는 그 노드만 바꿔 끼운다.</summary>
		private static DialogueGraph GraphWith(NodeGraph.NodeBase node, string inputPortId)
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = new();
			graph.AddNode(start);
			graph.AddNode(node);
			graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), node.FindPort(inputPortId));
			return graph;
		}

		[Test]
		public void CleanGraph_HasNoIssues()
		{
			DialogueGraph graph = GraphWith(new DialogueSpeakNode { Line = NewLine() }, DialogueSpeakNode.PORT_IN);

			DialogueGraphValidationResult result = DialogueGraphValidator.Validate(graph);

			Assert.That(result.IsValid, Is.True);
			Assert.That(result.Issues.Count, Is.Zero, "멀쩡한 그래프에서 소음이 나면 아무도 이 검사를 안 본다");
		}

		[Test]
		public void NoStartNode_IsErrorAndSkipsReachability()
		{
			DialogueGraph graph = NewGraph();
			graph.AddNode(new DialogueSpeakNode { Line = NewLine() });

			DialogueGraphValidationResult result = DialogueGraphValidator.Validate(graph);

			Assert.That(result.CountOf(DialogueGraphIssueKind.NoStartNode), Is.EqualTo(1));
			Assert.That(result.HasErrors, Is.True);
			Assert.That(result.CountOf(DialogueGraphIssueKind.UnreachableNode), Is.Zero,
				"진입점이 없으면 전부 안 닿는다 — 같은 사실을 두 번 말하지 않는다");
		}

		[Test]
		public void MultipleStartNodes_IsWarning()
		{
			DialogueGraph graph = NewGraph();
			graph.AddNode(new DialogueStartNode());
			graph.AddNode(new DialogueStartNode());

			DialogueGraphValidationResult result = DialogueGraphValidator.Validate(graph);

			Assert.That(result.CountOf(DialogueGraphIssueKind.MultipleStartNodes), Is.EqualTo(1));
			Assert.That(result.IsValid, Is.True, "고를 수는 있으니 오류는 아니다 — 다만 목록 순서가 정하는 건 의도가 아니다");
		}

		[Test]
		public void SpeakWithoutLine_IsError()
		{
			DialogueGraph graph = GraphWith(new DialogueSpeakNode(), DialogueSpeakNode.PORT_IN);

			Assert.That(DialogueGraphValidator.Validate(graph).CountOf(DialogueGraphIssueKind.SpeakWithoutLine), Is.EqualTo(1));
		}

		[Test]
		public void BranchWithoutCondition_IsError()
		{
			DialogueGraph missing = GraphWith(new DialogueBranchNode(), DialogueBranchNode.PORT_IN);
			Assert.That(DialogueGraphValidator.Validate(missing).CountOf(DialogueGraphIssueKind.BranchWithoutCondition), Is.EqualTo(1),
				"재생 중 터지는 것을 재생 전에 알려준다");

			DialogueGraph assigned = GraphWith(new DialogueBranchNode { Condition = new FixedCriteria() }, DialogueBranchNode.PORT_IN);
			Assert.That(DialogueGraphValidator.Validate(assigned).CountOf(DialogueGraphIssueKind.BranchWithoutCondition), Is.Zero);
		}

		[Test]
		public void WaitEventWithoutId_IsError_ButTimeWaitIsNot()
		{
			DialogueGraph eventWait = GraphWith(
				new DialogueWaitNode { Kind = DialogueWaitKind.Event, EventId = "   " }, DialogueWaitNode.PORT_IN);
			Assert.That(DialogueGraphValidator.Validate(eventWait).CountOf(DialogueGraphIssueKind.WaitEventWithoutId), Is.EqualTo(1),
				"기다릴 사건 이름이 없으면 그 대기는 영원히 안 풀린다");

			DialogueGraph timeWait = GraphWith(
				new DialogueWaitNode { Kind = DialogueWaitKind.Time, Seconds = 1f }, DialogueWaitNode.PORT_IN);
			Assert.That(DialogueGraphValidator.Validate(timeWait).CountOf(DialogueGraphIssueKind.WaitEventWithoutId), Is.Zero);
		}

		[Test]
		public void ChoiceWithoutOptions_IsError()
		{
			DialogueGraph graph = GraphWith(new DialogueChoiceNode { Prompt = "?" }, DialogueChoiceNode.PORT_IN);

			Assert.That(DialogueGraphValidator.Validate(graph).CountOf(DialogueGraphIssueKind.ChoiceWithoutOptions), Is.EqualTo(1));
		}

		[Test]
		public void ChoiceOptionWithoutConnection_IsWarningPerOption()
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = new();
			DialogueChoiceNode choice = new() { Prompt = "?", Options = new List<DialogueChoiceOption> { "A", "B" } };
			DialogueSpeakNode firstOption = new() { Line = NewLine() };
			graph.AddNode(start);
			graph.AddNode(choice);
			graph.AddNode(firstOption);
			graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), choice.FindPort(DialogueChoiceNode.PORT_IN));
			graph.Connect(choice.FindPort(DialogueChoiceNode.ChoicePortId(0)), firstOption.FindPort(DialogueSpeakNode.PORT_IN));

			DialogueGraphValidationResult result = DialogueGraphValidator.Validate(graph);

			Assert.That(result.CountOf(DialogueGraphIssueKind.ChoiceOptionNotConnected), Is.EqualTo(1),
				"이어진 0번은 안 걸리고 안 이어진 1번만 걸린다");
			Assert.That(result.IsValid, Is.True, "고르면 끝나는 것도 대화 설계일 수 있다 — 경고까지");
		}

		[Test]
		public void LoopWithNoWayOut_IsError()
		{
			// 흐름이 여럿 모일 수 있게 된 뒤로 정상 편집으로도 고리를 만들 수 있다. 고리 자체는 정상이지만
			// **나가는 길이 하나도 없으면** 플레이어가 영원히 같은 대사를 돈다(그 사이 뒤 대화도 다 막힌다).
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = new();
			DialogueSpeakNode first = new() { Line = NewLine() };
			DialogueSpeakNode second = new() { Line = NewLine() };
			graph.AddNode(start);
			graph.AddNode(first);
			graph.AddNode(second);
			graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), first.FindPort(DialogueSpeakNode.PORT_IN));
			graph.Connect(first.FindPort(DialogueSpeakNode.PORT_NEXT), second.FindPort(DialogueSpeakNode.PORT_IN));
			graph.Connect(second.FindPort(DialogueSpeakNode.PORT_NEXT), first.FindPort(DialogueSpeakNode.PORT_IN));

			DialogueGraphValidationResult result = DialogueGraphValidator.Validate(graph);

			Assert.That(result.CountOf(DialogueGraphIssueKind.CannotReachEnd), Is.GreaterThan(0));
			Assert.That(result.HasErrors, Is.True);
		}

		[Test]
		public void LoopWithAnExit_IsFine()
		{
			// 허브로 돌아오는 대화는 정상이다 — 나갈 길이 하나라도 있으면 걸리면 안 된다.
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = new();
			DialogueChoiceNode hub = new()
			{
				Prompt = "무엇을 물을까",
				Options = new List<DialogueChoiceOption> { "다시 묻는다", "그만한다" },
			};
			DialogueSpeakNode answer = new() { Line = NewLine() };
			DialogueSpeakNode farewell = new() { Line = NewLine() };
			graph.AddNode(start);
			graph.AddNode(hub);
			graph.AddNode(answer);
			graph.AddNode(farewell);
			graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), hub.FindPort(DialogueChoiceNode.PORT_IN));
			graph.Connect(hub.FindPort(DialogueChoiceNode.ChoicePortId(0)), answer.FindPort(DialogueSpeakNode.PORT_IN));
			graph.Connect(answer.FindPort(DialogueSpeakNode.PORT_NEXT), hub.FindPort(DialogueChoiceNode.PORT_IN));
			graph.Connect(hub.FindPort(DialogueChoiceNode.ChoicePortId(1)), farewell.FindPort(DialogueSpeakNode.PORT_IN));

			DialogueGraphValidationResult result = DialogueGraphValidator.Validate(graph);

			Assert.That(result.CountOf(DialogueGraphIssueKind.CannotReachEnd), Is.Zero,
				"돌아오는 대화는 정상이다 — 나갈 길이 있으면 걸면 안 된다");
		}

		[Test]
		public void UnreachableNode_IsWarning()
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = new();
			DialogueSpeakNode connected = new() { Line = NewLine() };
			DialogueSpeakNode orphan = new() { Line = NewLine() };
			graph.AddNode(start);
			graph.AddNode(connected);
			graph.AddNode(orphan);
			graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), connected.FindPort(DialogueSpeakNode.PORT_IN));

			DialogueGraphValidationResult result = DialogueGraphValidator.Validate(graph);

			Assert.That(result.CountOf(DialogueGraphIssueKind.UnreachableNode), Is.EqualTo(1));
			Assert.That(result.Issues.Count, Is.EqualTo(1), "닿는 노드까지 같이 걸리면 안 된다");
		}
	}
}
