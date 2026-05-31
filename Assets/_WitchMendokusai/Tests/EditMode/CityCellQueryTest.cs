using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-166 Phase 2 INC-5a (구조 리뷰 반영) — <see cref="CityCellQuery"/> 합성 read-only 뷰 잠금.
	///
	/// 핵심: 집계/성장/쇠퇴 판정의 진실 = GridData/ZoneGrid/RoadGraph(런타임 state), 시각 캐시 아님.
	/// (buildingVisuals 를 진실로 쓰던 save/load 갈라짐 버그를 데이터 소스 통일로 수정.) 순수 POCO + new().
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class CityCellQueryTest
	{
		[Test]
		public void CountBuildingsByZone_CountsGridDataIntersectZone()
		{
			GridData grid = new();
			ZoneGrid zones = new();
			RoadGraph roads = new();

			zones.Paint(new Vector3Int(0, 0, 0), ZoneType.Residential);
			zones.Paint(new Vector3Int(1, 0, 0), ZoneType.Residential);
			zones.Paint(new Vector3Int(2, 0, 0), ZoneType.Commercial);
			grid.AddBuildingAt(new Vector3Int(0, 0, 0), new BuildingInstanceData(0));
			grid.AddBuildingAt(new Vector3Int(1, 0, 0), new BuildingInstanceData(0));
			grid.AddBuildingAt(new Vector3Int(2, 0, 0), new BuildingInstanceData(0));
			grid.AddBuildingAt(new Vector3Int(9, 9, 0), new BuildingInstanceData(0)); // 존 없는 칸의 건물

			CityCellQuery query = new(grid, zones, roads);

			Assert.That(query.CountBuildingsByZone(ZoneType.Residential), Is.EqualTo(2), "주거존 건물 2");
			Assert.That(query.CountBuildingsByZone(ZoneType.Commercial), Is.EqualTo(1), "상업존 건물 1");
			Assert.That(query.CountBuildingsByZone(ZoneType.Industrial), Is.EqualTo(0), "산업 0 (존 없는 건물 미집계)");
		}

		[Test]
		public void GrowableCells_OnlyZonedEmptyRoadAdjacent()
		{
			GridData grid = new();
			ZoneGrid zones = new();
			RoadGraph roads = new();

			roads.AddRoad(new Vector3Int(0, 0, 0));
			zones.Paint(new Vector3Int(1, 0, 0), ZoneType.Residential);  // 도로인접 + 건물 있음 → 제외
			zones.Paint(new Vector3Int(0, 1, 0), ZoneType.Residential);  // 도로인접 + 빈 → growable
			zones.Paint(new Vector3Int(5, 5, 0), ZoneType.Residential);  // 도로 안 닿음 → 제외
			grid.AddBuildingAt(new Vector3Int(1, 0, 0), new BuildingInstanceData(0));

			CityCellQuery query = new(grid, zones, roads);
			List<Vector3Int> growable = query.GrowableCells(ZoneType.Residential).ToList();

			Assert.That(growable, Has.Member(new Vector3Int(0, 1, 0)), "빈+도로인접 존셀 = growable");
			Assert.That(growable, Has.No.Member(new Vector3Int(1, 0, 0)), "건물 있는 셀 제외");
			Assert.That(growable, Has.No.Member(new Vector3Int(5, 5, 0)), "도로 안 닿는 셀 제외");
			Assert.That(growable.Count, Is.EqualTo(1));
		}

		[Test]
		public void BuiltCells_ReturnsBuildingsOfZoneFromGridData()
		{
			GridData grid = new();
			ZoneGrid zones = new();
			RoadGraph roads = new();

			zones.Paint(new Vector3Int(0, 0, 0), ZoneType.Industrial);
			zones.Paint(new Vector3Int(1, 0, 0), ZoneType.Industrial);
			grid.AddBuildingAt(new Vector3Int(0, 0, 0), new BuildingInstanceData(0)); // (1,0) 은 존만, 건물 없음

			CityCellQuery query = new(grid, zones, roads);
			List<Vector3Int> built = query.BuiltCells(ZoneType.Industrial).ToList();

			Assert.That(built, Is.EqualTo(new List<Vector3Int> { new Vector3Int(0, 0, 0) }), "건물 있는 산업셀만(데이터 진실)");
		}
	}
}
