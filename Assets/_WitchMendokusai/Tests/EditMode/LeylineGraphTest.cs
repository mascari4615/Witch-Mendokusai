using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-173 Phase 0 — <see cref="LeylineGraph"/> 명시적 가중치 그래프 회귀 잠금.
	///
	/// LeylineGraph 는 마력의 강 Phase 0 흐름 모델의 토대 — RoadGraph 와 구조는 닮았으나
	/// 데이터 모델이 다름(임의 위치 노드 id + 명시적 가중치 엣지 vs 격자 셀 + 4-이웃). 결정성·경로
	/// 길이·미연결·자기자신 같은 핵심 불변식을 잠근다. RoadGraphTest 패턴 답습(new() + Assert.That).
	///
	/// 순수 POCO — Unity 의존 0.
	/// </summary>
	public sealed class LeylineGraphTest
	{
		private static LeylineNode N(string id, LeylineNodeKind kind = LeylineNodeKind.Relay)
		{
			return new LeylineNode(id, kind);
		}

		[Test]
		public void AddNode_RegistersNodeById()
		{
			LeylineGraph graph = new();
			LeylineNode gate = N("gate", LeylineNodeKind.Source);

			graph.AddNode(gate);

			Assert.That(graph.Nodes.ContainsKey("gate"), Is.True);
			Assert.That(graph.Nodes["gate"].Kind, Is.EqualTo(LeylineNodeKind.Source));
		}

		[Test]
		public void AddNode_SameIdOverwrites_KindUpdates()
		{
			// RoadGraph.AddRoad 페인트 멱등성과 동형 — UGC 워크플로에서 노드 속성 변경 흔함.
			LeylineGraph graph = new();
			graph.AddNode(N("workshop", LeylineNodeKind.Relay));
			graph.AddNode(N("workshop", LeylineNodeKind.Sink));

			Assert.That(graph.Nodes.Count, Is.EqualTo(1));
			Assert.That(graph.Nodes["workshop"].Kind, Is.EqualTo(LeylineNodeKind.Sink));
		}

		[Test]
		public void AddEdge_UnknownEndpoint_Throws()
		{
			// FastFail — 그래프 무결성이 엣지 양 끝점 등록 보장에 달려있음.
			LeylineGraph graph = new();
			graph.AddNode(N("a"));

			Assert.Throws<InvalidOperationException>(() => graph.AddEdge(new LeylineEdge("a", "ghost", 1f)));
			Assert.Throws<InvalidOperationException>(() => graph.AddEdge(new LeylineEdge("ghost", "a", 1f)));
		}

		[Test]
		public void Outgoing_OnlyFromEdges_Directional()
		{
			LeylineGraph graph = new();
			graph.AddNode(N("a"));
			graph.AddNode(N("b"));
			graph.AddNode(N("c"));
			graph.AddEdge(new LeylineEdge("a", "b", 1f));
			graph.AddEdge(new LeylineEdge("a", "c", 2f));
			graph.AddEdge(new LeylineEdge("b", "a", 5f));

			List<LeylineEdge> aOut = graph.Outgoing("a").ToList();

			Assert.That(aOut.Count, Is.EqualTo(2), "a 에서 나가는 엣지는 a→b, a→c 2개 (b→a 역방향 제외)");
			Assert.That(aOut.Any(edge => edge.ToId == "b"), Is.True);
			Assert.That(aOut.Any(edge => edge.ToId == "c"), Is.True);
		}

		[Test]
		public void Outgoing_UnknownNode_ReturnsEmpty()
		{
			// 미등록 노드 질의는 빈 (RoadGraph.Neighbors 와 같은 형식 — FastFail 아닌 정상 결과).
			LeylineGraph graph = new();
			Assert.That(graph.Outgoing("ghost"), Is.Empty);
		}

		[Test]
		public void FindShortestPath_DirectEdge_ReturnsTwoNodes()
		{
			LeylineGraph graph = new();
			graph.AddNode(N("gate", LeylineNodeKind.Source));
			graph.AddNode(N("workshop", LeylineNodeKind.Sink));
			graph.AddEdge(new LeylineEdge("gate", "workshop", 4f));

			List<string> path = graph.FindShortestPath("gate", "workshop");

			Assert.That(path, Is.EqualTo(new List<string> { "gate", "workshop" }));
		}

		[Test]
		public void FindShortestPath_TwoRoutes_PicksShorterByLength()
		{
			// 직선 (gate→workshop, 길이 10) vs 우회 (gate→relay→workshop, 1+2=3).
			// BFS(균등 가중치) 라면 직선(1 hop) 을 고르지만, Dijkstra(가중치) 는 우회(3) 선택.
			// 이게 RoadGraph.FindPath(BFS) 와의 결정적 차이 — Phase 0 핵심 퍼즐의 모델 기반.
			LeylineGraph graph = new();
			graph.AddNode(N("gate", LeylineNodeKind.Source));
			graph.AddNode(N("relay"));
			graph.AddNode(N("workshop", LeylineNodeKind.Sink));
			graph.AddEdge(new LeylineEdge("gate", "workshop", 10f));
			graph.AddEdge(new LeylineEdge("gate", "relay", 1f));
			graph.AddEdge(new LeylineEdge("relay", "workshop", 2f));

			List<string> path = graph.FindShortestPath("gate", "workshop");

			Assert.That(path, Is.EqualTo(new List<string> { "gate", "relay", "workshop" }),
				"가중치 합 작은 우회 경로(3) 가 직선(10) 보다 짧음");
			Assert.That(graph.PathLength(path), Is.EqualTo(3f).Within(0.0001f));
		}

		[Test]
		public void FindShortestPath_Disconnected_ReturnsEmpty()
		{
			LeylineGraph graph = new();
			graph.AddNode(N("a"));
			graph.AddNode(N("b"));
			graph.AddNode(N("c"));
			graph.AddNode(N("d"));
			graph.AddEdge(new LeylineEdge("a", "b", 1f));
			graph.AddEdge(new LeylineEdge("c", "d", 1f));

			Assert.That(graph.FindShortestPath("a", "d"), Is.Empty, "분리된 두 망 — 경로 없음 = 빈 (FastFail 아닌 정상 결과)");
		}

		[Test]
		public void FindShortestPath_NoOutgoingFromSource_ReturnsEmpty()
		{
			// directional — b→a 엣지만 있고 a→b 없으면 a→b 도달 불가.
			LeylineGraph graph = new();
			graph.AddNode(N("a"));
			graph.AddNode(N("b"));
			graph.AddEdge(new LeylineEdge("b", "a", 1f));

			Assert.That(graph.FindShortestPath("a", "b"), Is.Empty, "directional — 역방향 엣지로는 도달 X");
		}

		[Test]
		public void FindShortestPath_SameNode_ReturnsSingle()
		{
			LeylineGraph graph = new();
			graph.AddNode(N("self"));

			Assert.That(graph.FindShortestPath("self", "self"), Is.EqualTo(new List<string> { "self" }));
		}

		[Test]
		public void FindShortestPath_UnknownEndpoint_ReturnsEmpty()
		{
			LeylineGraph graph = new();
			graph.AddNode(N("a"));

			Assert.That(graph.FindShortestPath("a", "ghost"), Is.Empty);
			Assert.That(graph.FindShortestPath("ghost", "a"), Is.Empty);
		}

		[Test]
		public void FindShortestPath_DeterministicAcrossRuns()
		{
			// 6 동기 「퀄리티」 — 같은 망 = 같은 경로. 같은 망 4번 빌드해 같은 path/length 검증.
			List<string> reference = null;
			float referenceLength = -1f;
			for (int run = 0; run < 4; run++)
			{
				LeylineGraph graph = new();
				graph.AddNode(N("a", LeylineNodeKind.Source));
				graph.AddNode(N("b"));
				graph.AddNode(N("c"));
				graph.AddNode(N("d", LeylineNodeKind.Sink));
				graph.AddEdge(new LeylineEdge("a", "b", 2f));
				graph.AddEdge(new LeylineEdge("b", "d", 3f));
				graph.AddEdge(new LeylineEdge("a", "c", 1f));
				graph.AddEdge(new LeylineEdge("c", "d", 5f));

				List<string> path = graph.FindShortestPath("a", "d");
				float length = graph.PathLength(path);

				if (run == 0)
				{
					reference = path;
					referenceLength = length;
				}
				else
				{
					Assert.That(path, Is.EqualTo(reference), $"run {run} 경로 = run 0 와 같음 (결정성)");
					Assert.That(length, Is.EqualTo(referenceLength).Within(0.0001f), $"run {run} 길이 = run 0 와 같음");
				}
			}
		}

		[Test]
		public void PathLength_EmptyOrSingle_ReturnsZero()
		{
			LeylineGraph graph = new();
			graph.AddNode(N("a"));

			Assert.That(graph.PathLength(new List<string>()), Is.Zero);
			Assert.That(graph.PathLength(new List<string> { "a" }), Is.Zero);
			Assert.That(graph.PathLength(null), Is.Zero);
		}

		[Test]
		public void PathLength_MultiEdgeBetweenSameNodes_PicksShorter()
		{
			// a→b 다중 엣지(redundancy 표현). PathLength 는 짧은 쪽 채택.
			LeylineGraph graph = new();
			graph.AddNode(N("a"));
			graph.AddNode(N("b"));
			graph.AddEdge(new LeylineEdge("a", "b", 7f));
			graph.AddEdge(new LeylineEdge("a", "b", 2f));

			float length = graph.PathLength(new List<string> { "a", "b" });

			Assert.That(length, Is.EqualTo(2f).Within(0.0001f), "다중 엣지 중 짧은 쪽 채택 (redundancy = 흐름은 짧은 쪽)");
		}
	}
}
