using System.Collections.Generic;
using System.Linq;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	// 도로 격자 + 그래프. SimCity Phase 1 의 삼중역할 substrate (단일 셀-점유 dict 의 3개 derived 뷰):
	//  ① pathfinding 그래프 — 노드 = 도로 셀, 엣지 = 4-이웃 도로 (런타임 유도, 저장 X → dict 만 영속).
	//  ② 유틸 전파 도관 — 같은 인접성 위 BFS/flood-fill (Phase 2 전력/물).
	//  ③ lot 스냅 캔버스 — 도로 인접 빈 셀이 건물 lot 후보 (IsRoadAdjacent).
	//
	// GridData(건물) 와 형제 — GridData 확장 X. 같은 셀 좌표계(Vector3Int, z=0 평면, BuildManager
	// 와 동일)를 공유하되 책임 분리(6 동기 「분리」). 한 셀이 road XOR (zone/building) — 의미 비혼재.
	//
	// 좌표 = Vector3Int (GridData 1:1) — 검증된 직렬화(WorldStageSaveData round-trip) + lot 인접
	// 좌표 정합(도로 (x,y,0) ↔ 건물 lot (x±1,y,0) 직접 join). Vector2Int 미검증 직렬화 회피.
	public class RoadGraph : ISavable<List<KeyValuePair<Vector3Int, RoadCellData>>>
	{
		// 4-이웃 오프셋 (XY 평면, z=0 — BuildManager.GetBuildingCoords 와 같은 평면).
		private static readonly Vector3Int[] NEIGHBOR_OFFSETS =
		{
			new(1, 0, 0),
			new(-1, 0, 0),
			new(0, 1, 0),
			new(0, -1, 0),
		};

		public Dictionary<Vector3Int, RoadCellData> RoadData { get; private set; } = new();

		public bool HasRoad(Vector3Int cell)
		{
			return RoadData.ContainsKey(cell);
		}

		public bool TryGetRoad(Vector3Int cell, out RoadCellData data)
		{
			return RoadData.TryGetValue(cell, out data);
		}

		// 도로 페인트 = 멱등 덮어쓰기 (GridData.AddBuildingAt 의 중복 경고와 다름 — 도로는 이산
		// 배치가 아니라 페인트 캔버스라 재페인트가 정상 UX).
		public void AddRoad(Vector3Int cell, RoadType type = RoadType.Basic)
		{
			RoadData[cell] = new RoadCellData(type);
		}

		public void RemoveRoad(Vector3Int cell)
		{
			RoadData.Remove(cell);
		}

		// ① pathfinding — 이 셀의 4-이웃 중 도로인 셀만 (엣지 런타임 유도).
		public IEnumerable<Vector3Int> Neighbors(Vector3Int cell)
		{
			for (int i = 0; i < NEIGHBOR_OFFSETS.Length; i++)
			{
				Vector3Int neighbor = cell + NEIGHBOR_OFFSETS[i];
				if (RoadData.ContainsKey(neighbor))
				{
					yield return neighbor;
				}
			}
		}

		// ③ lot 스냅 — 이 셀(도로 아니어도 됨) 의 4-이웃에 도로가 하나라도 있나 (빈 lot 의 건축 적격 판정).
		public bool IsRoadAdjacent(Vector3Int cell)
		{
			for (int i = 0; i < NEIGHBOR_OFFSETS.Length; i++)
			{
				if (RoadData.ContainsKey(cell + NEIGHBOR_OFFSETS[i]))
				{
					return true;
				}
			}

			return false;
		}

		// ① pathfinding 연결성 — from→to 가 도로만 밟아 도달 가능한가 (BFS). 둘 다 도로여야 함.
		public bool AreConnected(Vector3Int from, Vector3Int to)
		{
			if (RoadData.ContainsKey(from) == false || RoadData.ContainsKey(to) == false)
			{
				return false;
			}

			if (from == to)
			{
				return true;
			}

			HashSet<Vector3Int> visited = new() { from };
			Queue<Vector3Int> frontier = new();
			frontier.Enqueue(from);

			while (frontier.Count > 0)
			{
				Vector3Int current = frontier.Dequeue();
				foreach (Vector3Int neighbor in Neighbors(current))
				{
					if (neighbor == to)
					{
						return true;
					}

					if (visited.Add(neighbor))
					{
						frontier.Enqueue(neighbor);
					}
				}
			}

			return false;
		}

		// ① pathfinding 경로 — from→to 를 도로만 밟아 도달하는 셀 시퀀스 (BFS 최단, predecessor 역추적).
		// from→to 순서 정렬. 둘 다 도로여야 하고, 미연결이면 빈 리스트 (IsRoadAdjacent 동형 정상 경로 —
		// FastFail 아님, "경로 없음"은 정상 질의 결과). from==to 면 단일 원소. AreConnected 의 BFS frontier
		// 를 predecessor 맵으로 일반화 (Phase 2 통근 에이전트·유틸 전파 경로의 단일 토대).
		public List<Vector3Int> FindPath(Vector3Int from, Vector3Int to)
		{
			List<Vector3Int> path = new();

			if (RoadData.ContainsKey(from) == false || RoadData.ContainsKey(to) == false)
			{
				return path;
			}

			if (from == to)
			{
				path.Add(from);
				return path;
			}

			Dictionary<Vector3Int, Vector3Int> predecessor = new();
			HashSet<Vector3Int> visited = new() { from };
			Queue<Vector3Int> frontier = new();
			frontier.Enqueue(from);

			bool reached = false;
			while (frontier.Count > 0 && reached == false)
			{
				Vector3Int current = frontier.Dequeue();
				foreach (Vector3Int neighbor in Neighbors(current))
				{
					if (visited.Add(neighbor) == false)
					{
						continue;
					}

					predecessor[neighbor] = current;
					if (neighbor == to)
					{
						reached = true;
						break;
					}

					frontier.Enqueue(neighbor);
				}
			}

			if (reached == false)
			{
				return path;
			}

			// to → from 역추적 후 뒤집어 from→to 순서로.
			Vector3Int step = to;
			path.Add(step);
			while (step != from)
			{
				step = predecessor[step];
				path.Add(step);
			}

			path.Reverse();
			return path;
		}

		// ② 유틸 전파 도관 — 여러 소스 셀에서 도로 따라 maxRange 홉 안에 도달하는 도로 셀 집합 (멀티소스 BFS).
		// 전기/물/마나 무관한 순수 그래프 질의(전파 도관). maxRange < 0 = 무한(전역). maxRange 0 = 소스만.
		// 도로 아닌 소스는 무시(전파는 도로 위만). Neighbors 재사용 → 도로 단절 구역은 자동 미도달.
		// (Phase 1 deferred 실현 — AreConnected/CountConnectedComponents BFS 골격의 멀티소스+range 일반화.)
		public HashSet<Vector3Int> FloodFill(IEnumerable<Vector3Int> sources, int maxRange)
		{
			HashSet<Vector3Int> visited = new();
			Dictionary<Vector3Int, int> distance = new();
			Queue<Vector3Int> frontier = new();

			foreach (Vector3Int source in sources)
			{
				if (RoadData.ContainsKey(source) == false)
				{
					continue;
				}

				if (visited.Add(source))
				{
					distance[source] = 0;
					frontier.Enqueue(source);
				}
			}

			while (frontier.Count > 0)
			{
				Vector3Int current = frontier.Dequeue();
				int currentDistance = distance[current];

				// range 도달 셀에선 더 안 퍼짐 (maxRange 음수 = 무한이라 게이트 통과).
				if (maxRange >= 0 && currentDistance >= maxRange)
				{
					continue;
				}

				foreach (Vector3Int neighbor in Neighbors(current))
				{
					if (visited.Add(neighbor))
					{
						distance[neighbor] = currentDistance + 1;
						frontier.Enqueue(neighbor);
					}
				}
			}

			return visited;
		}

		// 분리된 도로 덩어리 개수 (연결 컴포넌트). 도로망이 몇 조각으로 끊겨 있나 — Phase 2 유틸
		// 커버리지·고립 구역 진단의 토대.
		public int CountConnectedComponents()
		{
			HashSet<Vector3Int> visited = new();
			int components = 0;

			foreach (Vector3Int cell in RoadData.Keys)
			{
				if (visited.Contains(cell))
				{
					continue;
				}

				components++;

				Queue<Vector3Int> frontier = new();
				frontier.Enqueue(cell);
				visited.Add(cell);

				while (frontier.Count > 0)
				{
					Vector3Int current = frontier.Dequeue();
					foreach (Vector3Int neighbor in Neighbors(current))
					{
						if (visited.Add(neighbor))
						{
							frontier.Enqueue(neighbor);
						}
					}
				}
			}

			return components;
		}

		// ISavable — GridData 1:1 미러 (Clear 안 함, 덮어쓰기 머지).
		public void Load(List<KeyValuePair<Vector3Int, RoadCellData>> saveData)
		{
			foreach ((Vector3Int key, RoadCellData value) in saveData)
			{
				RoadData[key] = value;
			}
		}

		public List<KeyValuePair<Vector3Int, RoadCellData>> Save()
		{
			return RoadData.ToList();
		}
	}
}
