using System.Collections.Generic;
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
	/// TASK-WM-176 Phase 3 INC-1 — <see cref="RoadGraph.FloodFill"/> 멀티소스+range 유틸 전파 회귀 잠금.
	///
	/// 전기/물/마나 전파의 토대(도관 = 도로 그래프). 단일/멀티소스·range 게이트·도로단절 미도달·무한range 검증.
	/// 순수 POCO(Vector3Int 만). RoadGraphTest 패턴(new() + Assert.That).
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class RoadGraphFloodFillTest
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
		public void SingleSource_WithinRange_ReachesUpToMaxRange()
		{
			RoadGraph graph = Line(5); // (0,0)..(4,0)

			HashSet<Vector3Int> reached = graph.FloodFill(new[] { new Vector3Int(0, 0, 0) }, maxRange: 2);

			Assert.That(reached, Has.Member(new Vector3Int(0, 0, 0)), "소스(dist0)");
			Assert.That(reached, Has.Member(new Vector3Int(1, 0, 0)), "dist1");
			Assert.That(reached, Has.Member(new Vector3Int(2, 0, 0)), "dist2 = maxRange");
			Assert.That(reached, Has.No.Member(new Vector3Int(3, 0, 0)), "dist3 > maxRange 차단");
			Assert.That(reached, Has.No.Member(new Vector3Int(4, 0, 0)), "dist4 차단");
			Assert.That(reached.Count, Is.EqualTo(3));
		}

		[Test]
		public void MaxRangeZero_OnlySources()
		{
			RoadGraph graph = Line(3);

			HashSet<Vector3Int> reached = graph.FloodFill(new[] { new Vector3Int(0, 0, 0) }, maxRange: 0);

			Assert.That(reached, Is.EqualTo(new HashSet<Vector3Int> { new Vector3Int(0, 0, 0) }), "range 0 = 소스만, 전파 X");
		}

		[Test]
		public void MultiSource_Union()
		{
			RoadGraph graph = Line(5); // (0,0)..(4,0)

			HashSet<Vector3Int> reached = graph.FloodFill(new[] { new Vector3Int(0, 0, 0), new Vector3Int(4, 0, 0) }, maxRange: 1);

			// 양 끝에서 각 1홉: {0,1} ∪ {3,4}
			Assert.That(reached, Has.Member(new Vector3Int(0, 0, 0)));
			Assert.That(reached, Has.Member(new Vector3Int(1, 0, 0)));
			Assert.That(reached, Has.Member(new Vector3Int(3, 0, 0)));
			Assert.That(reached, Has.Member(new Vector3Int(4, 0, 0)));
			Assert.That(reached, Has.No.Member(new Vector3Int(2, 0, 0)), "가운데는 양쪽 1홉 모두 미도달");
			Assert.That(reached.Count, Is.EqualTo(4));
		}

		[Test]
		public void Disconnected_NotReached()
		{
			RoadGraph graph = new();
			graph.AddRoad(new Vector3Int(0, 0, 0));
			graph.AddRoad(new Vector3Int(1, 0, 0));
			graph.AddRoad(new Vector3Int(5, 5, 0)); // 분리 조각
			graph.AddRoad(new Vector3Int(6, 5, 0));

			HashSet<Vector3Int> reached = graph.FloodFill(new[] { new Vector3Int(0, 0, 0) }, maxRange: 99);

			Assert.That(reached, Has.Member(new Vector3Int(1, 0, 0)), "연결 조각 도달");
			Assert.That(reached, Has.No.Member(new Vector3Int(5, 5, 0)), "도로 단절 = 미도달(전력 끊김)");
			Assert.That(reached.Count, Is.EqualTo(2));
		}

		[Test]
		public void NegativeRange_FloodsWholeConnectedComponent()
		{
			RoadGraph graph = Line(5);

			HashSet<Vector3Int> reached = graph.FloodFill(new[] { new Vector3Int(0, 0, 0) }, maxRange: -1);

			Assert.That(reached.Count, Is.EqualTo(5), "maxRange<0 = 무한 전역 (연결 도로 전부)");
		}

		[Test]
		public void NonRoadSource_Ignored()
		{
			RoadGraph graph = Line(3);

			HashSet<Vector3Int> reached = graph.FloodFill(new[] { new Vector3Int(9, 9, 0) }, maxRange: 5);

			Assert.That(reached, Is.Empty, "도로 아닌 소스 = 무시(전파 도로 위만)");
		}
	}
}
