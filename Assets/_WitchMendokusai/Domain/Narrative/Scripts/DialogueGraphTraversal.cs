using System.Collections.Generic;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	public enum DialogueStepKind
	{
		Speak,
		Choice,
		End,
	}

	/// <summary>
	/// traversal 이 방출하는 한 스텝.
	/// Speak → <see cref="SpeakLine"/> 유효. Choice → <see cref="Prompt"/> + <see cref="Options"/>
	/// 유효(소비자는 <see cref="DialogueGraphTraversal.SelectChoice"/> 로 분기 선택). End → 전부 null.
	/// </summary>
	public readonly struct DialogueStep
	{
		public DialogueStepKind Kind { get; }
		public DialogueLine SpeakLine { get; }
		public string Prompt { get; }
		public IReadOnlyList<string> Options { get; }

		private DialogueStep(DialogueStepKind kind, DialogueLine speakLine, string prompt, IReadOnlyList<string> options)
		{
			Kind = kind;
			SpeakLine = speakLine;
			Prompt = prompt;
			Options = options;
		}

		public static DialogueStep Speak(DialogueLine line) => new(DialogueStepKind.Speak, line, null, null);
		public static DialogueStep Choice(string prompt, IReadOnlyList<string> options) =>
			new(DialogueStepKind.Choice, null, prompt, options);
		public static readonly DialogueStep End = new(DialogueStepKind.End, null, null, null);
	}

	/// <summary>
	/// 대화 그래프의 *순수* 플로우 traversal — Unity I/O(코루틴/버블/사운드) 0, 결정적.
	/// 그래서 EditMode 에서 그대로 회귀 잠금 가능(황금의 정신 「피드백 루프 먼저」 = testable seam).
	/// MonoBehaviour 러너(DialogueRunner Phase 2 통합 — 다음 단계)는 이 스텝 시퀀스를 소비해
	/// 버블/typewriter/sfx 연출만 담당(traversal 로직과 분리).
	///
	/// 현 단계: <see cref="DialogueStartNode"/> → (<see cref="DialogueSpeakNode"/> |
	/// <see cref="DialogueChoiceNode"/>)* → 종료. #6 Choice 추가 시 코어 "현재 노드의 출력
	/// 포트 → 연결 → 타깃"(<see cref="FollowFlow"/>) 는 *불변* — 따라갈 출력 포트 id 만
	/// 노드 타입별로 분기(<see cref="OutputPortToFollow"/>). Wait/Branch 도 동일 패턴 확장.
	/// </summary>
	public sealed class DialogueGraphTraversal
	{
		private readonly DialogueGraph graph;
		private NodeBase currentNode;
		private int pendingChoice = -1;

		public DialogueGraphTraversal(DialogueGraph graph)
		{
			this.graph = graph;
		}

		/// <summary>진입점 노드에서 첫 플로우 엣지를 따라 첫 스텝 반환. Start 노드/연결 없으면 End.</summary>
		public DialogueStep Start()
		{
			if (graph == null)
			{
				return DialogueStep.End;
			}

			DialogueStartNode startNode = FindStartNode();
			if (startNode == null)
			{
				return DialogueStep.End;
			}

			currentNode = FollowFlow(startNode, DialogueStartNode.PORT_NEXT);
			return StepForCurrent();
		}

		/// <summary>
		/// 현재 노드의 진행 플로우 엣지를 따라 다음 스텝. Speak→`next` / Choice→선택된
		/// `choice{i}`(미선택이면 End). 연결 없거나 끝이면 End.
		/// </summary>
		public DialogueStep Next()
		{
			if (currentNode == null)
			{
				return DialogueStep.End;
			}

			string outputPortId = OutputPortToFollow(currentNode);
			if (outputPortId == null)
			{
				return DialogueStep.End;
			}

			currentNode = FollowFlow(currentNode, outputPortId);
			return StepForCurrent();
		}

		/// <summary>
		/// Choice 스텝에서 분기 선택 — i 번째 옵션 포트(`choice{i}`)를 다음 <see cref="Next"/> 가 따라감.
		/// 현재 노드가 Choice 아니거나 index 범위 밖이면 false(상태 불변).
		/// </summary>
		public bool SelectChoice(int index)
		{
			if (currentNode is DialogueChoiceNode choiceNode == false)
			{
				return false;
			}
			if (index < 0 || index >= choiceNode.Options.Count)
			{
				return false;
			}
			pendingChoice = index;
			return true;
		}

		/// <summary>노드 타입별 진행 출력 포트 id. 미선택 Choice / 미지원 타입 = null(=End).</summary>
		private string OutputPortToFollow(NodeBase node)
		{
			if (node is DialogueSpeakNode)
			{
				return DialogueSpeakNode.PORT_NEXT;
			}
			if (node is DialogueChoiceNode)
			{
				return pendingChoice >= 0 ? DialogueChoiceNode.ChoicePortId(pendingChoice) : null;
			}
			return null;
		}

		private DialogueStep StepForCurrent()
		{
			if (currentNode is DialogueSpeakNode speakNode)
			{
				return DialogueStep.Speak(speakNode.Line);
			}
			if (currentNode is DialogueChoiceNode choiceNode)
			{
				pendingChoice = -1;
				return DialogueStep.Choice(choiceNode.Prompt, choiceNode.Options);
			}
			return DialogueStep.End;
		}

		private DialogueStartNode FindStartNode()
		{
			IReadOnlyList<NodeBase> nodes = graph.Nodes;
			for (int i = 0; i < nodes.Count; i++)
			{
				if (nodes[i] is DialogueStartNode startNode)
				{
					return startNode;
				}
			}
			return null;
		}

		/// <summary>node 의 outputPortId 출력 포트에서 나가는 Flow 연결의 타깃 노드. 없으면 null.</summary>
		private NodeBase FollowFlow(NodeBase node, string outputPortId)
		{
			if (node == null)
			{
				return null;
			}

			IReadOnlyList<NodeConnection> connections = graph.Connections;
			for (int i = 0; i < connections.Count; i++)
			{
				NodeConnection connection = connections[i];
				if (connection == null)
				{
					continue;
				}
				if (connection.SourceNodeId == node.Id && connection.SourcePortId == outputPortId)
				{
					return graph.FindNode(connection.TargetNodeId);
				}
			}
			return null;
		}
	}
}
