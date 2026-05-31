using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// GlassBox 전력(유틸) 도달 — 순수 함수(상태 0, new() 후 질의만). RciDemandModel/CityGrowthSystem 동형.
	//
	//  ComputeEnergizedRoads = 전력원의 인접도로 시드에서 도로 따라 range 홉 전파(RoadGraph.FloodFill 위임)
	//   → 전력 흐르는 도로 셀 집합. IsCellPowered = 건물/존 셀이 그 energized 도로에 인접하면 전력 공급.
	//
	// 비전-중립 — 전기/물/마나 무관(순수 그래프 도달). 전력원→인접도로 변환(TryRoadNeighbor 류)은 호출자
	// (CityPaintManager) 책임 — 통근 시민 집→인접도로 진입과 동형. 여기선 road 시드 받아 전파·인접질의만.
	public sealed class PowerGrid
	{
		// 4-이웃 (RoadGraph.NEIGHBOR_OFFSETS 와 동일 평면 z=0 — IsCellPowered 인접질의용).
		private static readonly Vector3Int[] NEIGHBOR_OFFSETS =
		{
			new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0),
		};

		// 전력 흐르는 도로 셀 집합 — roadSources(전력원 인접도로 진입점들)에서 range 홉 전파.
		public HashSet<Vector3Int> ComputeEnergizedRoads(RoadGraph roadGraph, IEnumerable<Vector3Int> roadSources, int range)
		{
			return roadGraph.FloodFill(roadSources, range);
		}

		// 셀(건물/존)이 전력 받나 = 4-이웃 도로 중 하나라도 energized (IsRoadAdjacent 동형, energized 한정).
		public bool IsCellPowered(Vector3Int cell, HashSet<Vector3Int> energizedRoads)
		{
			for (int i = 0; i < NEIGHBOR_OFFSETS.Length; i++)
			{
				if (energizedRoads.Contains(cell + NEIGHBOR_OFFSETS[i]))
				{
					return true;
				}
			}

			return false;
		}
	}
}
