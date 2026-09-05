using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
// ★ 좌표는 판정 쪽 (TASK-WM-214).
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-194 — <see cref="TowerDefenseMapGenerator"/> 절차적 개척판 생성 불변식 잠금.
	///
	/// 이 생성기의 값어치는 "랜덤해 보인다"가 아니라 *항상 플레이 가능한 판*이라는 보장에 있다.
	/// 그래서 테스트는 모양이 아니라 불변식을 건다: 결정론 / 스폰·노드에서 코어까지 도달 가능 /
	/// 노드가 암반 위에 없음 / 노드 간 최소 간격 / 코어 이격 / 수입 배수가 거리에 비례.
	/// 순수 정적 함수라 MonoBehaviour·씬 0 (EditMode 로 전량 검증 가능).
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class TowerDefenseMapGeneratorTest
	{
		private static TowerDefenseMapParameters Params(int seed)
		{
			TowerDefenseMapParameters parameters = TowerDefenseMapParameters.Default;
			parameters.Seed = seed;
			return parameters;
		}

		// 4방 이동으로 코어까지 실제로 걸어갈 수 있는지 — 생성기 내부 구현과 독립된 별도 BFS.
		private static bool CanWalkToCore(TowerDefenseMapLayout layout, Vector2Int start)
		{
			if (layout.IsBlocked(start))
				return false;

			Vector2Int[] offsets =
			{
				new Vector2Int(1, 0),
				new Vector2Int(-1, 0),
				new Vector2Int(0, 1),
				new Vector2Int(0, -1),
			};

			HashSet<Vector2Int> visited = new HashSet<Vector2Int> { start };
			Queue<Vector2Int> frontier = new Queue<Vector2Int>();
			frontier.Enqueue(start);

			while (frontier.Count > 0)
			{
				Vector2Int current = frontier.Dequeue();
				if (current == layout.CoreCell)
					return true;

				for (int i = 0; i < offsets.Length; i++)
				{
					Vector2Int next = current + offsets[i];
					if (layout.IsBlocked(next) || visited.Contains(next))
						continue;

					visited.Add(next);
					frontier.Enqueue(next);
				}
			}

			return false;
		}

		[Test]
		public void SameSeed_ProducesIdenticalLayout()
		{
			TowerDefenseMapLayout first = TowerDefenseMapGenerator.Generate(Params(1234));
			TowerDefenseMapLayout second = TowerDefenseMapGenerator.Generate(Params(1234));

			Assert.That(second.CoreCell, Is.EqualTo(first.CoreCell));
			Assert.That(second.EnemySpawnPoints, Is.EqualTo(first.EnemySpawnPoints));
			Assert.That(second.ObstacleCells, Is.EqualTo(first.ObstacleCells));
			Assert.That(second.ResourceNodes.Count, Is.EqualTo(first.ResourceNodes.Count));

			for (int i = 0; i < first.ResourceNodes.Count; i++)
			{
				Assert.That(second.ResourceNodes[i].Cell, Is.EqualTo(first.ResourceNodes[i].Cell));
				Assert.That(second.ResourceNodes[i].Tier, Is.EqualTo(first.ResourceNodes[i].Tier));
			}
		}

		[Test]
		public void DifferentSeed_ProducesDifferentLayout()
		{
			TowerDefenseMapLayout first = TowerDefenseMapGenerator.Generate(Params(1));
			TowerDefenseMapLayout second = TowerDefenseMapGenerator.Generate(Params(2));

			bool sameObstacles = first.ObstacleCells.Count == second.ObstacleCells.Count;
			if (sameObstacles)
			{
				for (int i = 0; i < first.ObstacleCells.Count; i++)
				{
					if (first.ObstacleCells[i] == second.ObstacleCells[i])
						continue;
					sameObstacles = false;
					break;
				}
			}

			Assert.That(sameObstacles, Is.False, "시드가 달라도 같은 판이 나오면 절차 생성이 아니다.");
		}

		[Test]
		public void EverySpawnPoint_CanReachCore()
		{
			for (int seed = 0; seed < SEED_SWEEP; seed++)
			{
				TowerDefenseMapLayout layout = TowerDefenseMapGenerator.Generate(Params(seed));

				for (int i = 0; i < layout.EnemySpawnPoints.Count; i++)
				{
					Vector2Int spawnCell = layout.WorldToCell(layout.EnemySpawnPoints[i]);
					Assert.That(CanWalkToCore(layout, spawnCell), Is.True,
						$"seed={seed} 스폰 {i} 이 코어까지 막힘 — 적이 영원히 못 온다.");
				}
			}
		}

		[Test]
		public void EveryResourceNode_IsWalkableAndReachable()
		{
			for (int seed = 0; seed < SEED_SWEEP; seed++)
			{
				TowerDefenseMapLayout layout = TowerDefenseMapGenerator.Generate(Params(seed));

				for (int i = 0; i < layout.ResourceNodes.Count; i++)
				{
					Vector2Int nodeCell = layout.ResourceNodes[i].Cell;
					Assert.That(layout.IsBlocked(nodeCell), Is.False, $"seed={seed} 노드 {i} 가 암반 위에 박혔다.");
					Assert.That(CanWalkToCore(layout, nodeCell), Is.True, $"seed={seed} 노드 {i} 가 고립됐다.");
				}
			}
		}

		[Test]
		public void ResourceNodes_RespectSpacingAndCoreDistance()
		{
			TowerDefenseMapParameters parameters = Params(77);

			for (int seed = 0; seed < SEED_SWEEP; seed++)
			{
				parameters.Seed = seed;
				TowerDefenseMapLayout layout = TowerDefenseMapGenerator.Generate(parameters);

				for (int i = 0; i < layout.ResourceNodes.Count; i++)
				{
					Vector2Int cell = layout.ResourceNodes[i].Cell;
					float coreDistance = Vector2Int.Distance(cell, layout.CoreCell);
					Assert.That(coreDistance, Is.GreaterThanOrEqualTo(parameters.NodeMinCoreDistance - EPSILON),
						$"seed={seed} 노드가 코어에 붙었다 — 개척 긴장이 사라진다.");

					for (int j = i + 1; j < layout.ResourceNodes.Count; j++)
					{
						float spacing = Vector2Int.Distance(cell, layout.ResourceNodes[j].Cell);
						Assert.That(spacing, Is.GreaterThanOrEqualTo(parameters.NodeMinSpacing - EPSILON),
							$"seed={seed} 노드 {i}·{j} 가 뭉쳤다 — 방어선 하나로 퉁쳐진다.");
					}
				}
			}
		}

		[Test]
		public void ResourceNodes_KeepEdgeMargin()
		{
			TowerDefenseMapParameters parameters = Params(0);

			for (int seed = 0; seed < SEED_SWEEP; seed++)
			{
				parameters.Seed = seed;
				TowerDefenseMapLayout layout = TowerDefenseMapGenerator.Generate(parameters);

				for (int i = 0; i < layout.ResourceNodes.Count; i++)
				{
					Vector2Int cell = layout.ResourceNodes[i].Cell;
					Assert.That(cell.x, Is.GreaterThanOrEqualTo(parameters.NodeEdgeMargin), $"seed={seed} 노드가 판 서쪽 구석에 붙었다.");
					Assert.That(cell.y, Is.GreaterThanOrEqualTo(parameters.NodeEdgeMargin), $"seed={seed} 노드가 판 남쪽 구석에 붙었다.");
					Assert.That(layout.Width - 1 - cell.x, Is.GreaterThanOrEqualTo(parameters.NodeEdgeMargin), $"seed={seed} 노드가 판 동쪽 구석에 붙었다.");
					Assert.That(layout.Length - 1 - cell.y, Is.GreaterThanOrEqualTo(parameters.NodeEdgeMargin), $"seed={seed} 노드가 판 북쪽 구석에 붙었다.");
				}
			}
		}

		[Test]
		public void ResourceNodes_SpreadAroundCore_WhenBoardIsOpen()
		{
			// 암반 0 + 노드 2개 = 각도 조건을 반드시 만족시킬 수 있는 판 → 완화 2패스가 끼어들 여지 없음.
			TowerDefenseMapParameters parameters = TowerDefenseMapParameters.Default;
			parameters.RockSiteCount = 0;
			parameters.ResourceNodeCount = 2;
			parameters.NodeAngularSpread = 90f;

			for (int seed = 0; seed < SEED_SWEEP; seed++)
			{
				parameters.Seed = seed;
				TowerDefenseMapLayout layout = TowerDefenseMapGenerator.Generate(parameters);
				Assert.That(layout.ResourceNodes.Count, Is.EqualTo(2));

				Vector2 first = (layout.ResourceNodes[0].Cell - layout.CoreCell).ToUnity();
				Vector2 second = (layout.ResourceNodes[1].Cell - layout.CoreCell).ToUnity();
				Assert.That(Vector2.Angle(first, second), Is.GreaterThanOrEqualTo(parameters.NodeAngularSpread - EPSILON),
					$"seed={seed} 노드가 코어 한쪽으로 쏠렸다 — 넓힐 방향 선택이 사라진다.");
			}
		}

		[Test]
		public void RequestedNodeCount_IsSatisfied()
		{
			for (int seed = 0; seed < SEED_SWEEP; seed++)
			{
				TowerDefenseMapLayout layout = TowerDefenseMapGenerator.Generate(Params(seed));
				Assert.That(layout.ResourceNodes.Count, Is.EqualTo(TowerDefenseMapParameters.Default.ResourceNodeCount),
					$"seed={seed} 기본 파라미터로 요청한 노드 수를 못 채웠다.");
			}
		}

		[Test]
		public void FartherNode_PaysMore()
		{
			for (int seed = 0; seed < SEED_SWEEP; seed++)
			{
				TowerDefenseMapLayout layout = TowerDefenseMapGenerator.Generate(Params(seed));

				for (int i = 0; i < layout.ResourceNodes.Count; i++)
				{
					for (int j = 0; j < layout.ResourceNodes.Count; j++)
					{
						float distanceI = Vector2Int.Distance(layout.ResourceNodes[i].Cell, layout.CoreCell);
						float distanceJ = Vector2Int.Distance(layout.ResourceNodes[j].Cell, layout.CoreCell);
						if (distanceI <= distanceJ)
							continue;

						Assert.That(layout.ResourceNodes[i].IncomeMultiplier,
							Is.GreaterThanOrEqualTo(layout.ResourceNodes[j].IncomeMultiplier - EPSILON),
							$"seed={seed} 먼 노드가 더 벌지 않으면 나갈 이유가 없다.");
					}
				}
			}
		}

		[Test]
		public void CoreSurroundings_AreAlwaysClear()
		{
			TowerDefenseMapParameters parameters = Params(9);

			for (int seed = 0; seed < SEED_SWEEP; seed++)
			{
				parameters.Seed = seed;
				TowerDefenseMapLayout layout = TowerDefenseMapGenerator.Generate(parameters);

				int range = Mathf.CeilToInt(parameters.CoreClearRadius);
				for (int dx = -range; dx <= range; dx++)
				{
					for (int dz = -range; dz <= range; dz++)
					{
						Vector2Int cell = layout.CoreCell + new Vector2Int(dx, dz);
						if (layout.IsInside(cell) == false)
							continue;
						if (Vector2Int.Distance(cell, layout.CoreCell) > parameters.CoreClearRadius)
							continue;

						Assert.That(layout.IsBlocked(cell), Is.False, $"seed={seed} 코어 주변에 암반이 남았다.");
					}
				}
			}
		}

		[Test]
		public void AllGeneratedCells_StayInsideBoard()
		{
			for (int seed = 0; seed < SEED_SWEEP; seed++)
			{
				TowerDefenseMapLayout layout = TowerDefenseMapGenerator.Generate(Params(seed));

				Assert.That(layout.IsInside(layout.CoreCell), Is.True);

				for (int i = 0; i < layout.EnemySpawnPoints.Count; i++)
					Assert.That(layout.IsInside(layout.WorldToCell(layout.EnemySpawnPoints[i])), Is.True);

				for (int i = 0; i < layout.ResourceNodes.Count; i++)
					Assert.That(layout.IsInside(layout.ResourceNodes[i].Cell), Is.True);

				for (int i = 0; i < layout.ObstacleCells.Count; i++)
					Assert.That(layout.IsInside(layout.ObstacleCells[i]), Is.True);
			}
		}

		[Test]
		public void CellAndWorldConversion_RoundTrips()
		{
			TowerDefenseMapLayout layout = TowerDefenseMapGenerator.Generate(Params(5));

			for (int x = 0; x < layout.Width; x += 7)
			{
				for (int z = 0; z < layout.Length; z += 7)
				{
					Vector2Int cell = new Vector2Int(x, z);
					Assert.That(layout.WorldToCell(layout.CellToWorld(cell)), Is.EqualTo(cell));
				}
			}
		}

		[Test]
		public void DegenerateParameters_StillProduceUsableBoard()
		{
			TowerDefenseMapParameters broken = new TowerDefenseMapParameters
			{
				Seed = 3,
				Width = -100,
				Length = 0,
				CellSize = -1f,
				SpawnPointCount = 0,
				ResourceNodeCount = -5,
				ObstacleDensity = 9f,
				RidgeWidth = -2f,
				RockSiteCount = -1,
			};

			TowerDefenseMapLayout layout = TowerDefenseMapGenerator.Generate(broken);

			Assert.That(layout.Width, Is.GreaterThan(0));
			Assert.That(layout.Length, Is.GreaterThan(0));
			Assert.That(layout.CellSize, Is.GreaterThan(0f));
			Assert.That(layout.EnemySpawnPoints.Count, Is.GreaterThanOrEqualTo(1));
			Assert.That(layout.IsBlocked(layout.CoreCell), Is.False);
		}

		[Test]
		public void SpawnPoints_AreDistinctCells()
		{
			for (int seed = 0; seed < SEED_SWEEP; seed++)
			{
				TowerDefenseMapLayout layout = TowerDefenseMapGenerator.Generate(Params(seed));

				HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
				for (int i = 0; i < layout.EnemySpawnPoints.Count; i++)
				{
					Vector2Int cell = layout.WorldToCell(layout.EnemySpawnPoints[i]);
					Assert.That(seen.Add(cell), Is.True, $"seed={seed} 스폰 지점이 겹쳤다.");
				}
			}
		}

		private const int SEED_SWEEP = 40;
		private const float EPSILON = 0.001f;
	}
}
