using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-166 Phase 2 INC-5a (구조 리뷰 반영) — <see cref="CityGrowthSystem"/> 성장 결정 잠금.
	///
	/// 성장 시뮬을 CityPaintManager MonoBehaviour 에서 분리한 순수 seam — 수요+도시상태(CityCellQuery) →
	/// 성장/쇠퇴 셀 결정(적용 X). 임계/cap/존타입 격리 검증. new() + Assert.That.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class CityGrowthSystemTest
	{
		[Test]
		public void PositiveDemand_GrowsUpToCapInGrowableCells()
		{
			GridData grid = new();
			ZoneGrid zones = new();
			RoadGraph roads = new();
			roads.AddRoad(new Vector3Int(0, 0, 0));
			zones.Paint(new Vector3Int(1, 0, 0), ZoneType.Residential);
			zones.Paint(new Vector3Int(-1, 0, 0), ZoneType.Residential);
			zones.Paint(new Vector3Int(0, 1, 0), ZoneType.Residential);
			CityCellQuery query = new(grid, zones, roads);
			CityGrowthSystem system = new();

			CityGrowthDecision decision = system.Decide(new RciDemand(0.5f, 0f, 0f), query, 0.2f, 2);

			Assert.That(decision.Grow.Count, Is.EqualTo(2), "growable 3 중 cap 2 만");
			Assert.That(decision.Grow.All(change => change.ZoneType == ZoneType.Residential), Is.True);
			Assert.That(decision.Shrink, Is.Empty);
		}

		[Test]
		public void NegativeDemand_ShrinksBuiltCells()
		{
			GridData grid = new();
			ZoneGrid zones = new();
			RoadGraph roads = new();
			zones.Paint(new Vector3Int(0, 0, 0), ZoneType.Commercial);
			zones.Paint(new Vector3Int(1, 0, 0), ZoneType.Commercial);
			grid.AddBuildingAt(new Vector3Int(0, 0, 0), new BuildingInstanceData(0));
			grid.AddBuildingAt(new Vector3Int(1, 0, 0), new BuildingInstanceData(0));
			CityCellQuery query = new(grid, zones, roads);
			CityGrowthSystem system = new();

			CityGrowthDecision decision = system.Decide(new RciDemand(0f, -0.5f, 0f), query, 0.2f, 5);

			Assert.That(decision.Shrink.Count, Is.EqualTo(2), "상업 건물 2 쇠퇴");
			Assert.That(decision.Shrink.All(change => change.ZoneType == ZoneType.Commercial), Is.True);
			Assert.That(decision.Grow, Is.Empty);
		}

		[Test]
		public void DemandWithinThreshold_NoChange()
		{
			GridData grid = new();
			ZoneGrid zones = new();
			RoadGraph roads = new();
			roads.AddRoad(new Vector3Int(0, 0, 0));
			zones.Paint(new Vector3Int(1, 0, 0), ZoneType.Residential);
			CityCellQuery query = new(grid, zones, roads);
			CityGrowthSystem system = new();

			// |수요| 가 임계(0.2) 이하 → 변화 0 (> / < 엄격 비교).
			CityGrowthDecision decision = system.Decide(new RciDemand(0.2f, -0.1f, 0.15f), query, 0.2f, 5);

			Assert.That(decision.Grow, Is.Empty, "임계 이하 성장 X");
			Assert.That(decision.Shrink, Is.Empty, "임계 이하 쇠퇴 X");
		}

		[Test]
		public void OnlyDemandedZoneTypeGrows()
		{
			GridData grid = new();
			ZoneGrid zones = new();
			RoadGraph roads = new();
			roads.AddRoad(new Vector3Int(0, 0, 0));
			zones.Paint(new Vector3Int(1, 0, 0), ZoneType.Residential);
			zones.Paint(new Vector3Int(0, 1, 0), ZoneType.Commercial);
			CityCellQuery query = new(grid, zones, roads);
			CityGrowthSystem system = new();

			// 주거만 양수 수요 → 주거만 성장.
			CityGrowthDecision decision = system.Decide(new RciDemand(0.5f, 0f, 0f), query, 0.2f, 5);

			Assert.That(decision.Grow.Count, Is.EqualTo(1));
			Assert.That(decision.Grow[0].ZoneType, Is.EqualTo(ZoneType.Residential), "수요 있는 주거만");
		}
	}
}
