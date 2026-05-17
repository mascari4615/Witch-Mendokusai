using System.Collections.Generic;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	public enum DialogueStepKind
	{
		Speak,
		End,
	}

	/// <summary>traversal 이 방출하는 한 스텝. Speak 면 <see cref="SpeakLine"/> 유효, End 면 null.</summary>
	public readonly struct DialogueStep
	{
		public DialogueStepKind Kind { get; }
		public DialogueLine SpeakLine { get; }

		private DialogueStep(DialogueStepKind kind, DialogueLine speakLine)
		{
			Kind = kind;
			SpeakLine = speakLine;
		}

		public static DialogueStep Speak(DialogueLine line) => new(DialogueStepKind.Speak, line);
		public static readonly DialogueStep End = new(DialogueStepKind.End, null);
	}

	/// <summary>
	/// 대화 그래프의 *순수* 플로우 traversal — Unity I/O(코루틴/버블/사운드) 0, 결정적.
	/// 그래서 EditMode 에서 그대로 회귀 잠금 가능(황금의 정신 「피드백 루프 먼저」 = testable seam).
	/// MonoBehaviour 러너(DialogueRunner Phase 2 통합 — 다음 단계)는 이 스텝 시퀀스를 소비해
	/// 버블/typewriter/sfx 연출만 담당(traversal 로직과 분리).
	///
	/// 현 단계(tracer-bullet): <see cref="DialogueStartNode"/> → <see cref="DialogueSpeakNode"/>* → 종료.
	/// Choice/Wait/Branch 는 다음 단계에서 *노드 타입 + 스텝 종류 추가* 만으로 확장 — 코어
	/// "현재 노드의 출력 포트 → 연결 → 타깃" 따라가기 로직은 불변(확장 seam).
	/// </summary>
	public sealed class DialogueGraphTraversal
	{
		private readonly DialogueGraph graph;
		private NodeBase currentNode;

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

		/// <summary>현재 노드의 `next` 플로우 엣지를 따라 다음 스텝. 연결 없거나 끝이면 End.</summary>
		public DialogueStep Next()
		{
			if (currentNode == null)
			{
				return DialogueStep.End;
			}

			currentNode = FollowFlow(currentNode, DialogueSpeakNode.PORT_NEXT);
			return StepForCurrent();
		}

		private DialogueStep StepForCurrent()
		{
			if (currentNode is DialogueSpeakNode speakNode)
			{
				return DialogueStep.Speak(speakNode.Line);
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
