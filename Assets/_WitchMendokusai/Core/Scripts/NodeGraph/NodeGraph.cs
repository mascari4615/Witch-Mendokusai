using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai.NodeGraph
{
	/// <summary>
	/// 노드 그래프 SO — 노드 + 연결 묶음. `[SerializeReference]` 으로 다양한 도메인 노드 polymorphic 직렬화.
	/// 도메인별 그래프는 이를 상속해서 표시 / 카탈로그 분리 (TASK-WM-034 단계 C). 1차는 generic.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(NodeGraph), menuName = "WM/NodeGraph/" + nameof(NodeGraph))]
	public class NodeGraph : ScriptableObject
	{
		[SerializeReference] private List<NodeBase> nodes = new();
		[SerializeField] private List<NodeConnection> connections = new();

		public IReadOnlyList<NodeBase> Nodes => nodes;
		public IReadOnlyList<NodeConnection> Connections => connections;

		public void AddNode(NodeBase node)
		{
			if (node == null)
				return;
			nodes.Add(node);
		}

		public void RemoveNode(NodeBase node)
		{
			if (node == null)
				return;
			connections.RemoveAll(c => c.SourceNodeId == node.Id || c.TargetNodeId == node.Id);
			nodes.Remove(node);
		}

		public void Clear()
		{
			nodes.Clear();
			connections.Clear();
		}

		public NodeBase FindNode(string nodeId)
		{
			if (string.IsNullOrEmpty(nodeId))
				return null;
			foreach (NodeBase n in nodes)
				if (n != null && n.Id == nodeId)
					return n;
			return null;
		}

		/// <summary>
		/// 두 포트 연결 — 타입 + 방향 검증. 같은 input 에 기존 연결 있으면 교체 (단일 input 의미, ShaderGraph 식).
		/// 성공 true. 검증 실패 시 false.
		/// </summary>
		public bool Connect(NodePort source, NodePort target)
		{
			if (source == null || target == null)
				return false;
			if (source.Direction != PortDirection.Output)
				return false;
			if (target.Direction != PortDirection.Input)
				return false;
			if (source.DataType != target.DataType)
				return false;

			connections.RemoveAll(c => c.TargetNodeId == target.Owner.Id && c.TargetPortId == target.PortId);
			connections.Add(new NodeConnection(source.Owner.Id, source.PortId, target.Owner.Id, target.PortId));
			return true;
		}

		public void Disconnect(NodeConnection c)
		{
			if (c == null)
				return;
			connections.Remove(c);
		}

		/// <summary>특정 input port 에 연결된 connection (단일 — 단일 input 의미). 없으면 null.</summary>
		public NodeConnection FindConnectionToInput(NodePort input)
		{
			if (input == null)
				return null;
			foreach (NodeConnection c in connections)
				if (c.TargetNodeId == input.Owner.Id && c.TargetPortId == input.PortId)
					return c;
			return null;
		}
	}
}
