using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using VContainer;

namespace WitchMendokusai
{
	// CityPaintManager 의 주민 움직임 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 CityPaintManager.cs 를 본다.
	public partial class CityPaintManager : MonoBehaviour
	{
		// INC-7 — 통근 시민 placeholder 에이전트. key = 집 셀(1 주거 = 1 시민). 진실 = CitizenRegistry,
		// 이건 그 시각 투영(렌더+이동). CitizenRegistry 가 비면 여기도 빔.
		private sealed class CitizenAgent
		{
			public CommutePathFollower Follower;
			public GameObject Visual;
		}
		private readonly Dictionary<Vector3Int, CitizenAgent> citizenAgents = new();

		// 4-이웃 오프셋 (존/직장 셀의 인접 도로 찾기 — RoadGraph NEIGHBOR_OFFSETS 와 동일 평면).
		private static readonly Vector3Int[] FOUR_NEIGHBORS =
		{
			new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0),
		};

		// INC-7 — registry(진실) ↔ 도시 상태 동기화 + 시각 에이전트 spawn/despawn. (1 주거 = 1 시민.)
		private void SyncCitizens(WorldStage worldStage, CityCellQuery query)
		{
			CitizenRegistry registry = worldStage.CitizenRegistry;

			// 1) 집 건물 사라진 시민 제거.
			HashSet<Vector3Int> residentialHomes = new(query.BuiltCells(ZoneType.Residential));
			registry.Citizens.RemoveAll(citizen => residentialHomes.Contains(citizen.HomeCell) == false);

			// 2) 시민 없는 주거에 도달 가능한 직장 배정해 추가.
			HashSet<Vector3Int> homed = new();
			foreach (CitizenSaveData citizen in registry.Citizens)
				homed.Add(citizen.HomeCell);

			foreach (Vector3Int home in residentialHomes)
			{
				if (homed.Contains(home))
					continue;
				if (TryAssignWork(worldStage, query, home, out Vector3Int work))
					registry.Add(new CitizenSaveData(home, work, CitizenState.GoingToWork));
			}

			// 3) 시각 에이전트 = registry 투영(spawn/despawn).
			SyncCitizenVisuals(worldStage, registry);
		}

		private void SyncCitizenVisuals(WorldStage worldStage, CitizenRegistry registry)
		{
			HashSet<Vector3Int> registryHomes = new();
			foreach (CitizenSaveData citizen in registry.Citizens)
				registryHomes.Add(citizen.HomeCell);

			List<Vector3Int> stale = new();
			foreach (KeyValuePair<Vector3Int, CitizenAgent> entry in citizenAgents)
				if (registryHomes.Contains(entry.Key) == false)
					stale.Add(entry.Key);
			foreach (Vector3Int home in stale)
			{
				Destroy(citizenAgents[home].Visual);
				citizenAgents.Remove(home);
			}

			foreach (CitizenSaveData citizen in registry.Citizens)
			{
				if (citizenAgents.ContainsKey(citizen.HomeCell))
					continue;
				if (TryBuildCommutePath(worldStage, citizen.HomeCell, citizen.WorkCell, out List<Vector3Int> path) == false)
					continue;

				citizenAgents[citizen.HomeCell] = new CitizenAgent
				{
					Follower = new CommutePathFollower(path),
					Visual = CreateCitizenCube(),
				};
			}
		}

		// 집에서 도로로 도달 가능한 직장(상업/산업 건물) 찾기 — 첫 reachable.
		private bool TryAssignWork(WorldStage worldStage, CityCellQuery query, Vector3Int home, out Vector3Int work)
		{
			RoadGraph roadGraph = worldStage.RoadGraph;
			if (TryRoadNeighbor(roadGraph, home, out Vector3Int homeRoad) == false)
			{
				work = default;
				return false;
			}

			foreach (Vector3Int candidate in JobCells(query))
			{
				if (TryRoadNeighbor(roadGraph, candidate, out Vector3Int workRoad) == false)
					continue;
				if (roadGraph.FindPath(homeRoad, workRoad).Count > 0)
				{
					work = candidate;
					return true;
				}
			}

			work = default;
			return false;
		}

		private static IEnumerable<Vector3Int> JobCells(CityCellQuery query)
		{
			foreach (Vector3Int cell in query.BuiltCells(ZoneType.Commercial))
				yield return cell;
			foreach (Vector3Int cell in query.BuiltCells(ZoneType.Industrial))
				yield return cell;
		}

		// 집→(집인접도로)→…도로…→(직장인접도로)→직장 셀 시퀀스. 도달 불가면 false.
		private bool TryBuildCommutePath(WorldStage worldStage, Vector3Int home, Vector3Int work, out List<Vector3Int> path)
		{
			RoadGraph roadGraph = worldStage.RoadGraph;
			path = null;

			if (TryRoadNeighbor(roadGraph, home, out Vector3Int homeRoad) == false)
				return false;
			if (TryRoadNeighbor(roadGraph, work, out Vector3Int workRoad) == false)
				return false;

			List<Vector3Int> roadPath = roadGraph.FindPath(homeRoad, workRoad);
			if (roadPath.Count == 0)
				return false;

			path = new List<Vector3Int> { home };
			path.AddRange(roadPath);
			path.Add(work);
			return true;
		}

		// 셀의 4-이웃 중 첫 도로 셀 (존/직장 셀 → 인접 도로 진입점).
		private static bool TryRoadNeighbor(RoadGraph roadGraph, Vector3Int cell, out Vector3Int road)
		{
			for (int i = 0; i < FOUR_NEIGHBORS.Length; i++)
			{
				Vector3Int neighbor = cell + FOUR_NEIGHBORS[i];
				if (roadGraph.HasRoad(neighbor))
				{
					road = neighbor;
					return true;
				}
			}

			road = default;
			return false;
		}

		// 시민 placeholder 큐브 (실 prefab/스킨 deferred).
		private GameObject CreateCitizenCube()
		{
			GameObject cube = CombatPrimitive.Create(PrimitiveType.Cube);
			cube.transform.SetParent(visualRoot, false);
			cube.name = "Citizen";
			cube.layer = IGNORE_RAYCAST_LAYER;

			Collider cubeCollider = cube.GetComponent<Collider>();
			if (cubeCollider != null)
				Destroy(cubeCollider);

			cube.transform.localScale = Vector3.one * citizenSize;
			cube.GetComponent<Renderer>().sharedMaterial = GetMaterial(citizenColor);
			return cube;
		}
	}
}
