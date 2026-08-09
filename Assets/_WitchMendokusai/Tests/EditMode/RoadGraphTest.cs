using System.Collections.Generic;
using System.Linq;
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
	/// TASK-WM-164 Phase 1 step1 (tracer-bullet) — <see cref="RoadGraph"/> 삼중역할 substrate 회귀 잠금.
	///
	/// RoadGraph 는 SimCity 도로의 ① pathfinding 그래프 ② 유틸 전파 도관 ③ lot 스냅 캔버스 를 단일
	/// 셀 dict 의 derived 뷰로 제공하는 근본 자료구조 — 잘못 짜면 Phase 2/3(에이전트 길찾기·전력
	/// 전파)이 붕괴(「코드는 싸지 않다」). 그 바닥을 결정적으로 잠근다.
	///
	/// 순수 POCO — Unity 런타임/PlayMode/GUI 무관(Vector3Int 값타입만). DialogueGraphTraversalTest /
	/// NodeGraphValidatorTest 패턴 답습(new() 직접 생성 + Assert.That).
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class RoadGraphTest
	{
		[Test]
		public void AddRoad_ThenHasRoad()
		{
			RoadGraph graph = new();
			Vector3Int cell = new(2, 3, 0);

			Assert.That(graph.HasRoad(cell), Is.False, "추가 전엔 도로 없음");

			graph.AddRoad(cell);

			Assert.That(graph.HasRoad(cell), Is.True);
			Assert.That(graph.TryGetRoad(cell, out RoadCellData data), Is.True);
			Assert.That(data.Type, Is.EqualTo(RoadType.Basic));
		}

		[Test]
		public void RemoveRoad_ThenNoRoad()
		{
			RoadGraph graph = new();
			Vector3Int cell = new(0, 0, 0);
			graph.AddRoad(cell);

			graph.RemoveRoad(cell);

			Assert.That(graph.HasRoad(cell), Is.False);
		}

		[Test]
		public void AddRoad_IsIdempotent_RepaintNoThrow()
		{
			RoadGraph graph = new();
			Vector3Int cell = new(1, 1, 0);

			graph.AddRoad(cell);
			graph.AddRoad(cell); // 재페인트 — 도로는 캔버스라 멱등 덮어쓰기(경고 X)

			Assert.That(graph.RoadData.Count, Is.EqualTo(1), "재페인트해도 셀 1개");
		}

		[Test]
		public void Neighbors_ReturnsOnly4NeighborRoads()
		{
			RoadGraph graph = new();
			Vector3Int center = new(5, 5, 0);
			graph.AddRoad(center);
			graph.AddRoad(new Vector3Int(6, 5, 0)); // 동 — 이웃
			graph.AddRoad(new Vector3Int(5, 6, 0)); // 북 — 이웃
			graph.AddRoad(new Vector3Int(6, 6, 0)); // 대각 — 이웃 아님(4-이웃)
			graph.AddRoad(new Vector3Int(9, 9, 0)); // 멀리 — 이웃 아님

			List<Vector3Int> neighbors = graph.Neighbors(center).ToList();

			Assert.That(neighbors.Count, Is.EqualTo(2), "동/북만 — 대각·원거리 제외");
			Assert.That(neighbors, Has.Member(new Vector3Int(6, 5, 0)));
			Assert.That(neighbors, Has.Member(new Vector3Int(5, 6, 0)));
			Assert.That(neighbors, Has.No.Member(new Vector3Int(6, 6, 0)), "대각은 4-이웃 아님");
		}

		[Test]
		public void IsRoadAdjacent_EmptyCellNextToRoad_True()
		{
			RoadGraph graph = new();
			graph.AddRoad(new Vector3Int(3, 0, 0));

			// (4,0,0) 은 도로 아니지만 (3,0,0) 도로에 인접 → lot 후보 적격.
			Assert.That(graph.IsRoadAdjacent(new Vector3Int(4, 0, 0)), Is.True);
			Assert.That(graph.HasRoad(new Vector3Int(4, 0, 0)), Is.False, "인접 셀 자체는 도로 아님");
		}

		[Test]
		public void IsRoadAdjacent_IsolatedCell_False()
		{
			RoadGraph graph = new();
			graph.AddRoad(new Vector3Int(0, 0, 0));

			// (10,10,0) 은 어떤 도로에도 안 닿음 → lot 부적격.
			Assert.That(graph.IsRoadAdjacent(new Vector3Int(10, 10, 0)), Is.False);
		}

		[Test]
		public void AreConnected_AlongStraightPath_True()
		{
			RoadGraph graph = new();
			for (int x = 0; x < 5; x++)
			{
				graph.AddRoad(new Vector3Int(x, 0, 0));
			}

			Assert.That(graph.AreConnected(new Vector3Int(0, 0, 0), new Vector3Int(4, 0, 0)), Is.True,
				"직선 도로 양끝은 연결됨");
		}

		[Test]
		public void AreConnected_SeparateClumps_False()
		{
			RoadGraph graph = new();
			graph.AddRoad(new Vector3Int(0, 0, 0));
			graph.AddRoad(new Vector3Int(1, 0, 0)); // 덩어리 A
			graph.AddRoad(new Vector3Int(10, 10, 0));
			graph.AddRoad(new Vector3Int(11, 10, 0)); // 덩어리 B (떨어짐)

			Assert.That(graph.AreConnected(new Vector3Int(0, 0, 0), new Vector3Int(10, 10, 0)), Is.False,
				"끊긴 두 덩어리는 미연결");
		}

		[Test]
		public void CountConnectedComponents_TwoSeparateClumps_ReturnsTwo()
		{
			RoadGraph graph = new();
			graph.AddRoad(new Vector3Int(0, 0, 0));
			graph.AddRoad(new Vector3Int(1, 0, 0));
			graph.AddRoad(new Vector3Int(2, 0, 0)); // 덩어리 1 (3셀 직선)
			graph.AddRoad(new Vector3Int(10, 10, 0));
			graph.AddRoad(new Vector3Int(10, 11, 0)); // 덩어리 2 (2셀 직선)

			Assert.That(graph.CountConnectedComponents(), Is.EqualTo(2));
		}

		[Test]
		public void CountConnectedComponents_Empty_ReturnsZero()
		{
			RoadGraph graph = new();

			Assert.That(graph.CountConnectedComponents(), Is.Zero);
		}

		// ★ risk #1 잠금: Vector3Int 직렬화 round-trip. Save()→새 그래프 Load()→전 셀 동일.
		[Test]
		public void SaveLoad_RoundTrip_PreservesAllCells()
		{
			RoadGraph original = new();
			original.AddRoad(new Vector3Int(0, 0, 0));
			original.AddRoad(new Vector3Int(1, 0, 0));
			original.AddRoad(new Vector3Int(-3, 7, 0)); // 음수 좌표도

			List<KeyValuePair<Vector3Int, RoadCellData>> saved = original.Save();

			RoadGraph restored = new();
			restored.Load(saved);

			Assert.That(restored.RoadData.Count, Is.EqualTo(original.RoadData.Count));
			foreach ((Vector3Int cell, RoadCellData data) in original.RoadData)
			{
				Assert.That(restored.HasRoad(cell), Is.True, $"복원 후 {cell} 도로 유지");
				Assert.That(restored.TryGetRoad(cell, out RoadCellData restoredData), Is.True);
				Assert.That(restoredData.Type, Is.EqualTo(data.Type), $"{cell} 종류 유지");
			}
		}
	}
}
