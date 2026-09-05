using System;
using System.Collections.Generic;

namespace WitchMendokusai.NodeGraph
{
	/// <summary>
	/// Pull-based 그래프 실행기. terminal 노드 <see cref="Evaluate"/> 호출 → 그 노드가 input 평가 시
	/// `GetInput&lt;T&gt;` 호출 → context 가 source 노드 재귀 evaluate + output 캐시.
	///
	/// 1회 그래프 평가 안 같은 노드는 1번만 evaluate (캐시). cycle 검출 → throw.
	/// </summary>
	public class NodeExecutionContext
	{
		private readonly NodeGraph graph;
		private readonly Dictionary<(string nodeId, string portId), object> outputCache = new();
		private readonly HashSet<string> evaluating = new();
		private readonly HashSet<string> evaluated = new();
		private readonly Dictionary<string, object> globalInputs = new();

		public NodeGraph Graph => graph;

		public NodeExecutionContext(NodeGraph graph)
		{
			this.graph = graph;
		}

		/// <summary>
		/// 컨텍스트 재사용 — 4개 상태 컬렉션 클리어 후 같은 graph 로 다음 평가.
		/// per-call `new` 폐기용 (TASK-WM-119: RegionGridNodeBase 가 256m 영역당 65536 셀
		/// 각각 new → 26만 alloc). 같은 thread / lock 보호 구간에서만 재사용 — graph 불변.
		/// 결과·평가순서 불변 (fresh context 와 동일 — 빈 상태에서 시작).
		/// </summary>
		public void Reset()
		{
			outputCache.Clear();
			evaluating.Clear();
			evaluated.Clear();
			globalInputs.Clear();
		}

		/// <summary>해당 input port 의 값 — connected source 노드를 재귀 평가 (캐시).
		/// 미연결 input 또는 source 노드 누락이면 default(T).</summary>
		public T GetInput<T>(NodePort<T> input)
		{
			if (input == null || graph == null)
				return default;
			NodeConnection conn = graph.FindConnectionToInput(input);
			if (conn == null)
				return default;

			(string, string) key = (conn.SourceNodeId, conn.SourcePortId);
			if (outputCache.TryGetValue(key, out object cached) && cached is T typedCached)
				return typedCached;

			NodeBase sourceNode = graph.FindNode(conn.SourceNodeId);
			if (sourceNode == null)
				return default;

			EvaluateNodeOnce(sourceNode);

			if (outputCache.TryGetValue(key, out cached) && cached is T resolved)
				return resolved;
			return default;
		}

		/// <summary>output port 결과 캐시 — 노드 OnEvaluate 안에서 호출.</summary>
		public void SetOutput<T>(NodePort<T> output, T value)
		{
			if (output == null)
				return;
			outputCache[(output.Owner.Id, output.PortId)] = value;
		}

		/// <summary>output port cached 값 직접 읽기 (Editor preview 등 외부 호출용). 없거나 타입 불일치 시 default.</summary>
		public T GetOutput<T>(NodePort<T> output)
		{
			if (output == null)
				return default;
			if (outputCache.TryGetValue((output.Owner.Id, output.PortId), out object cached) && cached is T typed)
				return typed;
			return default;
		}

		/// <summary>
		/// per-eval 글로벌 입력 — 호출자가 evaluate 전에 값 넣고, 도메인별 input 노드 (예: WorldPositionInputNode)
		/// 가 OnEvaluate 안에서 읽음. context 인스턴스가 per-eval 이라 thread-safe (background chunk gen 다발 호출).
		/// </summary>
		public void SetGlobalInput<T>(string key, T value)
		{
			if (key == null)
				return;
			globalInputs[key] = value;
		}

		public bool TryGetGlobalInput<T>(string key, out T value)
		{
			if (key != null && globalInputs.TryGetValue(key, out object raw) && raw is T typed)
			{
				value = typed;
				return true;
			}
			value = default;
			return false;
		}

		/// <summary>그래프 안 노드 1개 평가 — pull 시작점. 보통 terminal/output 노드.
		/// 같은 노드 중복 evaluate 안 되게 cycle 검출 + 캐시 검사.</summary>
		public void Evaluate(NodeBase node)
		{
			if (node == null)
				return;
			EvaluateNodeOnce(node);
		}

		private void EvaluateNodeOnce(NodeBase node)
		{
			if (evaluated.Contains(node.Id))
				return;
			if (evaluating.Contains(node.Id))
				throw new InvalidOperationException($"[NodeExecutionContext] Cycle detected at node {node.Id} ({node.GetType().Name}).");

			evaluating.Add(node.Id);
			try
			{
				node.Evaluate(this);
				evaluated.Add(node.Id);
			}
			finally
			{
				evaluating.Remove(node.Id);
			}
		}
	}
}
