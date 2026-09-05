using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// 마력 배선 망의 그래프 모델 — 순수 POCO. RoadGraph(격자 셀 dict, 4-이웃 자동 인접) 와 닮은
	// BFS·연결 컴포넌트 토대를 가지나 데이터 모델이 직교:
	//   RoadGraph    = Vector3Int 셀 + 4-이웃(엣지 런타임 유도) + 길이 항상 1
	//   LeylineGraph = string id 노드(임의 위치) + 명시적 엣지 + 가중치 Length
	// 「격자 위 통근」 vs 「거점 간 가중치 흐름」 의 의미 차이가 자료구조 차이로 그대로 나타난다.
	//
	// 향후 공통화 여지: Neighbors/FindShortestPath 추상이 두 그래프에 공통(WM-164 Phase 2 deferred
	// 의 RoadGraph BFS 중복 추출과 함께 IGraph<T> 로 묶기 가능). 단 Phase 0 = first-use 우선,
	// 신규 시 선제 추상화 금지 (CLAUDE.md 「데드 인터페이스 방지」).
	//
	// 알고리즘: 최단 경로 = Dijkstra(엣지 length 가중치). RoadGraph 의 BFS(균등 가중치) 대신
	// length 차이를 의미있게 반영 — Phase 0 핵심 퍼즐(「긴 우회 vs 짧은 직선」) 의 모델 기반.
	public sealed class LeylineGraph
	{
		private readonly Dictionary<string, LeylineNode> nodes = new();
		private readonly List<LeylineEdge> edges = new();
		private readonly Dictionary<string, List<LeylineEdge>> outgoingByNode = new();

		public IReadOnlyDictionary<string, LeylineNode> Nodes => nodes;
		public IReadOnlyList<LeylineEdge> Edges => edges;

		// 노드 등록 — 같은 id 재등록은 덮어쓰기(RoadGraph.AddRoad 의 페인트 멱등성 답습 — UGC
		// 워크플로에서 노드 속성 변경이 자주 일어남).
		public void AddNode(LeylineNode node)
		{
			if (node == null)
			{
				throw new ArgumentNullException(nameof(node));
			}

			nodes[node.Id] = node;
			if (outgoingByNode.ContainsKey(node.Id) == false)
			{
				outgoingByNode[node.Id] = new List<LeylineEdge>();
			}
		}

		// 엣지 등록 — 양 끝 노드가 사전 등록돼야 함(FastFail). RoadGraph 가 셀 인접성만 검사하면
		// 되는 것과 달리, 엣지가 명시적이라 양 끝점 무결성을 그래프가 책임짐.
		public void AddEdge(LeylineEdge edge)
		{
			if (edge == null)
			{
				throw new ArgumentNullException(nameof(edge));
			}

			if (nodes.ContainsKey(edge.FromId) == false)
			{
				throw new InvalidOperationException($"LeylineEdge.FromId '{edge.FromId}' 노드가 그래프에 없음 — AddNode 먼저");
			}

			if (nodes.ContainsKey(edge.ToId) == false)
			{
				throw new InvalidOperationException($"LeylineEdge.ToId '{edge.ToId}' 노드가 그래프에 없음 — AddNode 먼저");
			}

			edges.Add(edge);
			outgoingByNode[edge.FromId].Add(edge);
		}

		// 한 노드에서 나가는 엣지들(directional). 미등록 노드면 빈 — FastFail 아닌 정상 질의 결과
		// (RoadGraph.Neighbors 와 같은 형식).
		public IEnumerable<LeylineEdge> Outgoing(string nodeId)
		{
			if (outgoingByNode.TryGetValue(nodeId, out List<LeylineEdge> outgoing))
			{
				return outgoing;
			}

			return System.Array.Empty<LeylineEdge>();
		}

		// 최단 경로(엣지 length 합 최소). Dijkstra — 양수 가중치 보장(LeylineEdge.Length > 0).
		// from→to 노드 id 시퀀스 반환. 미연결이면 빈 리스트(RoadGraph.FindPath 와 동형 — FastFail
		// 아닌 「경로 없음」 정상 결과). from==to 면 단일 원소.
		//
		// O((V+E) log V) 가 정석이나 Phase 0 망 규모(<100 노드 예상)에선 단순 O((V+E) V) 우선순위
		// 탐색으로 충분 — 노드 수 폭발 시 PriorityQueue<T> (System.Collections.Generic, .NET 6+) 로 격상.
		public List<string> FindShortestPath(string fromId, string toId)
		{
			List<string> path = new();

			if (nodes.ContainsKey(fromId) == false || nodes.ContainsKey(toId) == false)
			{
				return path;
			}

			if (fromId == toId)
			{
				path.Add(fromId);
				return path;
			}

			Dictionary<string, float> distances = new();
			Dictionary<string, string> predecessor = new();
			HashSet<string> visited = new();

			foreach (string nodeId in nodes.Keys)
			{
				distances[nodeId] = float.PositiveInfinity;
			}
			distances[fromId] = 0f;

			while (true)
			{
				// 미방문 노드 중 거리 최소 선택.
				string current = null;
				float currentDistance = float.PositiveInfinity;
				foreach ((string nodeId, float distance) in distances)
				{
					if (visited.Contains(nodeId))
					{
						continue;
					}

					if (distance < currentDistance)
					{
						currentDistance = distance;
						current = nodeId;
					}
				}

				if (current == null || float.IsPositiveInfinity(currentDistance))
				{
					// 도달 가능 노드 소진.
					break;
				}

				if (current == toId)
				{
					break;
				}

				visited.Add(current);

				foreach (LeylineEdge edge in outgoingByNode[current])
				{
					float candidate = currentDistance + edge.Length;
					if (candidate < distances[edge.ToId])
					{
						distances[edge.ToId] = candidate;
						predecessor[edge.ToId] = current;
					}
				}
			}

			if (float.IsPositiveInfinity(distances[toId]))
			{
				return path;
			}

			// to → from 역추적 후 뒤집어 from→to 순서로 (RoadGraph.FindPath 패턴 답습).
			string step = toId;
			path.Add(step);
			while (step != fromId)
			{
				step = predecessor[step];
				path.Add(step);
			}
			path.Reverse();

			return path;
		}

		// 노드 id 시퀀스의 총 엣지 길이 합. 인접 노드 쌍에 대응하는 엣지를 outgoing 에서 찾아
		// 누적 — 같은 from→to 사이에 다중 엣지가 있으면 최단 엣지 채택(망 설계 의도와 일치:
		// 두 노드를 다중 배선으로 잇는 건 redundancy 표현, 흐름은 짧은 쪽으로).
		// 경로가 빈/단일 노드면 0 (정상 결과).
		public float PathLength(List<string> path)
		{
			if (path == null || path.Count < 2)
			{
				return 0f;
			}

			float total = 0f;
			for (int i = 1; i < path.Count; i++)
			{
				string from = path[i - 1];
				string to = path[i];
				float bestLength = float.PositiveInfinity;
				foreach (LeylineEdge edge in Outgoing(from))
				{
					if (edge.ToId == to && edge.Length < bestLength)
					{
						bestLength = edge.Length;
					}
				}

				if (float.IsPositiveInfinity(bestLength))
				{
					throw new InvalidOperationException($"경로 인접 쌍 '{from}'→'{to}' 에 엣지 없음 — 잘못된 경로");
				}

				total += bestLength;
			}

			return total;
		}
	}
}
