using UnityEngine;

namespace WitchMendokusai
{
	// 셀 점유 규칙 중앙 판정 — "이 셀에 무엇을 놓을 수 있나". road XOR (zone/building) 불변식을 한 곳에서 강제.
	//   (구조 리뷰 2026-05-31: RoadGraph 주석은 road XOR zone/building 인데 페인트 경로가 충돌 규칙을
	//    강제 안 했다 → 중앙 서비스로.) 순수 — 생성자 주입, 상태 0.
	//
	// 규칙: 도로 셀엔 존/건물 X (도로는 단독). 존/건물 셀엔 도로 X. 단 zone+building 공존은 정상
	//       (자동성장 = 존 칠한 셀에 건물). zone 재페인트는 멱등 덮어쓰기(허용).
	public sealed class CityPlacementService
	{
		private readonly GridData gridData;
		private readonly ZoneGrid zoneGrid;
		private readonly RoadGraph roadGraph;

		public CityPlacementService(GridData gridData, ZoneGrid zoneGrid, RoadGraph roadGraph)
		{
			this.gridData = gridData;
			this.zoneGrid = zoneGrid;
			this.roadGraph = roadGraph;
		}

		// 도로 가능 — 존도 건물도 없는 셀만 (road XOR zone/building).
		public bool CanPlaceRoad(Vector3Int cell)
		{
			return zoneGrid.HasZone(cell) == false && gridData.HasBuildingAt(cell) == false;
		}

		// 존 페인트 가능 — 도로 아닌 셀만 (도로 위 존 X). 빈 셀·건물 있는 셀(자동성장) 모두 OK.
		public bool CanPlaceZone(Vector3Int cell)
		{
			return roadGraph.HasRoad(cell) == false;
		}
	}
}
