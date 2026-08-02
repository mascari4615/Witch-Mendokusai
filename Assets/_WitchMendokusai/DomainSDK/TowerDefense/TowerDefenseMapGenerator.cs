using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 특수시공 개척(TD) 절차적 맵 생성기 — 시드 하나로 "게임판"을 통째 뽑는다.
	/// 순수 정적 함수(MonoBehaviour·씬·전역 RNG 0) → EditMode 로 규칙 전량 검증.
	///
	/// ★ 기존 지형 생성기와의 관계 — 층이 다르다:
	///   TerrainGraph/TerrainGenerator/BiomeData = *땅의 생김새*(높이·질감·식생)를 만드는 층.
	///   본 생성기 = *게임판의 의미*(코어·적 스폰·자원 노드·암반 능선·통로)를 만드는 층.
	///   높이맵 노이즈는 "여기가 길목이다"를 말해주지 못하므로 토폴로지는 따로 세우고,
	///   대신 VoronoiNode 가 쓰던 *셀 구획* 개념과 TerrainGenerator 의 *시드 결정론* 규약은 그대로 계승한다.
	///   (땅 비주얼이 필요해지면 같은 Seed 로 TerrainGenerator 를 샘플링해 이 판 위에 덮으면 정합.)
	///
	/// ★ 암반을 덩어리가 아니라 *능선*으로 만드는 이유: Voronoi 경계(두 최근접 site 등거리 지대)를 굳히면
	///   판이 구획으로 갈리면서 자연스러운 길목이 생긴다. 길목이 있어야 "어디에 타워를 놓을까"가 아프고,
	///   그게 곧 TD 의 재미다. 개활지면 타워 위치가 무의미해지고 그냥 사거리 싸움이 된다.
	///
	/// ★ 마지막에 연결성을 *뚫어서* 보장한다(재추첨 X): 스폰·노드에서 코어까지 길이 막혔으면 그 경로의 암반을
	///   깎아낸다. 재추첨 루프는 최악의 경우 종료를 보장 못 하고 시드↔결과 대응도 흐려진다.
	/// </summary>
	public static class TowerDefenseMapGenerator
	{
		public static TowerDefenseMapLayout Generate(TowerDefenseMapParameters parameters)
		{
			TowerDefenseMapParameters config = parameters.Normalized();
			System.Random random = new System.Random(config.Seed);

			Vector2Int coreCell = new Vector2Int(config.Width / 2, config.Length / 2);
			List<Vector2Int> spawnCells = BuildSpawnCells(config, coreCell, random);
			HashSet<Vector2Int> obstacles = BuildRidgeObstacles(config, random);

			ClearAround(obstacles, coreCell, config.CoreClearRadius);
			for (int i = 0; i < spawnCells.Count; i++)
				ClearAround(obstacles, spawnCells[i], config.SpawnClearRadius);

			List<Vector2Int> nodeCells = PickResourceNodeCells(config, coreCell, obstacles, random);
			for (int i = 0; i < nodeCells.Count; i++)
				ClearAround(obstacles, nodeCells[i], config.NodeClearRadius);

			EnsureReachable(config, obstacles, coreCell, spawnCells, nodeCells);

			List<TowerDefenseResourceNodeSpot> nodes = BuildNodeSpots(config, coreCell, nodeCells);

			return new TowerDefenseMapLayout(
				config.Seed,
				config.Width,
				config.Length,
				config.CellSize,
				coreCell,
				spawnCells,
				nodes,
				obstacles);
		}

		// ── 적 스폰: 외곽 링에 각도 균등 + 지터. 완전 대칭이면 판이 매번 같은 얼굴이 된다. ──────────────
		private static List<Vector2Int> BuildSpawnCells(TowerDefenseMapParameters config, Vector2Int coreCell, System.Random random)
		{
			float ringRadius = Mathf.Max(1f, Mathf.Min(config.Width, config.Length) * 0.5f - config.SpawnRingInset - 1f);
			float startAngle = (float)random.NextDouble() * FULL_CIRCLE_DEGREES;
			float angleStep = FULL_CIRCLE_DEGREES / config.SpawnPointCount;

			List<Vector2Int> spawnCells = new List<Vector2Int>(config.SpawnPointCount);
			HashSet<Vector2Int> taken = new HashSet<Vector2Int>();

			for (int i = 0; i < config.SpawnPointCount; i++)
			{
				float jitter = ((float)random.NextDouble() * 2f - 1f) * config.SpawnAngleJitter;
				float angleRadians = (startAngle + angleStep * i + jitter) * Mathf.Deg2Rad;

				Vector2Int cell = new Vector2Int(
					coreCell.x + Mathf.RoundToInt(Mathf.Cos(angleRadians) * ringRadius),
					coreCell.y + Mathf.RoundToInt(Mathf.Sin(angleRadians) * ringRadius));

				cell = ClampInside(cell, config);

				// 지터로 두 스폰이 같은 칸에 겹치면 빈 이웃으로 한 칸 민다(스폰 수는 요청대로 유지).
				if (taken.Contains(cell))
					cell = FindFreeNeighbour(cell, taken, config);

				taken.Add(cell);
				spawnCells.Add(cell);
			}

			return spawnCells;
		}

		// ── 암반 능선: Voronoi 경계 지대를 굳히고 밀도로 솎아 구멍(=통로)을 남긴다. ───────────────────
		private static HashSet<Vector2Int> BuildRidgeObstacles(TowerDefenseMapParameters config, System.Random random)
		{
			HashSet<Vector2Int> obstacles = new HashSet<Vector2Int>();
			if (config.RockSiteCount < MIN_SITES_FOR_RIDGE || config.ObstacleDensity <= 0f || config.RidgeWidth <= 0f)
				return obstacles;

			// ★ 지형의 정본을 *경계 없는 지형*으로 옮겼다(TASK-WM-194, 무한 맵 2/2).
			//   예전에는 판 크기만큼 무작위 site 를 뿌려 만들었다 — 그러면 판이 커질 때마다 site 수를 손으로
			//   맞춰야 하고, 무엇보다 **판 밖에는 지형이 없다**. 좌표에서 계산하는 지형으로 바꾸면 창(window)을
			//   넓히거나 옮겨도 이미 있던 자리의 지형이 그대로 이어진다 — 무한으로 가는 유일한 길이다.
			//   site 수는 이제 「구획 한 칸의 크기」로 환산된다(판 면적 ÷ site 수 의 제곱근).
			int siteSpacing = Mathf.Max(2, Mathf.RoundToInt(Mathf.Sqrt(config.Width * (float)config.Length / Mathf.Max(1, config.RockSiteCount))));
			Vector2Int centerCell = new Vector2Int(config.Width / 2, config.Length / 2);
			TowerDefenseInfiniteTerrain terrain = new(
				config.Seed, centerCell, siteSpacing, config.RidgeWidth, config.ObstacleDensity, config.CoreClearRadius);

			for (int x = 0; x < config.Width; x++)
			{
				for (int z = 0; z < config.Length; z++)
				{
					Vector2Int cell = new Vector2Int(x, z);
					if (terrain.IsBlocked(cell))
						obstacles.Add(cell);
				}
			}

			return obstacles;
		}

		// ── 자원 노드: 코어에서 충분히 멀고 서로 충분히 떨어진 자리만. 뭉치면 개척이 아니라 한 덩어리 방어. ──
		private static List<Vector2Int> PickResourceNodeCells(
			TowerDefenseMapParameters config,
			Vector2Int coreCell,
			HashSet<Vector2Int> obstacles,
			System.Random random)
		{
			List<Vector2Int> picked = new List<Vector2Int>(config.ResourceNodeCount);
			if (config.ResourceNodeCount <= 0)
				return picked;

			int marginCells = Mathf.CeilToInt(config.NodeEdgeMargin);

			// ★ 자원은 *광맥*에서 난다(사용자 지시: "자원이 한곳에 여러 타일이 좀 뭉쳐 있어야 할듯? 광맥처럼").
			//   아무 빈 칸이나 후보로 삼으면 자원이 판 전체에 흩뿌려져 「어디로 넓힐까」가 사라진다.
			//   경계 없는 지형이 알려주는 광맥 타일만 후보다 — 덩어리로 뭉쳐 있으므로 채집 여러 기가 붙는다.
			// ★ 광맥 밀도는 *암반*이 아니라 **요청한 노드 수**에 맞춘다 — 둘을 같은 숫자로 묶으면
			//   암반을 없앤 판(RockSiteCount 0)에서 광맥까지 사라져 자원이 0 이 된다(테스트가 그걸 잡았다).
			//   판 넓이를 노드 수로 나눈 간격이면 「요청한 만큼은 앉을 수 있다」가 성립한다.
			int veinSpacing = Mathf.Clamp(
				Mathf.RoundToInt(Mathf.Sqrt(config.Width * (float)config.Length / Mathf.Max(1, config.ResourceNodeCount)) * 0.5f),
				4, 18);
			TowerDefenseInfiniteTerrain veins = new(
				config.Seed, new Vector2Int(config.Width / 2, config.Length / 2),
				Mathf.Max(2, veinSpacing), config.RidgeWidth, config.ObstacleDensity, config.CoreClearRadius)
			{
				VeinSpacing = veinSpacing,
				VeinChance = 0.95f,
			};

			List<Vector2Int> candidates = new List<Vector2Int>();
			for (int x = marginCells; x < config.Width - marginCells; x++)
			{
				for (int z = marginCells; z < config.Length - marginCells; z++)
				{
					Vector2Int cell = new Vector2Int(x, z);
					if (obstacles.Contains(cell))
						continue;
					if (CellDistance(cell, coreCell) < config.NodeMinCoreDistance)
						continue;
					if (veins.IsResourceTile(cell) == false)
						continue;
					candidates.Add(cell);
				}
			}

			Shuffle(candidates, random);

			// 1패스 = 간격 + 각도 분산 둘 다 요구(사방으로 뻗은 판이 나온다).
			// 2패스 = 각도를 포기하고 간격만(요청 개수는 반드시 채운다 — 노드가 비면 경제가 죽는다).
			// 재추첨 루프가 아니라 조건 완화 2단계 = 항상 종료 + 결정론 유지.
			CollectNodes(config, coreCell, candidates, picked, true);
			if (picked.Count < config.ResourceNodeCount)
				CollectNodes(config, coreCell, candidates, picked, false);

			picked.Sort(CompareCell); // 채택 순서(셔플 의존)와 무관하게 노드 인덱스를 안정화.
			return picked;
		}

		private static void CollectNodes(
			TowerDefenseMapParameters config,
			Vector2Int coreCell,
			List<Vector2Int> candidates,
			List<Vector2Int> picked,
			bool requireAngularSpread)
		{
			for (int i = 0; i < candidates.Count && picked.Count < config.ResourceNodeCount; i++)
			{
				Vector2Int candidate = candidates[i];

				bool rejected = false;
				for (int j = 0; j < picked.Count; j++)
				{
					if (CellDistance(candidate, picked[j]) < config.NodeMinSpacing)
					{
						rejected = true;
						break;
					}

					if (requireAngularSpread == false || config.NodeAngularSpread <= 0f)
						continue;

					if (AngleBetween(coreCell, candidate, picked[j]) >= config.NodeAngularSpread)
						continue;

					rejected = true;
					break;
				}

				if (rejected == false)
					picked.Add(candidate);
			}
		}

		/// <summary> 코어에서 본 두 셀의 사잇각(도, 0~180). </summary>
		private static float AngleBetween(Vector2Int origin, Vector2Int left, Vector2Int right)
		{
			Vector2 toLeft = new Vector2(left.x - origin.x, left.y - origin.y);
			Vector2 toRight = new Vector2(right.x - origin.x, right.y - origin.y);
			if (toLeft.sqrMagnitude <= 0f || toRight.sqrMagnitude <= 0f)
				return 0f;

			return Vector2.Angle(toLeft, toRight);
		}

		private static List<TowerDefenseResourceNodeSpot> BuildNodeSpots(
			TowerDefenseMapParameters config,
			Vector2Int coreCell,
			List<Vector2Int> nodeCells)
		{
			// 정규화 기준 = 코어에서 판 모서리까지. 판 크기가 달라도 "멀다"의 의미가 같게 유지된다.
			float maxRadius = Mathf.Max(1f, CellDistance(coreCell, new Vector2Int(0, 0)));
			float originX = -config.Width * config.CellSize * 0.5f;
			float originZ = -config.Length * config.CellSize * 0.5f;
			float halfCell = config.CellSize * 0.5f;

			List<TowerDefenseResourceNodeSpot> spots = new List<TowerDefenseResourceNodeSpot>(nodeCells.Count);
			for (int i = 0; i < nodeCells.Count; i++)
			{
				Vector2Int cell = nodeCells[i];
				float normalizedDistance = Mathf.Clamp01(CellDistance(cell, coreCell) / maxRadius);

				TowerDefenseNodeTier tier = normalizedDistance <= config.InnerTierRatio
					? TowerDefenseNodeTier.Inner
					: TowerDefenseNodeTier.Outer;

				float incomeMultiplier = Mathf.Lerp(config.NearIncomeMultiplier, config.FarIncomeMultiplier, normalizedDistance);

				Vector3 position = new Vector3(
					originX + cell.x * config.CellSize + halfCell,
					0f,
					originZ + cell.y * config.CellSize + halfCell);

				spots.Add(new TowerDefenseResourceNodeSpot(cell, position, tier, incomeMultiplier));
			}

			return spots;
		}

		// ── 연결성 보장: 코어에서 못 닿는 지점이 있으면 그 방향의 암반을 깎아 길을 낸다. ─────────────────
		private static void EnsureReachable(
			TowerDefenseMapParameters config,
			HashSet<Vector2Int> obstacles,
			Vector2Int coreCell,
			List<Vector2Int> spawnCells,
			List<Vector2Int> nodeCells)
		{
			List<Vector2Int> mustReach = new List<Vector2Int>(spawnCells.Count + nodeCells.Count);
			mustReach.AddRange(spawnCells);
			mustReach.AddRange(nodeCells);

			for (int i = 0; i < mustReach.Count; i++)
			{
				HashSet<Vector2Int> reachable = FloodFill(config, obstacles, coreCell);
				if (reachable.Contains(mustReach[i]))
					continue;

				CarveLine(obstacles, mustReach[i], coreCell);
			}
		}

		private static HashSet<Vector2Int> FloodFill(TowerDefenseMapParameters config, HashSet<Vector2Int> obstacles, Vector2Int start)
		{
			HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
			if (obstacles.Contains(start))
				return visited;

			Queue<Vector2Int> frontier = new Queue<Vector2Int>();
			frontier.Enqueue(start);
			visited.Add(start);

			while (frontier.Count > 0)
			{
				Vector2Int current = frontier.Dequeue();
				for (int i = 0; i < NEIGHBOUR_OFFSETS.Length; i++)
				{
					Vector2Int next = current + NEIGHBOUR_OFFSETS[i];
					if (next.x < 0 || next.x >= config.Width || next.y < 0 || next.y >= config.Length)
						continue;
					if (obstacles.Contains(next) || visited.Contains(next))
						continue;

					visited.Add(next);
					frontier.Enqueue(next);
				}
			}

			return visited;
		}

		/// <summary> from→to 직선(Bresenham)을 따라 암반을 걷어낸다. 4방 이동이 끊기지 않게 계단 칸도 같이 뚫는다. </summary>
		private static void CarveLine(HashSet<Vector2Int> obstacles, Vector2Int from, Vector2Int to)
		{
			int x = from.x;
			int y = from.y;
			int deltaX = Mathf.Abs(to.x - x);
			int deltaY = Mathf.Abs(to.y - y);
			int stepX = to.x > x ? 1 : -1;
			int stepY = to.y > y ? 1 : -1;
			int error = deltaX - deltaY;

			obstacles.Remove(new Vector2Int(x, y));

			while (x != to.x || y != to.y)
			{
				int doubledError = error * 2;

				if (doubledError > -deltaY)
				{
					error -= deltaY;
					x += stepX;
					obstacles.Remove(new Vector2Int(x, y));
				}

				if (doubledError >= deltaX)
					continue;

				error += deltaX;
				y += stepY;
				obstacles.Remove(new Vector2Int(x, y));
			}
		}

		// ── 잡 도우미 ────────────────────────────────────────────────────────────────────────────
		private static void ClearAround(HashSet<Vector2Int> obstacles, Vector2Int center, float radius)
		{
			if (radius <= 0f)
			{
				obstacles.Remove(center);
				return;
			}

			int range = Mathf.CeilToInt(radius);
			for (int dx = -range; dx <= range; dx++)
			{
				for (int dz = -range; dz <= range; dz++)
				{
					Vector2Int cell = new Vector2Int(center.x + dx, center.y + dz);
					if (CellDistance(cell, center) > radius)
						continue;
					obstacles.Remove(cell);
				}
			}
		}

		private static Vector2Int ClampInside(Vector2Int cell, TowerDefenseMapParameters config)
		{
			return new Vector2Int(
				Mathf.Clamp(cell.x, 0, config.Width - 1),
				Mathf.Clamp(cell.y, 0, config.Length - 1));
		}

		private static Vector2Int FindFreeNeighbour(Vector2Int cell, HashSet<Vector2Int> taken, TowerDefenseMapParameters config)
		{
			for (int radius = 1; radius < Mathf.Max(config.Width, config.Length); radius++)
			{
				for (int dx = -radius; dx <= radius; dx++)
				{
					for (int dz = -radius; dz <= radius; dz++)
					{
						Vector2Int candidate = ClampInside(new Vector2Int(cell.x + dx, cell.y + dz), config);
						if (taken.Contains(candidate))
							continue;
						return candidate;
					}
				}
			}

			return cell;
		}

		private static void Shuffle(List<Vector2Int> items, System.Random random)
		{
			for (int i = items.Count - 1; i > 0; i--)
			{
				int swapIndex = random.Next(i + 1);
				Vector2Int swapped = items[i];
				items[i] = items[swapIndex];
				items[swapIndex] = swapped;
			}
		}

		private static float CellDistance(Vector2Int left, Vector2Int right)
		{
			float deltaX = left.x - right.x;
			float deltaY = left.y - right.y;
			return Mathf.Sqrt(deltaX * deltaX + deltaY * deltaY);
		}

		private static int CompareCell(Vector2Int left, Vector2Int right)
		{
			if (left.y != right.y)
				return left.y.CompareTo(right.y);
			return left.x.CompareTo(right.x);
		}

		private const float FULL_CIRCLE_DEGREES = 360f;
		private const int MIN_SITES_FOR_RIDGE = 2;

		private static readonly Vector2Int[] NEIGHBOUR_OFFSETS =
		{
			new Vector2Int(1, 0),
			new Vector2Int(-1, 0),
			new Vector2Int(0, 1),
			new Vector2Int(0, -1),
		};
	}
}
