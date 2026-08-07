using System;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-173 Phase 0 — <see cref="ManaFlow"/> 도착량 계산 회귀 잠금.
	///
	/// Phase 0 슬라이스 본 의도("두 노드 → 거리 따라 신선도 줄어든 채 도착, 도착량 수치 표시")
	/// 의 모델 검증. LeylineGraph + Freshness 합성 — 「짧은 직선 vs 긴 우회 = 도착량 차」 잠금.
	/// </summary>
	public sealed class ManaFlowTest
	{
		[Test]
		public void CalculateArrivalAmount_ZeroLength_FullAmount()
		{
			Assert.That(ManaFlow.CalculateArrivalAmount(100f, 0f, 0.1f), Is.EqualTo(100f).Within(0.0001f));
		}

		[Test]
		public void CalculateArrivalAmount_FullDecay_Zero()
		{
			// 길이 20 × 0.1 = 2.0 → 신선도 0 → 도착량 0.
			Assert.That(ManaFlow.CalculateArrivalAmount(100f, 20f, 0.1f), Is.Zero);
		}

		[Test]
		public void CalculateArrivalAmount_LinearMidpoint()
		{
			// 길이 5 × 0.1 → 신선도 0.5 → 100 × 0.5 = 50.
			Assert.That(ManaFlow.CalculateArrivalAmount(100f, 5f, 0.1f), Is.EqualTo(50f).Within(0.0001f));
		}

		[Test]
		public void CalculateArrivalAmount_NegativeSent_Throws()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => ManaFlow.CalculateArrivalAmount(-1f, 1f, 0.1f));
		}

		[Test]
		public void CalculateOnGraph_TwoNodeSlice_FreshnessApplied()
		{
			// Phase 0 슬라이스 본 의도 — 마계 게이트 → 공방 2노드 흐름.
			LeylineGraph graph = new();
			graph.AddNode(new LeylineNode("gate", LeylineNodeKind.Source));
			graph.AddNode(new LeylineNode("workshop", LeylineNodeKind.Sink));
			graph.AddEdge(new LeylineEdge("gate", "workshop", 4f));

			// 보낸 마력 100, 거리 4, 감쇠율 0.1 → 신선도 0.6 → 60.
			float arrived = ManaFlow.CalculateOnGraph(graph, "gate", "workshop", 100f, 0.1f);

			Assert.That(arrived, Is.EqualTo(60f).Within(0.0001f));
		}

		[Test]
		public void CalculateOnGraph_ShorterRouteArrivesMore()
		{
			// 「짧은 직선 vs 긴 우회」 = Phase 0 퍼즐 핵심. Dijkstra 가 짧은 쪽 자동 선택 → 도착량 차.
			LeylineGraph shortGraph = new();
			shortGraph.AddNode(new LeylineNode("gate", LeylineNodeKind.Source));
			shortGraph.AddNode(new LeylineNode("workshop", LeylineNodeKind.Sink));
			shortGraph.AddEdge(new LeylineEdge("gate", "workshop", 2f));

			LeylineGraph longGraph = new();
			longGraph.AddNode(new LeylineNode("gate", LeylineNodeKind.Source));
			longGraph.AddNode(new LeylineNode("workshop", LeylineNodeKind.Sink));
			longGraph.AddEdge(new LeylineEdge("gate", "workshop", 8f));

			float shortArrived = ManaFlow.CalculateOnGraph(shortGraph, "gate", "workshop", 100f, 0.1f);
			float longArrived = ManaFlow.CalculateOnGraph(longGraph, "gate", "workshop", 100f, 0.1f);

			Assert.That(shortArrived, Is.GreaterThan(longArrived), "짧은 배선이 더 많이 도착");
			Assert.That(shortArrived, Is.EqualTo(80f).Within(0.0001f));
			Assert.That(longArrived, Is.EqualTo(20f).Within(0.0001f));
		}

		[Test]
		public void CalculateOnGraph_Disconnected_ReturnsZero()
		{
			// 미연결 = 0 도착 (FastFail 아닌 「경로 없음 = 흐름 없음」 정상 결과).
			LeylineGraph graph = new();
			graph.AddNode(new LeylineNode("a", LeylineNodeKind.Source));
			graph.AddNode(new LeylineNode("b", LeylineNodeKind.Sink));

			Assert.That(ManaFlow.CalculateOnGraph(graph, "a", "b", 100f, 0.1f), Is.Zero);
		}

		[Test]
		public void CalculateOnGraph_RelayedRoute_AccumulatesLength()
		{
			// 우회 (a→relay→b, 1+2=3) 가 직선(a→b, 10) 보다 짧음 → Dijkstra 가 우회 채택.
			// 우회 누적 길이 3 × 0.1 = 0.3 감쇠 → 신선도 0.7 → 100 × 0.7 = 70.
			LeylineGraph graph = new();
			graph.AddNode(new LeylineNode("a", LeylineNodeKind.Source));
			graph.AddNode(new LeylineNode("relay", LeylineNodeKind.Relay));
			graph.AddNode(new LeylineNode("b", LeylineNodeKind.Sink));
			graph.AddEdge(new LeylineEdge("a", "b", 10f));
			graph.AddEdge(new LeylineEdge("a", "relay", 1f));
			graph.AddEdge(new LeylineEdge("relay", "b", 2f));

			float arrived = ManaFlow.CalculateOnGraph(graph, "a", "b", 100f, 0.1f);

			Assert.That(arrived, Is.EqualTo(70f).Within(0.0001f), "우회(누적 3) 가 선택, 100 × (1 - 0.3) = 70");
		}

		[Test]
		public void CalculateOnGraph_DeterministicAcrossRuns()
		{
			// 6 동기 「퀄리티」 — 같은 망 = 같은 도착량.
			float reference = -1f;
			for (int run = 0; run < 4; run++)
			{
				LeylineGraph graph = new();
				graph.AddNode(new LeylineNode("gate", LeylineNodeKind.Source));
				graph.AddNode(new LeylineNode("mid", LeylineNodeKind.Relay));
				graph.AddNode(new LeylineNode("workshop", LeylineNodeKind.Sink));
				graph.AddEdge(new LeylineEdge("gate", "mid", 2f));
				graph.AddEdge(new LeylineEdge("mid", "workshop", 3f));

				float arrived = ManaFlow.CalculateOnGraph(graph, "gate", "workshop", 100f, 0.05f);
				if (run == 0)
				{
					reference = arrived;
				}
				else
				{
					Assert.That(arrived, Is.EqualTo(reference), $"run {run} 도착량 = run 0 와 같음 (결정성 — 같은 망 = 같은 흐름)");
				}
			}
		}

		[Test]
		public void CalculateOnGraph_NullGraph_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => ManaFlow.CalculateOnGraph(null, "a", "b", 100f, 0.1f));
		}
	}
}
