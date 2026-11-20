using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 맵 오브젝트 배치와 수명. 판 시작에 예산으로 고르고, 첫 물리 틱 뒤 (지면 생성 뒤) 와 등장 시각에 놓음
	// 정본 memo wm/design/map-objects-20261119
	public partial class MonsterSpawner
	{
		private readonly List<MapObjectPlan> mapObjectPlans = new();
		private readonly List<MapObjectSite> mapObjectSites = new();
		private readonly List<Vector3> mapObjectPositions = new();
		private System.Random mapObjectRandom;
		private Coroutine pendingMapObjectStart;
		private int nextMapObjectPlan;
		// 지금 계획의 배치 실패 횟수. 착지 전, 몸통 겹침 같은 일시 실패는 다음 틱에 다시
		private int mapObjectFailedRounds;
		private int mapObjectGeneration;
		private bool mapObjectsReady;
		private Transform mapObjectRoot;

		public IReadOnlyList<MapObjectSite> MapObjectSites => mapObjectSites;

		private void InitMapObjects(Dungeon dungeon)
		{
			StopMapObjects();
			MapObjectBudget budget = dungeon.MapObjectBudget;
			if (budget == null)
				return;

			mapObjectPlans.AddRange(MapObjectPlanner.Plan(budget, RunSeed.Derive(budget.SeedSalt), out int remaining));
			mapObjectRandom = new System.Random(RunSeed.Derive(budget.SeedSalt + 1));
			Debug.Log($"[MapObject] 예산 {budget.Credits}, 남음 {remaining}, 계획 {mapObjectPlans.Count}개");
			pendingMapObjectStart = StartCoroutine(StartMapObjectsAfterPhysics(mapObjectGeneration));
		}

		// 판 시작과 같은 호출에서는 지면 청크가 아직 없음 (GroundGenerator.FixedUpdate 가 만듦). 첫 물리 틱 뒤 배치
		private IEnumerator StartMapObjectsAfterPhysics(int generation)
		{
			yield return new WaitForFixedUpdate();
			if (generation != mapObjectGeneration)
				yield break;
			pendingMapObjectStart = null;
			if (dungeonManager.IsDungeon == false || isActiveAndEnabled == false)
				yield break;
			mapObjectsReady = true;
			SpawnDueMapObjects();
		}

		private void UpdateMapObjects()
		{
			if (mapObjectsReady)
				SpawnDueMapObjects();
		}

		private void SpawnDueMapObjects()
		{
			float elapsed = (float)dungeonManager.Context.ElapsedTime.TotalSeconds;
			while (nextMapObjectPlan < mapObjectPlans.Count && mapObjectPlans[nextMapObjectPlan].SpawnTime <= elapsed)
			{
				MapObjectData data = mapObjectPlans[nextMapObjectPlan].Data;
				if (TryPlaceMapObject(data, mapObjectRandom, out _, out string failure) == false)
				{
					mapObjectFailedRounds++;
					if (mapObjectFailedRounds < MapObjectSettings.Load().PlacementRounds)
						return;
					Debug.LogWarning($"[MapObject] {data.name} 배치 포기 ({mapObjectFailedRounds}회): {failure}");
				}
				nextMapObjectPlan++;
				mapObjectFailedRounds = 0;
			}
		}

		// 배치 규칙 (거리, 간격, 지면, 장애물, 길) 대로 한 개. 실패 이유는 failure
		public bool TryPlaceMapObject(MapObjectData data, System.Random random, out MapObjectSite site, out string failure)
		{
			site = null;
			MapObjectSettings settings = MapObjectSettings.Load();
			Vector3 origin = playerProvider.Current.transform.position;
			if (TryMapObjectGround(origin, settings, out Vector3 playerGround) == false)
			{
				failure = "주인공 발밑 지면 없음";
				return false;
			}
			float radius = data.ResolveMarkerRadius(PlayerInteraction.InteractionDistance);
			MapObjectProbe probe = (Vector3 candidate, out Vector3 ground) => ProbeMapObjectSite(candidate, playerGround, radius, data.RequireRoute, settings, out ground);
			if (MapObjectPlanner.TryPickPosition(random, data, origin, mapObjectPositions, settings.PlacementAttempts, probe, out Vector3 position, out MapObjectPlacementTally tally) == false)
			{
				failure = tally.ToString();
				return false;
			}
			site = PlaceMapObjectAt(data, position);
			failure = null;
			Debug.Log($"[MapObject] {data.name} 배치 ({tally})");
			return true;
		}

		private MapObjectProbeResult ProbeMapObjectSite(Vector3 candidate, Vector3 playerGround, float radius, bool requireRoute, MapObjectSettings settings, out Vector3 ground)
		{
			if (TryMapObjectGround(candidate, settings, out ground) == false)
				return MapObjectProbeResult.NoGround;
			// 지면 위로 띄운 구. 지면 자체는 안 닿음
			if (Physics.CheckSphere(ground + Vector3.up * (radius + settings.GroundLift), radius, settings.ObstacleMask, QueryTriggerInteraction.Ignore))
				return MapObjectProbeResult.Blocked;
			if (requireRoute && IsPlayerRouteClear(playerGround, ground, settings) == false)
				return MapObjectProbeResult.RouteBlocked;
			return MapObjectProbeResult.Ok;
		}

		// 검사 없이 정해진 자리에. 개발자 창 near 와 규칙 배치의 마지막 단계
		public MapObjectSite PlaceMapObjectAt(MapObjectData data, Vector3 position)
		{
			MapObjectSettings settings = MapObjectSettings.Load();
			if (mapObjectRoot == null)
				mapObjectRoot = new GameObject("MapObjects").transform;
			GameObject owner = new(data.DisplayName);
			owner.SetActive(false);
			owner.transform.SetParent(mapObjectRoot, false);
			owner.transform.position = position;
			MapObjectSite site = owner.AddComponent<MapObjectSite>();
			site.Init(data, TryActivateMapObject);
			owner.AddComponent<InteractiveObject>();
			if (data.Prefab != null)
				Instantiate(data.Prefab, owner.transform, false);
			else
				MapObjectSite.BuildMarker(owner, data, settings, data.ResolveMarkerRadius(PlayerInteraction.InteractionDistance));
			owner.SetActive(true);
			mapObjectSites.Add(site);
			mapObjectPositions.Add(position);
			return site;
		}

		private bool TryMapObjectGround(Vector3 position, MapObjectSettings settings, out Vector3 ground)
		{
			if (Physics.Raycast(position + Vector3.up * settings.GroundProbeHeight, Vector3.down, out RaycastHit hit, settings.GroundProbeHeight * 2f, settings.GroundMask, QueryTriggerInteraction.Ignore))
			{
				ground = hit.point + Vector3.up * settings.GroundLift;
				return true;
			}
			ground = position;
			return false;
		}

		// 판 끝, 재도전, 재초기화. 대기 중 배치 취소와 놓인 것 제거
		private void StopMapObjects()
		{
			mapObjectGeneration++;
			if (pendingMapObjectStart != null)
			{
				StopCoroutine(pendingMapObjectStart);
				pendingMapObjectStart = null;
			}
			foreach (MapObjectSite site in mapObjectSites)
			{
				if (site == null)
					continue;
				site.gameObject.SetActive(false);
				Destroy(site.gameObject);
			}
			mapObjectSites.Clear();
			mapObjectPositions.Clear();
			mapObjectPlans.Clear();
			nextMapObjectPlan = 0;
			mapObjectFailedRounds = 0;
			mapObjectsReady = false;
		}
	}
}
