using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — 조건이 걸린 선택지의 회귀 잠금.
	///
	/// ★ 여기서 제일 위험한 것: **화면에 보이는 순번과 노드에 적힌 순번이 어긋난다.**
	/// 조건 때문에 위 칸이 빠지면 「두 번째로 보이는 것」이 「적힌 두 번째」가 아니다.
	/// 그런데 연결(포트)은 *적힌 순번* 으로 고정이다. 이 표가 틀리면 플레이어가 A 를 눌렀는데
	/// B 가지로 가는 — 눈으로는 절대 못 잡는 종류의 버그가 된다. 그래서 잠근다.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueChoiceConditionTest
	{
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

		private static DialogueGraph NewGraph() => ScriptableObject.CreateInstance<DialogueGraph>();
		private static DialogueLine NewLine() => ScriptableObject.CreateInstance<DialogueLine>();

		/// <summary>선택지 3칸 그래프 — 각 칸의 조건과 도착 대사를 지정한다.</summary>
		private static DialogueGraphTraversal BuildThreeOptionGraph(
			IReadOnlyList<Criteria> conditions, out IReadOnlyList<DialogueLine> destinations)
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = new();
			DialogueChoiceNode choice = new() { Prompt = "무엇을 할까" };
			List<DialogueLine> lines = new();
			graph.AddNode(start);
			graph.AddNode(choice);

			for (int i = 0; i < conditions.Count; i++)
			{
				choice.Options.Add(new DialogueChoiceOption($"option{i}", conditions[i]));
			}

			graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), choice.FindPort(DialogueChoiceNode.PORT_IN));

			for (int i = 0; i < conditions.Count; i++)
			{
				DialogueLine line = NewLine();
				lines.Add(line);
				DialogueSpeakNode speak = new() { Line = line };
				graph.AddNode(speak);
				graph.Connect(choice.FindPort(DialogueChoiceNode.ChoicePortId(i)), speak.FindPort(DialogueSpeakNode.PORT_IN));
			}

			destinations = lines;
			return new DialogueGraphTraversal(graph);
		}

		[Test]
		public void UnmetOption_IsHiddenFromTheList()
		{
			DialogueGraphTraversal traversal = BuildThreeOptionGraph(
				new Criteria[] { null, new FixedCriteria(false), null }, out IReadOnlyList<DialogueLine> _);

			DialogueStep step = traversal.Start();

			Assert.That(step.Kind, Is.EqualTo(DialogueStepKind.Choice));
			Assert.That(step.Options.Count, Is.EqualTo(2), "조건이 안 맞는 칸은 목록에서 아예 빠진다");
			Assert.That(step.Options[0], Is.EqualTo("option0"));
			Assert.That(step.Options[1], Is.EqualTo("option2"));
		}

		[Test]
		public void VisibleIndex_MapsToAuthoredPort_NotListPosition()
		{
			// 0번이 잠긴다 → 보이는 것은 [1번, 2번]. 보이는 0번을 고르면 *적힌 1번* 가지로 가야 한다.
			DialogueGraphTraversal traversal = BuildThreeOptionGraph(
				new Criteria[] { new FixedCriteria(false), null, null }, out IReadOnlyList<DialogueLine> destinations);

			traversal.Start();
			Assert.That(traversal.SelectChoice(0), Is.True);
			DialogueStep afterPick = traversal.Next();

			Assert.That(afterPick.SpeakLine, Is.SameAs(destinations[1]),
				"보이는 첫 칸이 적힌 첫 칸이 아니다 — 여기서 밀리면 누른 것과 다른 가지로 간다");
		}

		[Test]
		public void SelectChoice_RangeIsVisibleCount_NotAuthoredCount()
		{
			DialogueGraphTraversal traversal = BuildThreeOptionGraph(
				new Criteria[] { null, new FixedCriteria(false), new FixedCriteria(false) }, out IReadOnlyList<DialogueLine> _);

			traversal.Start();

			Assert.That(traversal.SelectChoice(0), Is.True);
			Assert.That(traversal.SelectChoice(1), Is.False, "적힌 칸은 셋이어도 보이는 건 하나뿐이다");
		}

		[Test]
		public void AllOptionsUnmet_EndsDialogue()
		{
			DialogueGraphTraversal traversal = BuildThreeOptionGraph(
				new Criteria[] { new FixedCriteria(false), new FixedCriteria(false) }, out IReadOnlyList<DialogueLine> _);

			DialogueStep step = traversal.Start();

			Assert.That(step.Kind, Is.EqualTo(DialogueStepKind.End),
				"고를 게 하나도 없으면 빈 목록을 띄우고 멈추는 대신 대화를 끝낸다");
		}

		[Test]
		public void NoCondition_BehavesExactlyAsBefore()
		{
			DialogueGraphTraversal traversal = BuildThreeOptionGraph(
				new Criteria[] { null, null }, out IReadOnlyList<DialogueLine> destinations);

			DialogueStep step = traversal.Start();
			Assert.That(step.Options.Count, Is.EqualTo(2));

			Assert.That(traversal.SelectChoice(1), Is.True);
			Assert.That(traversal.Next().SpeakLine, Is.SameAs(destinations[1]));
		}

		[Test]
		public void EveryOptionConditional_IsWarned()
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = new();
			DialogueChoiceNode choice = new()
			{
				Prompt = "?",
				Options = new List<DialogueChoiceOption>
				{
					new("locked-a", new FixedCriteria(true)),
					new("locked-b", new FixedCriteria(true)),
				},
			};
			DialogueSpeakNode firstDestination = new() { Line = NewLine() };
			DialogueSpeakNode secondDestination = new() { Line = NewLine() };
			graph.AddNode(start);
			graph.AddNode(choice);
			graph.AddNode(firstDestination);
			graph.AddNode(secondDestination);
			graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), choice.FindPort(DialogueChoiceNode.PORT_IN));
			graph.Connect(choice.FindPort(DialogueChoiceNode.ChoicePortId(0)), firstDestination.FindPort(DialogueSpeakNode.PORT_IN));
			graph.Connect(choice.FindPort(DialogueChoiceNode.ChoicePortId(1)), secondDestination.FindPort(DialogueSpeakNode.PORT_IN));

			DialogueGraphValidationResult result = DialogueGraphValidator.Validate(graph);

			Assert.That(result.CountOf(DialogueGraphIssueKind.ChoiceMayHaveNoAvailableOption), Is.EqualTo(1),
				"전부 조건이면 아무것도 안 맞는 순간 대화가 조용히 끝난다 — 조건 없는 칸 하나를 권한다");
			Assert.That(result.IsValid, Is.True, "설계 의도일 수 있으니 경고까지");
		}

		[Test]
		public void OneUnconditionalOption_IsNotWarned()
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = new();
			DialogueChoiceNode choice = new()
			{
				Prompt = "?",
				Options = new List<DialogueChoiceOption>
				{
					new("locked", new FixedCriteria(true)),
					new("always"),
				},
			};
			DialogueSpeakNode firstDestination = new() { Line = NewLine() };
			DialogueSpeakNode secondDestination = new() { Line = NewLine() };
			graph.AddNode(start);
			graph.AddNode(choice);
			graph.AddNode(firstDestination);
			graph.AddNode(secondDestination);
			graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), choice.FindPort(DialogueChoiceNode.PORT_IN));
			graph.Connect(choice.FindPort(DialogueChoiceNode.ChoicePortId(0)), firstDestination.FindPort(DialogueSpeakNode.PORT_IN));
			graph.Connect(choice.FindPort(DialogueChoiceNode.ChoicePortId(1)), secondDestination.FindPort(DialogueSpeakNode.PORT_IN));

			Assert.That(DialogueGraphValidator.Validate(graph).CountOf(DialogueGraphIssueKind.ChoiceMayHaveNoAvailableOption), Is.Zero);
		}
	}
}
