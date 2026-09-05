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

		// --- O(1) lookup 캐시 (TASK: voxel gen 속도 — RegionGridNodeBase 가 256m 영역당 65536 셀
		// 각각 FindNode/FindConnectionToInput 선형 스캔 재귀 → O(N) → O(1)). [NonSerialized] = SO
		// 로드 후 첫 접근 시 lazy build (deserialize 는 mutator 안 거치므로 lazy 가 정답). mutator
		// (AddNode/RemoveNode/Connect/Disconnect/Clear) = 에디터만 → invalidate. 런타임 그래프 불변
		// = build 후 lock-free 동시 read 안전 (background chunk gen 다발 호출). 결과·평가순서 불변. -->
		[System.NonSerialized] private Dictionary<string, NodeBase> nodeByIdCache;
		[System.NonSerialized] private Dictionary<(string targetNodeId, string targetPortId), NodeConnection> connByInputCache;
		[System.NonSerialized] private volatile bool cacheReady;
		[System.NonSerialized] private readonly object cacheBuildLock = new();

		private void EnsureLookupCache()
		{
			if (cacheReady)
				return;
			lock (cacheBuildLock)
			{
				if (cacheReady)
					return;
				Dictionary<string, NodeBase> byId = new(nodes.Count);
				foreach (NodeBase n in nodes)
					if (n != null && string.IsNullOrEmpty(n.Id) == false)
						byId[n.Id] = n;
				Dictionary<(string, string), NodeConnection> byInput = new(connections.Count);
				foreach (NodeConnection c in connections)
					if (c != null)
						byInput[(c.TargetNodeId, c.TargetPortId)] = c;
				nodeByIdCache = byId;
				connByInputCache = byInput;
				cacheReady = true;
			}
		}

		/// <summary>mutator (에디터 그래프 편집) 후 lookup 캐시 무효화. 런타임 호출 X.</summary>
		private void InvalidateLookupCache()
		{
			lock (cacheBuildLock)
			{
				cacheReady = false;
			}
		}

		public IReadOnlyList<NodeBase> Nodes => nodes;

		/// <summary>
		/// 그래프 연결 — base = SerializeField 직렬화 connections. 서브클래스가 override 시
		/// 도메인 데이터에서 derived connections 합산 가능 (예: ChapterSO 의 UnlockEffects → from→to, TASK-WM-059 polish B).
		/// </summary>
		public virtual IReadOnlyList<NodeConnection> Connections => connections;

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
			InvalidateLookupCache();
		}

		public void RemoveNode(NodeBase node)
		{
			if (node == null)
				return;
			connections.RemoveAll(c => c.SourceNodeId == node.Id || c.TargetNodeId == node.Id);
			nodes.Remove(node);
			InvalidateLookupCache();
		}

		public void Clear()
		{
			nodes.Clear();
			connections.Clear();
			InvalidateLookupCache();
		}

		public NodeBase FindNode(string nodeId)
		{
			if (string.IsNullOrEmpty(nodeId))
				return null;
			EnsureLookupCache();
			return nodeByIdCache.TryGetValue(nodeId, out NodeBase n) ? n : null;
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
		/// 두 포트 연결 — 타입 + 방향 검증. 성공 true. 검증 실패 시 false.
		///
		/// **데이터 포트**: 같은 input 에 기존 연결 있으면 교체 (단일 input 의미, ShaderGraph 식) —
		/// Pull 실행기가 값을 하나만 읽으므로 둘이면 어느 쪽인지 정할 수 없다.
		///
		/// **플로우 포트(`FlowSignal`)**: 여럿을 그대로 받는다. 흐름은 반대로 *모이는 게 정상*이다 —
		/// 대화에서 여러 갈래가 같은 장면으로 합류하거나 되돌아오는 건 예외가 아니라 기본형이다.
		/// (2026-08-08 실측: 교체 규칙 때문에 「여러 곳에서 한 장면으로 가기」가 조용히 끊겼다.
		///  나중에 연결한 쪽이 앞의 것을 밀어내서, 원고대로 세운 대화가 중간에서 끝나 버렸다.)
		/// 흐름 순회기는 *출발 포트* 로 다음을 찾으므로 입력이 여럿이어도 모호함이 없다.
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

			if (target.DataType != typeof(FlowSignal))
				connections.RemoveAll(c => c.TargetNodeId == target.Owner.Id && c.TargetPortId == target.PortId);
			connections.Add(new NodeConnection(source.Owner.Id, source.PortId, target.Owner.Id, target.PortId));
			InvalidateLookupCache();
			return true;
		}

		public void Disconnect(NodeConnection c)
		{
			if (c == null)
				return;
			connections.Remove(c);
			InvalidateLookupCache();
		}

		/// <summary>특정 input port 에 연결된 connection (단일 — 단일 input 의미). 없으면 null.</summary>
		public NodeConnection FindConnectionToInput(NodePort input)
		{
			if (input == null)
				return null;
			EnsureLookupCache();
			return connByInputCache.TryGetValue((input.Owner.Id, input.PortId), out NodeConnection c) ? c : null;
		}
	}
}
