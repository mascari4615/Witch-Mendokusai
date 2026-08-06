using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// 마력 흐름 계산기 — 보낸 양 × 신선도 = 도착량. 순수 함수(상태 0). Phase 0 슬라이스의 본 의도:
	//   "두 노드(마계 게이트 → 공방) 흐름 → 거리에 따라 신선도 줄어든 채 도착, 도착량 수치 표시."
	// 망 위 계산(CalculateOnGraph)은 LeylineGraph 의 최단 경로를 길이로 환산해 Freshness 에 위임.
	//
	// 경로 노화의 합성 모델: Phase 0 = 「총 경로 길이를 한 번에 Freshness 에 통과」(엣지별 누적
	// 곱 아님). 선형 감쇠라 둘이 산술적으로 거의 동치이고, exp 으로 격상 시 「엣지별 곱 vs 총길
	// 한 번」 의 의미 차이가 생김(엣지별 곱 = 매 홉마다 fresh-clamp, 한 번 = 누적 거리 함수).
	// Phase 0 = 「한 번」을 정본으로(단순 + Phase 2 시간 노화 합산과 같은 모양).
	public static class ManaFlow
	{
		// 보낸 양 sentAmount 이 길이 pathLength 의 배선을 거쳐 도착한 양. 신선도가 0 이면 0,
		// 1 이면 그대로. 음수 입력 = boundary 위반 → FastFail.
		public static float CalculateArrivalAmount(float sentAmount, float pathLength, float decayRate)
		{
			if (sentAmount < 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(sentAmount), sentAmount, "보낸 양은 음수일 수 없다");
			}

			float freshness = Freshness.DecayByDistance(pathLength, decayRate);
			return sentAmount * freshness;
		}

		// 그래프 위 source→sink 의 도착량. 최단 경로를 찾아 길이를 합쳐 도착량 계산.
		// 미연결 = 0 (FastFail 아닌 「경로 없음 = 도착 0」 정상 결과 — RoadGraph.FindPath 동형).
		// 같은 망 입력 = 같은 결과 (Dijkstra + 선형 감쇠 모두 결정적 — 6 동기 「퀄리티」 EditMode 잠금).
		public static float CalculateOnGraph(LeylineGraph graph, string sourceId, string sinkId, float sentAmount, float decayRate)
		{
			if (graph == null)
			{
				throw new ArgumentNullException(nameof(graph));
			}

			List<string> path = graph.FindShortestPath(sourceId, sinkId);
			if (path.Count == 0)
			{
				return 0f;
			}

			float pathLength = graph.PathLength(path);
			return CalculateArrivalAmount(sentAmount, pathLength, decayRate);
		}
	}
}
