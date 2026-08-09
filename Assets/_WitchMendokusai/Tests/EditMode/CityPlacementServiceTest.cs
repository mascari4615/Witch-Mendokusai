using NUnit.Framework;
using UnityEngine;
// ★ 좌표는 판정 쪽 (TASK-WM-214).
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2 = WitchMendokusai.Numerics.Vector2;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-166 Phase 2 INC-5b (구조 리뷰 반영) — <see cref="CityPlacementService"/> 셀 점유 규칙 잠금.
	///
	/// road XOR (zone/building) 불변식 중앙 강제. 도로↔존/건물 상호배제, zone+building 공존 허용. 순수 + new().
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class CityPlacementServiceTest
	{
		private static CityPlacementService Service(GridData grid, ZoneGrid zones, RoadGraph roads)
		{
			return new CityPlacementService(grid, zones, roads);
		}

		[Test]
		public void CanPlaceRoad_EmptyCell_True()
		{
			Assert.That(Service(new GridData(), new ZoneGrid(), new RoadGraph()).CanPlaceRoad(new Vector3Int(0, 0, 0)), Is.True);
		}

		[Test]
		public void CanPlaceRoad_ZonedCell_False()
		{
			ZoneGrid zones = new();
			zones.Paint(new Vector3Int(0, 0, 0), ZoneType.Residential);

			Assert.That(Service(new GridData(), zones, new RoadGraph()).CanPlaceRoad(new Vector3Int(0, 0, 0)), Is.False, "존 셀엔 도로 X");
		}

		[Test]
		public void CanPlaceRoad_BuiltCell_False()
		{
			GridData grid = new();
			grid.AddBuildingAt(new Vector3Int(0, 0, 0), new BuildingInstanceData(0));

			Assert.That(Service(grid, new ZoneGrid(), new RoadGraph()).CanPlaceRoad(new Vector3Int(0, 0, 0)), Is.False, "건물 셀엔 도로 X");
		}

		[Test]
		public void CanPlaceZone_NonRoadCell_True()
		{
			Assert.That(Service(new GridData(), new ZoneGrid(), new RoadGraph()).CanPlaceZone(new Vector3Int(0, 0, 0)), Is.True);
		}

		[Test]
		public void CanPlaceZone_RoadCell_False()
		{
			RoadGraph roads = new();
			roads.AddRoad(new Vector3Int(0, 0, 0));

			Assert.That(Service(new GridData(), new ZoneGrid(), roads).CanPlaceZone(new Vector3Int(0, 0, 0)), Is.False, "도로 셀엔 존 X");
		}
	}
}
