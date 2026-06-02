using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 진단 히트맵 per-cell 스칼라 필드 — SimCity 데이터뷰의 순수 공간 신호. 상태 0(new() 후 질의만).
	// 전역 신호(RciDemand)·도달 상태(PowerGrid)를 셀별 [0,1] 로 투영 → HeatmapGradient 가 색으로 매핑.
	//
	// Phase 1 deferred "per-cell 공간 신호(desirability) 부재" 해소 = 전역 RciDemand 의 공간 투영.
	// 값의 의미만 산출(색 무관) — RoadGraph/PowerGrid/RciDemandModel 동형 순수(DomainSDK 거주, Unity state 0).
	//
	// 비전-중립 — 마법진/전기/마나 스킨 무관(순수 그래프·수학). 호출자(CityPaintManager)가 평가 대상 셀 집합과
	// 전역 수요를 주입 → 여기선 셀별 스칼라만. PowerGrid.IsCellPowered / RoadGraph.IsRoadAdjacent 재사용(중복 0).
	public sealed class CityMetricField
	{
		// 전력 커버리지 필드 — 각 셀 powered=1 / 무전력=0 (PowerGrid.IsCellPowered 재사용). 이진 데이터뷰.
		// 어디가 전력 닿고 어디가 사각인지 한눈에 — 발전소 range·도로 단절 진단.
		public Dictionary<Vector3Int, float> PowerCoverage(IEnumerable<Vector3Int> cells, PowerGrid powerGrid, HashSet<Vector3Int> energizedRoads)
		{
			Dictionary<Vector3Int, float> field = new();
			foreach (Vector3Int cell in cells)
			{
				field[cell] = powerGrid.IsCellPowered(cell, energizedRoads) ? 1f : 0f;
			}

			return field;
		}

		// 욕망도 필드 — 전역 존 수요(zoneDemand, -1..1)를 셀별 국소 인자(도로 접근·전력)로 투영한 성장 잠재 [0,1].
		//  normalizedDemand = (zoneDemand + 1) / 2  → 0(쇠퇴압) .. 1(성장압)
		//  road = IsRoadAdjacent ? 1 : 0 / power = IsCellPowered ? 1 : 0
		//  score = 가중평균(demand·road·power) → 도로·전력 닿고 수요 높은 셀이 hot.
		// CityGrowthSystem 의 "수요>임계 & 도로 인접 & (전력 게이트)" 성장 후보 판정과 동형 신호 — "어디서 자랄지".
		public Dictionary<Vector3Int, float> Desirability(
			IEnumerable<Vector3Int> cells, float zoneDemand,
			RoadGraph roadGraph, PowerGrid powerGrid, HashSet<Vector3Int> energizedRoads,
			DesirabilityWeights weights)
		{
			float normalizedDemand = Mathf.Clamp01((zoneDemand + 1f) * 0.5f);
			float weightSum = weights.DemandWeight + weights.RoadWeight + weights.PowerWeight;

			Dictionary<Vector3Int, float> field = new();
			foreach (Vector3Int cell in cells)
			{
				float road = roadGraph.IsRoadAdjacent(cell) ? 1f : 0f;
				float power = powerGrid.IsCellPowered(cell, energizedRoads) ? 1f : 0f;

				float weighted = normalizedDemand * weights.DemandWeight + road * weights.RoadWeight + power * weights.PowerWeight;
				// 가중치 합 0 = 의미 없는 필드 → 0 (0 나눗셈 회피, FastFail 아님 — 빈 가중치는 정상 질의).
				field[cell] = weightSum <= Mathf.Epsilon ? 0f : Mathf.Clamp01(weighted / weightSum);
			}

			return field;
		}
	}
}
