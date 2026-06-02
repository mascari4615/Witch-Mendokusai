using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-176 Phase 3 INC-6 — <see cref="CityMetricField"/> per-cell 진단 필드 회귀 잠금.
	///
	/// PowerCoverage = IsCellPowered 이진 투영(range 게이트 존중). Desirability = 전역 수요의 공간 투영
	/// (도로·전력 인자 가중평균, [0,1], 가중치 0 = 0 나눗셈 회피, 수요 -1..1 정규화). 순수(new() + Assert).
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class CityMetricFieldTest
	{
		private static RoadGraph Line(int length)
		{
			RoadGraph graph = new();
			for (int i = 0; i < length; i++)
			{
				graph.AddRoad(new Vector3Int(i, 0, 0));
			}

			return graph;
		}

		// 균등 가중치(데모 디폴트) — 세 인자 동등 기여.
		private static DesirabilityWeights EqualWeights()
		{
			return new DesirabilityWeights(1f, 1f, 1f);
		}

		[Test]
		public void PowerCoverage_PoweredCellOne_UnpoweredZero()
		{
			PowerGrid power = new();
			RoadGraph graph = Line(5); // (0,0)..(4,0)
			HashSet<Vector3Int> energized = power.ComputeEnergizedRoads(graph, new[] { new Vector3Int(0, 0, 0) }, range: 2);

			CityMetricField field = new();
			Vector3Int powered = new(1, 1, 0);   // (1,0) energized 인접 → 1
			Vector3Int unpowered = new(4, 1, 0); // (4,0) range 초과 → 0
			Dictionary<Vector3Int, float> result = field.PowerCoverage(new[] { powered, unpowered }, power, energized);

			Assert.That(result[powered], Is.EqualTo(1f), "전력 셀 = 1");
			Assert.That(result[unpowered], Is.EqualTo(0f), "무전력 셀 = 0");
		}

		[Test]
		public void PowerCoverage_CoversAllInputCells()
		{
			PowerGrid power = new();
			RoadGraph graph = Line(3);
			HashSet<Vector3Int> energized = power.ComputeEnergizedRoads(graph, new[] { new Vector3Int(0, 0, 0) }, range: 99);

			CityMetricField field = new();
			Vector3Int[] cells = { new(0, 1, 0), new(1, 1, 0), new(9, 9, 0) };
			Dictionary<Vector3Int, float> result = field.PowerCoverage(cells, power, energized);

			Assert.That(result.Count, Is.EqualTo(3), "입력 셀 전부 필드에 존재");
		}

		[Test]
		public void Desirability_RoadAndPoweredHighDemand_IsHot()
		{
			PowerGrid power = new();
			RoadGraph graph = Line(3);
			HashSet<Vector3Int> energized = power.ComputeEnergizedRoads(graph, new[] { new Vector3Int(0, 0, 0) }, range: 99);

			CityMetricField field = new();
			Vector3Int cell = new(1, 1, 0); // (1,0) 도로 인접 + energized
			Dictionary<Vector3Int, float> result = field.Desirability(
				new[] { cell }, zoneDemand: 1f, graph, power, energized, EqualWeights());

			// demand=1(정규화 1) + road=1 + power=1 → 가중평균 1.
			Assert.That(result[cell], Is.EqualTo(1f).Within(0.0001f), "수요 최대+도로+전력 = 최고 hot");
		}

		[Test]
		public void Desirability_NoRoadNoPowerLowDemand_IsCold()
		{
			PowerGrid power = new();
			RoadGraph graph = Line(3);
			HashSet<Vector3Int> energized = power.ComputeEnergizedRoads(graph, new[] { new Vector3Int(0, 0, 0) }, range: 99);

			CityMetricField field = new();
			Vector3Int cell = new(50, 50, 0); // 도로 멀고 전력 없음
			Dictionary<Vector3Int, float> result = field.Desirability(
				new[] { cell }, zoneDemand: -1f, graph, power, energized, EqualWeights());

			// demand=-1(정규화 0) + road=0 + power=0 → 0.
			Assert.That(result[cell], Is.EqualTo(0f).Within(0.0001f), "수요 최저+무도로+무전력 = 최저 cold");
		}

		[Test]
		public void Desirability_RoadAccessRaisesScore()
		{
			PowerGrid power = new();
			RoadGraph graph = Line(3);
			HashSet<Vector3Int> energized = new(); // 전력 없음 — 도로 인자만 분리 검증

			CityMetricField field = new();
			Vector3Int near = new(1, 1, 0);   // 도로 인접
			Vector3Int far = new(50, 50, 0);  // 도로 없음
			Dictionary<Vector3Int, float> result = field.Desirability(
				new[] { near, far }, zoneDemand: 0f, graph, power, energized, EqualWeights());

			Assert.That(result[near], Is.GreaterThan(result[far]), "같은 수요면 도로 인접 셀이 더 hot");
		}

		[Test]
		public void Desirability_ZeroWeights_NoDivideByZero()
		{
			PowerGrid power = new();
			RoadGraph graph = Line(3);
			HashSet<Vector3Int> energized = power.ComputeEnergizedRoads(graph, new[] { new Vector3Int(0, 0, 0) }, range: 99);

			CityMetricField field = new();
			Vector3Int cell = new(1, 1, 0);
			Dictionary<Vector3Int, float> result = field.Desirability(
				new[] { cell }, zoneDemand: 1f, graph, power, energized, new DesirabilityWeights(0f, 0f, 0f));

			Assert.That(result[cell], Is.EqualTo(0f), "가중치 0 합 = 0 (NaN/예외 없음)");
		}

		[Test]
		public void Desirability_DemandNormalization_MapsMinusOneToZeroPlusOneToFull()
		{
			PowerGrid power = new();
			RoadGraph graph = Line(3);
			HashSet<Vector3Int> energized = new();

			CityMetricField field = new();
			Vector3Int cell = new(50, 50, 0); // 도로·전력 인자 0 → 수요 인자만 노출
			DesirabilityWeights demandOnly = new(1f, 0f, 0f);

			float low = field.Desirability(new[] { cell }, -1f, graph, power, energized, demandOnly)[cell];
			float high = field.Desirability(new[] { cell }, 1f, graph, power, energized, demandOnly)[cell];

			Assert.That(low, Is.EqualTo(0f).Within(0.0001f), "수요 -1 → 정규화 0");
			Assert.That(high, Is.EqualTo(1f).Within(0.0001f), "수요 +1 → 정규화 1");
		}
	}
}
