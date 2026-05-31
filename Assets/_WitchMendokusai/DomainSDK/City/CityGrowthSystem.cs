using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 성장/쇠퇴할 셀 1개 (어느 셀, 어느 존타입).
	public readonly struct GrowthChange
	{
		public readonly Vector3Int Cell;
		public readonly ZoneType ZoneType;

		public GrowthChange(Vector3Int cell, ZoneType zoneType)
		{
			Cell = cell;
			ZoneType = zoneType;
		}
	}

	// 하루치 성장 결정 — 적용 X (호출자가 GridData mutate + 시각 projection). materialized 리스트라 호출자
	// 가 GridData 를 mutate 해도 안전(enumeration 중 수정 아님).
	public readonly struct CityGrowthDecision
	{
		public readonly IReadOnlyList<GrowthChange> Grow;
		public readonly IReadOnlyList<GrowthChange> Shrink;

		public CityGrowthDecision(IReadOnlyList<GrowthChange> grow, IReadOnlyList<GrowthChange> shrink)
		{
			Grow = grow;
			Shrink = shrink;
		}
	}

	// 도시 성장 시뮬 — 수요(RciDemand) + 도시 상태(CityCellQuery) → 성장/쇠퇴 셀 목록. 순수(상태 0).
	// 적용(GridData 변경·시각)은 호출자(CityPaintManager) 책임 — 결정과 적용 분리.
	//   (구조 리뷰 2026-05-31: 성장 시뮬을 MonoBehaviour 에서 분리 → EditMode 검증 가능 순수 seam.)
	// 수요 > 임계 → 성장 가능 셀을 cap 만큼 / 수요 < -임계 → 건물 있는 셀을 cap 만큼 쇠퇴.
	public sealed class CityGrowthSystem
	{
		public CityGrowthDecision Decide(RciDemand demand, CityCellQuery query, float growthThreshold, int maxChangePerZone)
		{
			List<GrowthChange> grow = new();
			List<GrowthChange> shrink = new();

			EvaluateZone(ZoneType.Residential, demand.Residential, query, growthThreshold, maxChangePerZone, grow, shrink);
			EvaluateZone(ZoneType.Commercial, demand.Commercial, query, growthThreshold, maxChangePerZone, grow, shrink);
			EvaluateZone(ZoneType.Industrial, demand.Industrial, query, growthThreshold, maxChangePerZone, grow, shrink);

			return new CityGrowthDecision(grow, shrink);
		}

		private static void EvaluateZone(ZoneType zoneType, float demand, CityCellQuery query, float growthThreshold, int maxChangePerZone, List<GrowthChange> grow, List<GrowthChange> shrink)
		{
			if (demand > growthThreshold)
			{
				int count = 0;
				foreach (Vector3Int cell in query.GrowableCells(zoneType))
				{
					if (count >= maxChangePerZone)
					{
						break;
					}

					grow.Add(new GrowthChange(cell, zoneType));
					count++;
				}
			}
			else if (demand < -growthThreshold)
			{
				int count = 0;
				foreach (Vector3Int cell in query.BuiltCells(zoneType))
				{
					if (count >= maxChangePerZone)
					{
						break;
					}

					shrink.Add(new GrowthChange(cell, zoneType));
					count++;
				}
			}
		}
	}
}
