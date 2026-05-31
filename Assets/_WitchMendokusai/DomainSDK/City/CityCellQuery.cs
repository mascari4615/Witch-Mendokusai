using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 도시 3레이어(GridData 건물 / ZoneGrid 존 / RoadGraph 도로) 합성 read-only 뷰 — "이 셀에 뭐가 있나",
	// "어디가 성장 가능한가", "존타입별 건물 수". 3-grid join 을 한 곳에 중앙화(매 셀 손합성 제거).
	//
	// ★ 진실 소스 = GridData/ZoneGrid/RoadGraph (런타임 state) — 시각 캐시 아님.
	//    (구조 리뷰 2026-05-31: CityPaintManager.buildingVisuals(렌더 캐시)를 집계 진실로 쓰면 save/load 후
	//     GridData 와 갈라진다 → 집계/성장/쇠퇴 판정을 전부 데이터 소스로 통일.)
	//
	// 순수 — 생성자 주입(grids), 상태 0(읽기 뷰만). DomainSDK 거주.
	public sealed class CityCellQuery
	{
		private readonly GridData gridData;
		private readonly ZoneGrid zoneGrid;
		private readonly RoadGraph roadGraph;

		public CityCellQuery(GridData gridData, ZoneGrid zoneGrid, RoadGraph roadGraph)
		{
			this.gridData = gridData;
			this.zoneGrid = zoneGrid;
			this.roadGraph = roadGraph;
		}

		// 해당 존타입 건물 수 — GridData(진실) ∩ ZoneGrid. RciDemand occupancy 입력원.
		public int CountBuildingsByZone(ZoneType zoneType)
		{
			int count = 0;
			foreach (Vector3Int cell in gridData.BuildingData.Keys)
			{
				if (zoneGrid.GetZone(cell) == zoneType)
				{
					count++;
				}
			}

			return count;
		}

		// 성장 가능 셀 — 해당 존 칠해짐 + 건물 없음 + 도로 인접 (최소 lot 규칙). 자동성장 후보.
		public IEnumerable<Vector3Int> GrowableCells(ZoneType zoneType)
		{
			foreach (KeyValuePair<Vector3Int, ZoneCellData> entry in zoneGrid.ZoneData)
			{
				if (entry.Value.Type == zoneType
					&& gridData.HasBuildingAt(entry.Key) == false
					&& roadGraph.IsRoadAdjacent(entry.Key))
				{
					yield return entry.Key;
				}
			}
		}

		// 쇠퇴 후보 — 해당 존타입의 건물 있는 셀 (GridData 진실, 시각 캐시 아님).
		public IEnumerable<Vector3Int> BuiltCells(ZoneType zoneType)
		{
			foreach (Vector3Int cell in gridData.BuildingData.Keys)
			{
				if (zoneGrid.GetZone(cell) == zoneType)
				{
					yield return cell;
				}
			}
		}

		// Phase 3 INC-4 — 전력 받는 성장 후보 = GrowableCells ∩ (전력 흐르는 도로 인접). 전력 게이트.
		// PowerGrid.IsCellPowered 로 energized 인접 판정(중복 X). GrowableCells 비파괴 확장(Phase2 무회귀).
		public IEnumerable<Vector3Int> PoweredGrowableCells(ZoneType zoneType, PowerGrid powerGrid, HashSet<Vector3Int> energizedRoads)
		{
			foreach (Vector3Int cell in GrowableCells(zoneType))
			{
				if (powerGrid.IsCellPowered(cell, energizedRoads))
				{
					yield return cell;
				}
			}
		}
	}
}
