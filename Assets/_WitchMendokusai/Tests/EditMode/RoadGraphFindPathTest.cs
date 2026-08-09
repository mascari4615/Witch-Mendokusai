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
	/// TASK-WM-166 Phase 2 INC-1 — <see cref="RoadGraph.FindPath"/> 순수 경로질의 회귀 잠금.
	///
	/// FindPath 는 Phase 2 통근 에이전트(집↔직장 이동)·유틸 전파 경로의 단일 토대. AreConnected 의 BFS
	/// 를 predecessor 맵으로 일반화한 것 — 최단성/순서/경계(미연결·자기자신·비도로 끝점)를 결정적으로 잠근다.
	///
	/// 순수 POCO — Unity 런타임/PlayMode/GUI 무관(Vector3Int 값타입만). RoadGraphTest 패턴 답습(new() + Assert.That).
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class RoadGraphFindPathTest
	{
		// 경로 유효성 — from 시작 / to 끝 / 전부 도로 / 인접쌍 4-이웃(맨해튼 거리 1).
		private static void AssertValidPath(RoadGraph graph, List<Vector3Int> path, Vector3Int from, Vector3Int to)
		{
			Assert.That(path.Count, Is.GreaterThan(0), "유효 경로는 비어있지 않음");
			Assert.That(path[0], Is.EqualTo(from), "경로 시작 = from");
			Assert.That(path[path.Count - 1], Is.EqualTo(to), "경로 끝 = to");

			foreach (Vector3Int cell in path)
			{
				Assert.That(graph.HasRoad(cell), Is.True, $"경로 셀 {cell} 은 도로여야 함");
			}

			for (int i = 1; i < path.Count; i++)
			{
				Vector3Int delta = path[i] - path[i - 1];
				int manhattan = Mathf.Abs(delta.x) + Mathf.Abs(delta.y) + Mathf.Abs(delta.z);
				Assert.That(manhattan, Is.EqualTo(1), $"연속 셀 {path[i - 1]}→{path[i]} 는 4-이웃");
			}
		}

		[Test]
		public void StraightLine_ReturnsOrderedCellSequence()
		{
			RoadGraph graph = new();
			graph.AddRoad(new Vector3Int(0, 0, 0));
			graph.AddRoad(new Vector3Int(1, 0, 0));
			graph.AddRoad(new Vector3Int(2, 0, 0));
			graph.AddRoad(new Vector3Int(3, 0, 0));

			List<Vector3Int> path = graph.FindPath(new Vector3Int(0, 0, 0), new Vector3Int(3, 0, 0));

			List<Vector3Int> expected = new()
			{
				new Vector3Int(0, 0, 0),
				new Vector3Int(1, 0, 0),
				new Vector3Int(2, 0, 0),
				new Vector3Int(3, 0, 0),
			};
			Assert.That(path, Is.EqualTo(expected), "직선 도로 = from→to 순서 셀 시퀀스");
		}

		[Test]
		public void TwoRoutes_ReturnsShortest()
		{
			RoadGraph graph = new();
			// 짧은 길 (0,0)-(1,0)-(2,0) = 3 셀.
			graph.AddRoad(new Vector3Int(0, 0, 0));
			graph.AddRoad(new Vector3Int(1, 0, 0));
			graph.AddRoad(new Vector3Int(2, 0, 0));
			// 긴 우회 (0,0)-(0,1)-(1,1)-(2,1)-(2,0) = 5 셀 (같은 끝점 연결).
			graph.AddRoad(new Vector3Int(0, 1, 0));
			graph.AddRoad(new Vector3Int(1, 1, 0));
			graph.AddRoad(new Vector3Int(2, 1, 0));

			List<Vector3Int> path = graph.FindPath(new Vector3Int(0, 0, 0), new Vector3Int(2, 0, 0));

			AssertValidPath(graph, path, new Vector3Int(0, 0, 0), new Vector3Int(2, 0, 0));
			Assert.That(path.Count, Is.EqualTo(3), "BFS = 최단(3셀), 우회(5셀) 아님");
		}

		[Test]
		public void Disconnected_ReturnsEmpty()
		{
			RoadGraph graph = new();
			// 두 분리된 도로 조각 — 둘 다 도로지만 서로 미연결.
			graph.AddRoad(new Vector3Int(0, 0, 0));
			graph.AddRoad(new Vector3Int(1, 0, 0));
			graph.AddRoad(new Vector3Int(5, 5, 0));
			graph.AddRoad(new Vector3Int(6, 5, 0));

			List<Vector3Int> path = graph.FindPath(new Vector3Int(0, 0, 0), new Vector3Int(6, 5, 0));

			Assert.That(path, Is.Empty, "미연결 = 빈 경로 (FastFail 아닌 정상 결과)");
		}

		[Test]
		public void SameCell_ReturnsSingle()
		{
			RoadGraph graph = new();
			Vector3Int cell = new(2, 2, 0);
			graph.AddRoad(cell);

			List<Vector3Int> path = graph.FindPath(cell, cell);

			Assert.That(path, Is.EqualTo(new List<Vector3Int> { cell }), "from==to = 단일 원소");
		}

		[Test]
		public void EndpointNotRoad_ReturnsEmpty()
		{
			RoadGraph graph = new();
			graph.AddRoad(new Vector3Int(0, 0, 0));

			// to 가 비도로.
			Assert.That(graph.FindPath(new Vector3Int(0, 0, 0), new Vector3Int(1, 1, 0)), Is.Empty, "끝점 비도로 = 빈 경로");
			// from 이 비도로.
			Assert.That(graph.FindPath(new Vector3Int(9, 9, 0), new Vector3Int(0, 0, 0)), Is.Empty, "시작점 비도로 = 빈 경로");
		}
	}
}
