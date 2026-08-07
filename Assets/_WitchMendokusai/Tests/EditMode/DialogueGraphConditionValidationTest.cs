using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — **손으로 만든 자산의 빈 조건**을 검사가 잡는지.
	///
	/// ★ 왜 필요한가: 조건은 틀려도 안 터진다. 그냥 늘 거짓이 되고 그 가지는 한 번도 안 밟힌다.
	///   원고에서 온 조건은 읽는 쪽이 막지만, **자산을 손으로 만들면** 칸을 비워 둔 채로 저장할 수 있고
	///   그때는 아무도 안 본다. 화면에도 흔적이 없다.
	///
	/// 반대쪽도 잠근다 — 답이 적힌 멀쩡한 조건을 잡으면 안 된다.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueGraphConditionValidationTest
	{
		private static bool HasDeadConditionIssue(DialogueGraphValidationResult result)
		{
			for (int i = 0; i < result.Issues.Count; i++)
			{
				if (result.Issues[i].Kind == DialogueGraphIssueKind.ConditionCanNeverMatch)
				{
					return true;
				}
			}
			return false;
		}

		private static DialogueGraph GraphWithBranchCondition(Criteria condition)
		{
			DialogueGraph graph = ScriptableObject.CreateInstance<DialogueGraph>();
			DialogueStartNode start = new();
			DialogueBranchNode branch = new() { Condition = condition };
			DialogueSpeakNode speak = new();

			graph.AddNode(start);
			graph.AddNode(branch);
			graph.AddNode(speak);
			return graph;
		}

		[Test]
		public void ChosenConditionWithoutALabel_IsReported()
		{
			DialogueGraph graph = GraphWithBranchCondition(new DialogueChosenCriteria { DialogueId = 5200 });

			Assert.That(HasDeadConditionIssue(DialogueGraphValidator.Validate(graph)), Is.True,
				"답을 안 적으면 비교할 게 없어 절대 안 맞는다 — 그 가지는 죽은 가지다");
		}

		[Test]
		public void ChosenConditionWithALabel_IsFine()
		{
			DialogueGraph graph = GraphWithBranchCondition(
				new DialogueChosenCriteria { DialogueId = 5200, Label = "거절한다" });

			Assert.That(HasDeadConditionIssue(DialogueGraphValidator.Validate(graph)), Is.False);
		}

		[Test]
		public void OtherConditionKinds_AreNotTouched()
		{
			// 번호가 0 인 경우는 안 센다 — 0 이 뜻 있는 번호인지 여기서 단정할 수 없다.
			// 단정 못 하는 것을 잡으면 멀쩡한 자산을 잡는 검사가 된다.
			DialogueGraph graph = GraphWithBranchCondition(new DialogueSeenCriteria { DialogueId = 0 });

			Assert.That(HasDeadConditionIssue(DialogueGraphValidator.Validate(graph)), Is.False);
		}
	}
}
