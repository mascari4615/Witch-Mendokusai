using System.Collections.Generic;

namespace WitchMendokusai.NodeGraph
{
	/// <summary>
	/// 그래프 정적 검사 — runtime evaluate 전에 connection 무결성 / cycle / type / port 누락을 한 번에 보고.
	///
	/// 동기 + 부작용 0. 호출 위치:
	/// <list type="bullet">
	/// <item>Editor 메뉴 (사용자 수동) — `WM/NodeGraph/Validate Selected` (별도 sub).</item>
	/// <item>그래프 자산 OnValidate / Postprocessor (자동, 별도 sub).</item>
	/// <item>terminal 노드 evaluate 직전 사전 체크 (소비자 옵션).</item>
	/// </list>
	///
	/// `NodeGraph.Connect` 가 이미 insertion 시 type/direction 차단하지만, 다음 케이스들은 거기서 못 잡음:
	/// <list type="bullet">
	/// <item>외부 에디터로 직접 .asset 편집한 케이스.</item>
	/// <item>노드 클래스의 <see cref="NodeBase.CreatePorts"/> 가 변경돼서 옛 connection 의 PortId 가 더 이상 존재 X.</item>
	/// <item>노드 데이터 타입 변경 (`NodePort&lt;float&gt;` → `NodePort&lt;int&gt;`) 후 옛 connection.</item>
	/// <item>노드 삭제 후 dangling connection (`NodeGraph.RemoveNode` 가 청소하지만, 직접 list 편집 케이스 보장 X).</item>
	/// <item>같은 input port 에 여러 connection (Connect 가 자동 교체하지만, 수동 list 편집 케이스).</item>
	/// <item>그래프 sub-loop (A → B → A) — runtime <see cref="NodeExecutionContext"/> 가 evaluate 시점에 throw 하지만, 평가 전 미리 알고 싶음.</item>
	/// </list>
	/// </summary>
	public static class NodeGraphValidator
	{
		public static NodeGraphValidationResult Validate(NodeGraph graph)
		{
			if (graph == null)
			{
				throw new System.ArgumentNullException(nameof(graph));
			}

			NodeGraphValidationResult result = new();
			Dictionary<string, NodeBase> nodesById = BuildNodeIndex(graph, result);
			ValidateConnections(graph, nodesById, result);
			DetectCycles(graph, result);
			ReportUnconnectedInputs(graph, nodesById, result);
			return result;
		}

		private static Dictionary<string, NodeBase> BuildNodeIndex(NodeGraph graph, NodeGraphValidationResult result)
		{
			Dictionary<string, NodeBase> nodesById = new();
			IReadOnlyList<NodeBase> nodes = graph.Nodes;
			for (int i = 0; i < nodes.Count; i++)
			{
				NodeBase node = nodes[i];
				if (node == null)
				{
					result.Add(new NodeGraphIssue(
						NodeGraphIssueKind.NullNodeEntry,
						NodeGraphIssueSeverity.Error,
						$"Node entry at index {i} is null. `[SerializeReference]` likely lost a missing/renamed type."));
					continue;
				}
				if (string.IsNullOrEmpty(node.Id))
				{
					result.Add(new NodeGraphIssue(
						NodeGraphIssueKind.NullNodeEntry,
						NodeGraphIssueSeverity.Error,
						$"Node at index {i} ({node.GetType().Name}) has an empty Id."));
					continue;
				}
				nodesById[node.Id] = node;
			}
			return nodesById;
		}

		private static void ValidateConnections(
			NodeGraph graph,
			Dictionary<string, NodeBase> nodesById,
			NodeGraphValidationResult result)
		{
			IReadOnlyList<NodeConnection> connections = graph.Connections;
			Dictionary<(string nodeId, string portId), int> targetCounts = new();
			HashSet<(string nodeId, string portId)> flowInputs = new();

			for (int i = 0; i < connections.Count; i++)
			{
				NodeConnection connection = connections[i];
				if (connection == null)
				{
					continue;
				}

				bool sourceFound = nodesById.TryGetValue(connection.SourceNodeId, out NodeBase sourceNode);
				bool targetFound = nodesById.TryGetValue(connection.TargetNodeId, out NodeBase targetNode);

				if (sourceFound == false)
				{
					result.Add(new NodeGraphIssue(
						NodeGraphIssueKind.DanglingSourceNode,
						NodeGraphIssueSeverity.Error,
						$"Connection [{i}] source node '{connection.SourceNodeId}' not found in graph."));
				}
				if (targetFound == false)
				{
					result.Add(new NodeGraphIssue(
						NodeGraphIssueKind.DanglingTargetNode,
						NodeGraphIssueSeverity.Error,
						$"Connection [{i}] target node '{connection.TargetNodeId}' not found in graph."));
				}
				if (sourceFound == false || targetFound == false)
				{
					continue;
				}

				NodePort sourcePort = sourceNode.FindPort(connection.SourcePortId, PortDirection.Output);
				NodePort targetPort = targetNode.FindPort(connection.TargetPortId, PortDirection.Input);

				if (sourcePort == null)
				{
					NodePort anyDirection = sourceNode.FindPort(connection.SourcePortId);
					if (anyDirection == null)
					{
						result.Add(new NodeGraphIssue(
							NodeGraphIssueKind.MissingSourcePort,
							NodeGraphIssueSeverity.Error,
							$"Connection [{i}] source port '{connection.SourcePortId}' not declared by node {SafeTypeName(sourceNode)} ({connection.SourceNodeId}).",
							connection.SourceNodeId,
							connection.SourcePortId));
					}
					else
					{
						result.Add(new NodeGraphIssue(
							NodeGraphIssueKind.PortDirectionMismatch,
							NodeGraphIssueSeverity.Error,
							$"Connection [{i}] expected Output port '{connection.SourcePortId}' on {SafeTypeName(sourceNode)} but found {anyDirection.Direction}.",
							connection.SourceNodeId,
							connection.SourcePortId));
					}
				}

				if (targetPort == null)
				{
					NodePort anyDirection = targetNode.FindPort(connection.TargetPortId);
					if (anyDirection == null)
					{
						result.Add(new NodeGraphIssue(
							NodeGraphIssueKind.MissingTargetPort,
							NodeGraphIssueSeverity.Error,
							$"Connection [{i}] target port '{connection.TargetPortId}' not declared by node {SafeTypeName(targetNode)} ({connection.TargetNodeId}).",
							connection.TargetNodeId,
							connection.TargetPortId));
					}
					else
					{
						result.Add(new NodeGraphIssue(
							NodeGraphIssueKind.PortDirectionMismatch,
							NodeGraphIssueSeverity.Error,
							$"Connection [{i}] expected Input port '{connection.TargetPortId}' on {SafeTypeName(targetNode)} but found {anyDirection.Direction}.",
							connection.TargetNodeId,
							connection.TargetPortId));
					}
				}

				if (sourcePort != null && targetPort != null && sourcePort.DataType != targetPort.DataType)
				{
					result.Add(new NodeGraphIssue(
						NodeGraphIssueKind.PortTypeMismatch,
						NodeGraphIssueSeverity.Error,
						$"Connection [{i}] type mismatch: {SafeTypeName(sourceNode)}.{connection.SourcePortId}<{sourcePort.DataType.Name}> → {SafeTypeName(targetNode)}.{connection.TargetPortId}<{targetPort.DataType.Name}>.",
						connection.TargetNodeId,
						connection.TargetPortId));
				}

				// 플로우 입력은 여럿이 정상이다(대화의 합류) — 셈에서 뺀다.
				if (targetPort != null && targetPort.DataType == typeof(FlowSignal))
				{
					flowInputs.Add((connection.TargetNodeId, connection.TargetPortId));
				}

				(string, string) targetKey = (connection.TargetNodeId, connection.TargetPortId);
				if (targetCounts.TryGetValue(targetKey, out int existing))
				{
					targetCounts[targetKey] = existing + 1;
				}
				else
				{
					targetCounts[targetKey] = 1;
				}
			}

			foreach (KeyValuePair<(string nodeId, string portId), int> entry in targetCounts)
			{
				if (entry.Value <= 1 || flowInputs.Contains(entry.Key))
				{
					continue;
				}
				result.Add(new NodeGraphIssue(
					NodeGraphIssueKind.DuplicateTargetConnection,
					NodeGraphIssueSeverity.Warning,
					$"Input port '{entry.Key.portId}' on node {entry.Key.nodeId} has {entry.Value} incoming connections (single-input semantic).",
					entry.Key.nodeId,
					entry.Key.portId));
			}
		}

		private static void DetectCycles(NodeGraph graph, NodeGraphValidationResult result)
		{
			Dictionary<string, List<string>> outgoing = BuildOutgoingAdjacency(graph);
			HashSet<string> visited = new();
			HashSet<string> visiting = new();
			List<string> path = new();
			HashSet<string> reportedCycleSignatures = new();

			IReadOnlyList<NodeBase> nodes = graph.Nodes;
			for (int i = 0; i < nodes.Count; i++)
			{
				NodeBase node = nodes[i];
				if (node == null)
				{
					continue;
				}
				if (visited.Contains(node.Id))
				{
					continue;
				}
				DfsCycle(node.Id, outgoing, visited, visiting, path, reportedCycleSignatures, result);
			}
		}

		private static Dictionary<string, List<string>> BuildOutgoingAdjacency(NodeGraph graph)
		{
			Dictionary<string, List<string>> outgoing = new();
			IReadOnlyList<NodeConnection> connections = graph.Connections;
			for (int i = 0; i < connections.Count; i++)
			{
				NodeConnection connection = connections[i];
				if (connection == null)
				{
					continue;
				}
				if (outgoing.TryGetValue(connection.SourceNodeId, out List<string> targets) == false)
				{
					targets = new List<string>();
					outgoing[connection.SourceNodeId] = targets;
				}
				targets.Add(connection.TargetNodeId);
			}
			return outgoing;
		}

		private static void DfsCycle(
			string nodeId,
			Dictionary<string, List<string>> outgoing,
			HashSet<string> visited,
			HashSet<string> visiting,
			List<string> path,
			HashSet<string> reportedCycleSignatures,
			NodeGraphValidationResult result)
		{
			visiting.Add(nodeId);
			path.Add(nodeId);

			if (outgoing.TryGetValue(nodeId, out List<string> targets))
			{
				for (int i = 0; i < targets.Count; i++)
				{
					string target = targets[i];
					if (visiting.Contains(target))
					{
						List<string> cycleNodes = ExtractCycle(path, target);
						string signature = BuildCycleSignature(cycleNodes);
						if (reportedCycleSignatures.Contains(signature) == false)
						{
							reportedCycleSignatures.Add(signature);
							result.Add(new NodeGraphIssue(
								NodeGraphIssueKind.Cycle,
								NodeGraphIssueSeverity.Error,
								$"Cycle detected: {string.Join(" → ", cycleNodes)} → {target}.",
								target,
								null,
								cycleNodes));
						}
						continue;
					}
					if (visited.Contains(target))
					{
						continue;
					}
					DfsCycle(target, outgoing, visited, visiting, path, reportedCycleSignatures, result);
				}
			}

			path.RemoveAt(path.Count - 1);
			visiting.Remove(nodeId);
			visited.Add(nodeId);
		}

		private static List<string> ExtractCycle(List<string> path, string startNodeId)
		{
			List<string> cycle = new();
			bool found = false;
			for (int i = 0; i < path.Count; i++)
			{
				if (found == false && path[i] == startNodeId)
				{
					found = true;
				}
				if (found)
				{
					cycle.Add(path[i]);
				}
			}
			return cycle;
		}

		/// <summary>같은 cycle 을 다른 진입점에서 재발견했을 때 중복 이슈 방지 — 정렬된 노드 ID 리스트가 키.</summary>
		private static string BuildCycleSignature(List<string> cycleNodes)
		{
			List<string> sorted = new(cycleNodes);
			sorted.Sort(System.StringComparer.Ordinal);
			return string.Join(",", sorted);
		}

		private static void ReportUnconnectedInputs(
			NodeGraph graph,
			Dictionary<string, NodeBase> nodesById,
			NodeGraphValidationResult result)
		{
			HashSet<(string nodeId, string portId)> connectedTargets = new();
			IReadOnlyList<NodeConnection> connections = graph.Connections;
			for (int i = 0; i < connections.Count; i++)
			{
				NodeConnection connection = connections[i];
				if (connection == null)
				{
					continue;
				}
				connectedTargets.Add((connection.TargetNodeId, connection.TargetPortId));
			}

			foreach (KeyValuePair<string, NodeBase> entry in nodesById)
			{
				NodeBase node = entry.Value;
				foreach (NodePort port in node.InputPorts)
				{
					if (connectedTargets.Contains((node.Id, port.PortId)))
					{
						continue;
					}
					result.Add(new NodeGraphIssue(
						NodeGraphIssueKind.UnconnectedInput,
						NodeGraphIssueSeverity.Info,
						$"Input port '{port.PortId}' on {SafeTypeName(node)} ({node.Id}) has no incoming connection (default value will be used).",
						node.Id,
						port.PortId));
				}
			}
		}

		private static string SafeTypeName(NodeBase node) => node == null ? "<null>" : node.GetType().Name;
	}
}
