using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using VContainer;

namespace WitchMendokusai
{
	// CityPaintManager 의 덧판과 지표 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 CityPaintManager.cs 를 본다.
	public partial class CityPaintManager : MonoBehaviour
	{
		// INC-6 진단 히트맵 — 데이터뷰 오버레이. 순수 필드(CityMetricField) + 색(HeatmapGradient).
		// 오버레이 타일 = 칠해진 셀 위 투영(렌더 캐시) — 데이터 진실은 grid/registry, 토글 끄면 전부 제거.
		private enum OverlayKind
		{
			Off,          // 데이터뷰 끔 (평소 존/도로 색)
			PowerCoverage, // 전력 닿는 곳 hot
			Desirability,  // 선택 존 성장 잠재(수요·도로·전력 투영) hot
		}
		private OverlayKind overlayKind = OverlayKind.Off;
		private readonly Dictionary<Vector3Int, GameObject> overlayVisuals = new();
		private readonly CityMetricField metricField = new();
		private readonly HeatmapGradient heatmap = new();

		// INC-6 — 진단 히트맵 재계산 + 그리기. 평가 대상 = 칠해진 발자취(존 ∪ 도로). 끄면 전부 제거.
		// 순수부(CityMetricField 값 → HeatmapGradient 색)는 EditMode 검증 — 여기선 수집·투영만.
		private void RefreshOverlay()
		{
			ClearOverlay();

			if (overlayKind == OverlayKind.Off)
				return;
			if (stageManager.CurStage is WorldStage worldStage == false)
				return;

			HashSet<Vector3Int> cells = new(worldStage.ZoneGrid.ZoneData.Keys);
			foreach (Vector3Int road in worldStage.RoadGraph.RoadData.Keys)
				cells.Add(road);
			if (cells.Count == 0)
				return;

			HashSet<Vector3Int> energizedRoads = ComputeEnergizedRoads(worldStage);
			Dictionary<Vector3Int, float> field;

			if (overlayKind == OverlayKind.PowerCoverage)
			{
				field = metricField.PowerCoverage(cells, powerGrid, energizedRoads);
			}
			else
			{
				// 선택 존 전역 수요를 공간 투영 — "이 존을 어디에 깔면 자랄까" 데이터뷰.
				CityCellQuery query = new(worldStage.GridData, worldStage.ZoneGrid, worldStage.RoadGraph);
				RciDemandCoefficients coefficients = new(residentsPerJob, shopsPerResident, industryPerResident, immigrationBaseline, exportBaseline, demandGain);
				RciDemand demand = demandModel.Evaluate(
					query.CountBuildingsByZone(ZoneType.Residential),
					query.CountBuildingsByZone(ZoneType.Commercial),
					query.CountBuildingsByZone(ZoneType.Industrial),
					coefficients);

				DesirabilityWeights weights = new(desirabilityDemandWeight, desirabilityRoadWeight, desirabilityPowerWeight);
				field = metricField.Desirability(cells, ZoneDemand(demand, SelectedZoneType), worldStage.RoadGraph, powerGrid, energizedRoads, weights);
			}

			foreach (KeyValuePair<Vector3Int, float> entry in field)
				SetOverlayVisual(entry.Key, heatmap.Evaluate(entry.Value).ToUnity());
		}

		// 선택 존타입의 전역 수요값 추출 (욕망도 공간 투영의 base).
		private static float ZoneDemand(RciDemand demand, ZoneType type)
		{
			switch (type)
			{
				case ZoneType.Residential: return demand.Residential;
				case ZoneType.Commercial: return demand.Commercial;
				case ZoneType.Industrial: return demand.Industrial;
				default: return 0f;
			}
		}

		// 오버레이 타일 — 존 타일 위에 뜬 납작한 히트맵 색판(데이터뷰 = 평소 색 위 진단 오버레이).
		private void SetOverlayVisual(Vector3Int cell, Color color)
		{
			if (overlayVisuals.TryGetValue(cell, out GameObject visual) == false)
			{
				visual = CreateCellCube(cell, overlayTileHeight);
				visual.name = $"Overlay_{cell.x}_{cell.y}";
				overlayVisuals[cell] = visual;
			}

			visual.GetComponent<Renderer>().sharedMaterial = GetMaterial(color);
		}

		private void ClearOverlayVisual(Vector3Int cell)
		{
			if (overlayVisuals.TryGetValue(cell, out GameObject visual))
			{
				overlayVisuals.Remove(cell);
				Destroy(visual);
			}
		}

		private void ClearOverlay()
		{
			foreach (GameObject visual in overlayVisuals.Values)
				Destroy(visual);
			overlayVisuals.Clear();
		}

		// INC-6 — 진단 히트맵 순환 (Off → 전력 커버리지 → 욕망도 → Off). 데이터뷰 토글(수동 트리거).
		[ContextMenu("Cycle Diagnostic Overlay (Off/Power/Desire)")]
		private void CycleOverlay_Editor()
		{
			overlayKind = (OverlayKind)(((int)overlayKind + 1) % 3);
			RefreshOverlay();
			Debug.Log($"[City/Overlay] 데이터뷰 = {overlayKind}");
		}
	}
}
