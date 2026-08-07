using System.Collections.Generic;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>대화 그래프에서만 뜻이 있는 결함 종류. 일반 그래프 결함은 <see cref="NodeGraphIssueKind"/>.</summary>
	public enum DialogueGraphIssueKind
	{
		/// <summary>진입점이 없다 — 재생하면 아무 말도 안 하고 끝난다.</summary>
		NoStartNode = 0,

		/// <summary>진입점이 둘 이상 — 어느 쪽으로 시작할지 목록 순서가 정한다(불안정).</summary>
		MultipleStartNodes = 1,

		/// <summary>분기 조건이 비었다 — 재생 중 그 자리에서 터진다.</summary>
		BranchWithoutCondition = 2,

		/// <summary>말하기 노드에 대사가 없다 — 빈 말풍선이 뜬다.</summary>
		SpeakWithoutLine = 3,

		/// <summary>선택지가 하나도 없다 — 고를 게 없어 대화가 거기서 멈춘다.</summary>
		ChoiceWithoutOptions = 4,

		/// <summary>선택지 하나가 아무 데도 안 이어졌다 — 고르면 대화가 조용히 끝난다.</summary>
		ChoiceOptionNotConnected = 5,

		/// <summary>사건 대기인데 기다릴 사건 이름이 비었다 — 영원히 안 풀린다.</summary>
		WaitEventWithoutId = 6,

		/// <summary>시작점에서 닿을 수 없는 노드 — 만들어 놓고 안 이어진 것.</summary>
		UnreachableNode = 7,

		/// <summary>모든 선택지에 조건이 걸렸다 — 하나도 안 맞는 상황이 오면 대화가 거기서 끝난다.</summary>
		ChoiceMayHaveNoAvailableOption = 8,

		/// <summary>효과 노드가 비었다 — 뭔가 일으키라고 놓고 아무것도 안 적혔다.</summary>
		EffectNodeWithoutEffects = 9,

		/// <summary>거기 들어가면 대화가 영영 안 끝난다 — 끝으로 가는 길이 없는 고리.</summary>
		CannotReachEnd = 10,
	}

	public sealed class DialogueGraphIssue
	{
		public DialogueGraphIssueKind Kind { get; }
		public NodeGraphIssueSeverity Severity { get; }
		public string Message { get; }
		public string NodeId { get; }

		public DialogueGraphIssue(DialogueGraphIssueKind kind, NodeGraphIssueSeverity severity, string message, string nodeId = null)
		{
			Kind = kind;
			Severity = severity;
			Message = message;
			NodeId = nodeId;
		}
	}

	public sealed class DialogueGraphValidationResult
	{
		private readonly List<DialogueGraphIssue> issues = new();

		public IReadOnlyList<DialogueGraphIssue> Issues => issues;
		public int ErrorCount { get; private set; }
		public int WarningCount { get; private set; }

		public bool HasErrors => ErrorCount > 0;
		public bool IsValid => ErrorCount == 0;

		public void Add(DialogueGraphIssue issue)
		{
			issues.Add(issue);
			if (issue.Severity == NodeGraphIssueSeverity.Error)
			{
				ErrorCount++;
			}
			else if (issue.Severity == NodeGraphIssueSeverity.Warning)
			{
				WarningCount++;
			}
		}

		/// <summary>해당 종류의 결함이 몇 건인지 — 검사/에디터가 「그 규칙이 걸렸나」를 볼 때.</summary>
		public int CountOf(DialogueGraphIssueKind kind)
		{
			int count = 0;
			for (int i = 0; i < issues.Count; i++)
			{
				if (issues[i].Kind == kind)
				{
					count++;
				}
			}
			return count;
		}
	}

	/// <summary>
	/// 대화 그래프 정적 검사 (TASK-WM-052).
	///
	/// ★ 왜 필요한가: 여기서 잡는 것들은 **예외를 안 내고 조용히 이상해진다** — 빈 말풍선이 뜨거나,
	///   고르자마자 대화가 끝나거나, 영원히 안 풀리는 대기에 걸린다. 화면으로만 잡으려면
	///   대사 하나하나 눌러 봐야 하고, 안 눌러 본 가지는 영영 안 잡힌다.
	///   (`DialogueBranchNode` 의 조건 미할당만 예외 — 그건 재생 중 터진다. 터지기 *전에* 알려주는 게 여기.)
	///
	/// 일반 그래프 무결성(연결 dangling·포트 타입·고리)은 <see cref="NodeGraphValidator"/> 몫 —
	/// 이 검사는 그 위에 *대화에서만 뜻이 있는 규칙*만 얹는다(Core 열거형에 도메인 값 안 넣는다).
	/// 부작용 0 · Unity I/O 0 = EditMode 에서 그대로 돈다.
	/// </summary>
	public static class DialogueGraphValidator
	{
		public static DialogueGraphValidationResult Validate(DialogueGraph graph)
		{
			DialogueGraphValidationResult result = new();
			if (graph == null)
			{
				result.Add(new DialogueGraphIssue(
					DialogueGraphIssueKind.NoStartNode,
					NodeGraphIssueSeverity.Error,
					"Dialogue graph is null."));
				return result;
			}

			IReadOnlyList<NodeBase> nodes = graph.Nodes;
			ValidateStartNodes(nodes, result);
			ValidateNodeContents(graph, nodes, result);
			ValidateReachability(graph, nodes, result);
			ValidateCanReachEnd(graph, nodes, result);
			return result;
		}

		private static void ValidateStartNodes(IReadOnlyList<NodeBase> nodes, DialogueGraphValidationResult result)
		{
			int startCount = 0;
			for (int i = 0; i < nodes.Count; i++)
			{
				if (nodes[i] is DialogueStartNode)
				{
					startCount++;
				}
			}

			if (startCount == 0)
			{
				result.Add(new DialogueGraphIssue(
					DialogueGraphIssueKind.NoStartNode,
					NodeGraphIssueSeverity.Error,
					"No DialogueStartNode — playing this graph says nothing and ends immediately."));
				return;
			}

			if (startCount > 1)
			{
				result.Add(new DialogueGraphIssue(
					DialogueGraphIssueKind.MultipleStartNodes,
					NodeGraphIssueSeverity.Warning,
					$"{startCount} DialogueStartNodes — traversal takes the first in list order, which is not a stable authoring intent."));
			}
		}

		private static void ValidateNodeContents(DialogueGraph graph, IReadOnlyList<NodeBase> nodes, DialogueGraphValidationResult result)
		{
			for (int i = 0; i < nodes.Count; i++)
			{
				NodeBase node = nodes[i];
				if (node == null)
				{
					continue;
				}

				if (node is DialogueSpeakNode speakNode && speakNode.Line == null)
				{
					result.Add(new DialogueGraphIssue(
						DialogueGraphIssueKind.SpeakWithoutLine,
						NodeGraphIssueSeverity.Error,
						$"DialogueSpeakNode '{node.Id}' has no DialogueLine — an empty bubble would be shown.",
						node.Id));
				}

				if (node is DialogueBranchNode branchNode && branchNode.Condition == null)
				{
					result.Add(new DialogueGraphIssue(
						DialogueGraphIssueKind.BranchWithoutCondition,
						NodeGraphIssueSeverity.Error,
						$"DialogueBranchNode '{node.Id}' has no Condition — playback throws when it reaches this node.",
						node.Id));
				}

				if (node is DialogueWaitNode waitNode
					&& waitNode.Kind == DialogueWaitKind.Event
					&& string.IsNullOrWhiteSpace(waitNode.EventId))
				{
					result.Add(new DialogueGraphIssue(
						DialogueGraphIssueKind.WaitEventWithoutId,
						NodeGraphIssueSeverity.Error,
						$"DialogueWaitNode '{node.Id}' waits for an event but has no EventId — it can never resolve.",
						node.Id));
				}

				if (node is DialogueEffectNode effectNode && effectNode.Effects.Count == 0 && effectNode.EffectData.Count == 0)
				{
					result.Add(new DialogueGraphIssue(
						DialogueGraphIssueKind.EffectNodeWithoutEffects,
						NodeGraphIssueSeverity.Warning,
						$"DialogueEffectNode '{node.Id}' has no effects — it passes through doing nothing.",
						node.Id));
				}

				if (node is DialogueChoiceNode choiceNode)
				{
					ValidateChoiceNode(graph, choiceNode, result);
				}
			}
		}

		private static void ValidateChoiceNode(DialogueGraph graph, DialogueChoiceNode choiceNode, DialogueGraphValidationResult result)
		{
			if (choiceNode.Options.Count == 0)
			{
				result.Add(new DialogueGraphIssue(
					DialogueGraphIssueKind.ChoiceWithoutOptions,
					NodeGraphIssueSeverity.Error,
					$"DialogueChoiceNode '{choiceNode.Id}' has no options — nothing to pick, so the dialogue stalls there.",
					choiceNode.Id));
				return;
			}

			bool everyOptionConditional = true;
			for (int i = 0; i < choiceNode.Options.Count; i++)
			{
				DialogueChoiceOption option = choiceNode.Options[i];
				if (option == null || option.Condition == null)
				{
					everyOptionConditional = false;
				}
				if (HasOutgoing(graph, choiceNode.Id, DialogueChoiceNode.ChoicePortId(i)))
				{
					continue;
				}
				result.Add(new DialogueGraphIssue(
					DialogueGraphIssueKind.ChoiceOptionNotConnected,
					NodeGraphIssueSeverity.Warning,
					$"DialogueChoiceNode '{choiceNode.Id}' option {i} (\"{choiceNode.Options[i].Label}\") goes nowhere — picking it ends the dialogue silently.",
					choiceNode.Id));
			}

			if (everyOptionConditional == false)
			{
				return;
			}
			result.Add(new DialogueGraphIssue(
				DialogueGraphIssueKind.ChoiceMayHaveNoAvailableOption,
				NodeGraphIssueSeverity.Warning,
				$"DialogueChoiceNode '{choiceNode.Id}': every option is conditional — if none match, the dialogue ends here with nothing shown. Consider one unconditional fallback.",
				choiceNode.Id));
		}

		private static void ValidateReachability(DialogueGraph graph, IReadOnlyList<NodeBase> nodes, DialogueGraphValidationResult result)
		{
			DialogueStartNode startNode = null;
			for (int i = 0; i < nodes.Count; i++)
			{
				if (nodes[i] is DialogueStartNode found)
				{
					startNode = found;
					break;
				}
			}
			if (startNode == null)
			{
				return;
			}

			HashSet<string> reached = new() { startNode.Id };
			Queue<string> frontier = new();
			frontier.Enqueue(startNode.Id);

			IReadOnlyList<NodeConnection> connections = graph.Connections;
			while (frontier.Count > 0)
			{
				string current = frontier.Dequeue();
				for (int i = 0; i < connections.Count; i++)
				{
					NodeConnection connection = connections[i];
					if (connection == null || connection.SourceNodeId != current)
					{
						continue;
					}
					if (reached.Add(connection.TargetNodeId))
					{
						frontier.Enqueue(connection.TargetNodeId);
					}
				}
			}

			for (int i = 0; i < nodes.Count; i++)
			{
				NodeBase node = nodes[i];
				if (node == null || reached.Contains(node.Id))
				{
					continue;
				}
				result.Add(new DialogueGraphIssue(
					DialogueGraphIssueKind.UnreachableNode,
					NodeGraphIssueSeverity.Warning,
					$"{node.GetType().Name} '{node.Id}' cannot be reached from the start node — it never plays.",
					node.Id));
			}
		}

		/// <summary>
		/// **거기 들어가면 대화가 영영 안 끝나는 자리**를 찾는다.
		///
		/// ★ 왜 필요한가: 흐름 연결이 여럿 모일 수 있게 된 뒤로 **고리(A→B→A)를 정상 편집으로도 만들 수 있다.**
		///   고리 자체는 정상이다(허브로 돌아오는 대화). 문제는 **그 고리에서 나가는 길이 하나도 없을 때**다 —
		///   플레이어는 같은 대사를 영원히 돌게 되고, 그 사이 뒤에 줄 선 대화까지 전부 막힌다.
		///   눈으로는 「무한 반복」으로만 보이고 어디가 원인인지 안 보인다.
		///
		/// 판정: 나가는 연결이 없는 자리(= 대화 끝) 에서 **거꾸로** 훑어, 거기 닿지 못하는 노드를 고른다.
		/// </summary>
		private static void ValidateCanReachEnd(DialogueGraph graph, IReadOnlyList<NodeBase> nodes, DialogueGraphValidationResult result)
		{
			IReadOnlyList<NodeConnection> connections = graph.Connections;

			Dictionary<string, List<string>> incoming = new();
			HashSet<string> hasOutgoing = new();
			for (int i = 0; i < connections.Count; i++)
			{
				NodeConnection connection = connections[i];
				if (connection == null)
				{
					continue;
				}
				hasOutgoing.Add(connection.SourceNodeId);
				if (incoming.TryGetValue(connection.TargetNodeId, out List<string> sources) == false)
				{
					sources = new List<string>();
					incoming[connection.TargetNodeId] = sources;
				}
				sources.Add(connection.SourceNodeId);
			}

			// 끝나는 자리 = 나가는 연결이 하나도 없는 노드. 거기서 거꾸로 닿는 곳은 다 「끝에 갈 수 있다」.
			HashSet<string> canReachEnd = new();
			Queue<string> frontier = new();
			for (int i = 0; i < nodes.Count; i++)
			{
				NodeBase node = nodes[i];
				if (node == null || hasOutgoing.Contains(node.Id))
				{
					continue;
				}
				if (canReachEnd.Add(node.Id))
				{
					frontier.Enqueue(node.Id);
				}
			}

			while (frontier.Count > 0)
			{
				string current = frontier.Dequeue();
				if (incoming.TryGetValue(current, out List<string> sources) == false)
				{
					continue;
				}
				for (int i = 0; i < sources.Count; i++)
				{
					if (canReachEnd.Add(sources[i]))
					{
						frontier.Enqueue(sources[i]);
					}
				}
			}

			for (int i = 0; i < nodes.Count; i++)
			{
				NodeBase node = nodes[i];
				if (node == null || canReachEnd.Contains(node.Id))
				{
					continue;
				}
				result.Add(new DialogueGraphIssue(
					DialogueGraphIssueKind.CannotReachEnd,
					NodeGraphIssueSeverity.Error,
					$"{node.GetType().Name} '{node.Id}' 에서는 대화가 끝나는 곳으로 갈 수 없다 — 들어가면 영원히 돈다.",
					node.Id));
			}
		}

		private static bool HasOutgoing(DialogueGraph graph, string nodeId, string portId)
		{
			IReadOnlyList<NodeConnection> connections = graph.Connections;
			for (int i = 0; i < connections.Count; i++)
			{
				NodeConnection connection = connections[i];
				if (connection == null)
				{
					continue;
				}
				if (connection.SourceNodeId == nodeId && connection.SourcePortId == portId)
				{
					return true;
				}
			}
			return false;
		}
	}
}
