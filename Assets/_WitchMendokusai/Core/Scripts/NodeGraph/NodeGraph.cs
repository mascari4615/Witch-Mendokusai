using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai.NodeGraph
{
	/// <summary>
	/// 노드 그래프 SO — 노드 + 연결 묶음. `[SerializeReference]` 으로 다양한 도메인 노드 polymorphic 직렬화.
	/// 도메인별 그래프는 이를 상속해서 <see cref="Domain"/> override (예: <c>TerrainGraph</c>, <c>ChapterSO</c>).
	/// 직접 `NodeGraph` 인스턴스 자산 = `Generic` 도메인 — fallback / 마이그레이션 (모든 노드 카탈로그 보임).
	///
	/// TASK-WM-059 B1 (2026-05-09): <see cref="DataSO"/> 상속 — 그래프 자체가 카탈로그 메타 호스트 (ID/Name/Description/Sprite).
	/// 도메인 SO (ChapterSO 등) 가 NodeGraph 상속 = 데이터 = 그래프 단일 정본 (변환 어댑터 폐기). DataSOInspector 는 AssetPrefixes 미등록 시 default fallback.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(NodeGraph), menuName = "WM/NodeGraph/" + nameof(NodeGraph))]
	public class NodeGraph : DataSO
	{
		[SerializeReference] private List<NodeBase> nodes = new();
		[SerializeField] private List<NodeConnection> connections = new();

		public IReadOnlyList<NodeBase> Nodes => nodes;
		public IReadOnlyList<NodeConnection> Connections => connections;

		/// <summary>
		/// 그래프 도메인 — 카탈로그 (Editor) 필터링에 사용. 서브클래스가 override.
		/// 기본 = `Generic` 으로, 직접 NodeGraph 인스턴스 자산 (마이그레이션 전) 호환성 유지.
		/// </summary>
		public virtual NodeDomain Domain => NodeDomain.Generic;

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

		/// <summary>도메인 헬퍼 — 그래프 안 첫 T 타입 노드. 없으면 null.</summary>
		public T FindNode<T>() where T : NodeBase
		{
			foreach (NodeBase n in nodes)
				if (n is T typed)
					return typed;
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
