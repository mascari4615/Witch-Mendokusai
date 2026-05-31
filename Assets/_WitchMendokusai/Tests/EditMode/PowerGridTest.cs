using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-176 Phase 3 INC-2 — <see cref="PowerGrid"/> 전력 도달 판정 회귀 잠금.
	///
	/// 전력원 인접도로 시드 → range 전파(energized 도로) → 인접 건물/존 셀 powered 판정. range 게이트·도로단절
	/// 자동 미공급 검증. 순수 POCO(new() + Assert.That).
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class PowerGridTest
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

		[Test]
		public void ComputeEnergizedRoads_ReachesWithinRange()
		{
			PowerGrid power = new();
			RoadGraph graph = Line(5); // (0,0)..(4,0)

			HashSet<Vector3Int> energized = power.ComputeEnergizedRoads(graph, new[] { new Vector3Int(0, 0, 0) }, range: 2);

			Assert.That(energized, Has.Member(new Vector3Int(2, 0, 0)), "range 내 도로 energized");
			Assert.That(energized, Has.No.Member(new Vector3Int(4, 0, 0)), "range 초과 도로 미전력");
		}

		[Test]
		public void IsCellPowered_AdjacentToEnergizedRoad_True()
		{
			PowerGrid power = new();
			RoadGraph graph = Line(5);
			HashSet<Vector3Int> energized = power.ComputeEnergizedRoads(graph, new[] { new Vector3Int(0, 0, 0) }, range: 2);

			// (1,1) 의 아래 이웃 (1,0) 이 energized → powered.
			Assert.That(power.IsCellPowered(new Vector3Int(1, 1, 0), energized), Is.True, "energized 도로 인접 = 전력");
		}

		[Test]
		public void IsCellPowered_AdjacentToUnenergizedRoad_False()
		{
			PowerGrid power = new();
			RoadGraph graph = Line(5);
			HashSet<Vector3Int> energized = power.ComputeEnergizedRoads(graph, new[] { new Vector3Int(0, 0, 0) }, range: 2);

			// (4,1) 의 아래 이웃 (4,0) 은 도로지만 range 초과 = 미전력 → unpowered.
			Assert.That(power.IsCellPowered(new Vector3Int(4, 1, 0), energized), Is.False, "range 밖 도로 인접 = 무전력(range 게이트 존중)");
		}

		[Test]
		public void IsCellPowered_NoRoadNeighbor_False()
		{
			PowerGrid power = new();
			RoadGraph graph = Line(5);
			HashSet<Vector3Int> energized = power.ComputeEnergizedRoads(graph, new[] { new Vector3Int(0, 0, 0) }, range: 2);

			Assert.That(power.IsCellPowered(new Vector3Int(9, 9, 0), energized), Is.False, "도로 이웃 없음 = 무전력");
		}

		[Test]
		public void Disconnected_NotPowered()
		{
			PowerGrid power = new();
			RoadGraph graph = new();
			graph.AddRoad(new Vector3Int(0, 0, 0));
			graph.AddRoad(new Vector3Int(1, 0, 0));
			graph.AddRoad(new Vector3Int(5, 5, 0)); // 분리된 도로 조각

			HashSet<Vector3Int> energized = power.ComputeEnergizedRoads(graph, new[] { new Vector3Int(0, 0, 0) }, range: 99);

			// (5,6) 은 분리 도로 (5,5) 인접이나 그 도로가 전력원과 단절 = 미전력.
			Assert.That(power.IsCellPowered(new Vector3Int(5, 6, 0), energized), Is.False, "도로 단절 = 전력 단절");
			// (1,1) 은 연결 도로 (1,0) 인접 = 전력.
			Assert.That(power.IsCellPowered(new Vector3Int(1, 1, 0), energized), Is.True);
		}
	}
}
