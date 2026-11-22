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
		private readonly List<Vector3> sameKindPositions = new();
		private System.Random mapObjectRandom;
		// 운 성소 굴림. 자리 고르기와 다른 갈래라 굴림 수가 배치를 바꾸지 않음
		private System.Random mapObjectRollRandom;
		private float lastMapObjectTickTime;
		// 판 전체 스캐너를 쓴 뒤. 나중에 나오는 오브젝트도 놓일 때 발견
		private bool mapObjectsRevealAll;
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
			// 예산 없는 던전 (진열장) 도 틱과 굴림은 돌림. 개발자 창 mapobj 로 놓은 오브젝트가 움직이게
			MapObjectBudget budget = dungeon.MapObjectBudget;
			int seedSalt = budget != null ? budget.SeedSalt : MapObjectSettings.Load().DefaultBudget.SeedSalt;
			mapObjectRandom = new System.Random(RunSeed.Derive(seedSalt + 1));
			mapObjectRollRandom = new System.Random(RunSeed.Derive(seedSalt + 2));
			if (budget != null)
			{
				mapObjectPlans.AddRange(MapObjectPlanner.Plan(budget, RunSeed.Derive(budget.SeedSalt), MapObjectSettings.Load().Pacing, out int remaining));
				Debug.Log($"[MapObject] 예산 {budget.Credits}, 남음 {remaining}, 계획 {mapObjectPlans.Count}개");
			}
			// 인물 숙련 조각은 던전 예산과 따로, 이 인물로 들어간 판에만. 간격은 인물 자료. 등장 시각 순서로 합침
			DollMasteryData mastery = DollMasteryRun.Data;
			MapObjectBudget shardBudget = mastery != null ? mastery.ShardBudget : null;
			if (shardBudget != null)
			{
				mapObjectPlans.AddRange(MapObjectPlanner.Plan(shardBudget, RunSeed.Derive(shardBudget.SeedSalt), new MapObjectPacing(mastery.ShardGapSeconds, false), out _));
				mapObjectPlans.Sort((left, right) => left.SpawnTime.CompareTo(right.SpawnTime));
				Debug.Log($"[MapObject] 숙련 조각 포함 계획 {mapObjectPlans.Count}개");
			}
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
			EnsureArena();
			mapObjectsReady = true;
			lastMapObjectTickTime = (float)dungeonManager.Context.ElapsedTime.TotalSeconds;
			SpawnDueMapObjects();
		}

		private void UpdateMapObjects()
		{
			if (mapObjectsReady == false)
				return;
			SpawnDueMapObjects();
			float elapsed = (float)dungeonManager.Context.ElapsedTime.TotalSeconds;
			float delta = Mathf.Max(0f, elapsed - lastMapObjectTickTime);
			lastMapObjectTickTime = elapsed;
			TickMapObjectSites(delta);
		}

		// 발견 거리 판정과 머무름 진행. 판 시간 (카드 고르는 동안 멈춤) 기준
		private void TickMapObjectSites(float deltaSeconds)
		{
			PlayerObject player = playerProvider.CurrentObject;
			if (player == null || player.IsAlive == false)
				return;
			MapObjectSettings settings = MapObjectSettings.Load();
			Vector3 playerPosition = player.transform.position;
			float discoverSquared = settings.DiscoverDistance * settings.DiscoverDistance;
			foreach (MapObjectSite site in mapObjectSites)
			{
				if (site == null || site.IsSpent)
					continue;
				if (site.IsKnown == false)
				{
					Vector3 offset = site.transform.position - playerPosition;
					offset.y = 0f;
					if (offset.sqrMagnitude <= discoverSquared)
						site.Reveal();
				}
				if (site.IsHolding)
					TickHold(site, deltaSeconds, playerPosition, settings);
				else if (site.Data.ActivateOnTouch && IsTouching(site, playerPosition))
					site.OnInteract();
			}
		}

		private static bool IsTouching(MapObjectSite site, Vector3 playerPosition)
		{
			Vector3 offset = site.transform.position - playerPosition;
			offset.y = 0f;
			float radius = site.Data.ResolveMarkerRadius(PlayerInteraction.InteractionDistance);
			return offset.sqrMagnitude <= radius * radius;
		}

		// 완료인데 효과를 못 내면 (적 상한, 체력 가득) 다음 틱에 다시. 반응 글자는 처음 한 번
		// 구역 밖 감소 (Decay) 면 그사이 진행이 줄 수 있음
		private void TickHold(MapObjectSite site, float deltaSeconds, Vector3 playerPosition, MapObjectSettings settings)
		{
			switch (site.TickHold(deltaSeconds, playerPosition))
			{
				case MapObjectHoldResult.Running:
					return;
				case MapObjectHoldResult.Failed:
					PopMapObjectText(site, settings.HoldFailText);
					Debug.Log($"[MapObject] {site.Data.name} 머무름 실패 (제한 {site.Data.HoldTimeLimit}초)");
					return;
				case MapObjectHoldResult.Completed:
					if (TryFindBlockedEffect(site.Data, MapObjectEffectTiming.OnHoldComplete, site.transform.position, out string blocked))
					{
						if (site.MarkCompletionBlocked())
						{
							Debug.Log($"[MapObject] {site.Data.name} 완료 보류: {blocked}");
							PopMapObjectText(site, blocked);
						}
						return;
					}
					ApplyMapObjectEffects(site, MapObjectEffectTiming.OnHoldComplete);
					PopMapObjectText(site, settings.HoldCompleteText);
					Debug.Log($"[MapObject] {site.Data.name} 머무름 완료");
					site.CompleteHold();
					return;
			}
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
			sameKindPositions.Clear();
			foreach (MapObjectSite placed in mapObjectSites)
				if (placed != null && placed.Data == data)
					sameKindPositions.Add(placed.transform.position);
			if (MapObjectPlanner.TryPickPosition(random, data, origin, mapObjectPositions, sameKindPositions, settings.SameKindSpacing, settings.PlacementAttempts, probe, out Vector3 position, out MapObjectPlacementTally tally) == false)
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
			// 유한 경계 밖이나 벽에 걸치는 자리
			if (IsInsideArena(candidate, radius + arena.SpawnInset) == false)
			{
				ground = candidate;
				return MapObjectProbeResult.Blocked;
			}
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
			site.Init(data, this, data.ResolveMarkerRadius(PlayerInteraction.InteractionDistance));
			owner.AddComponent<InteractiveObject>();
			if (data.Model != null)
				MapObjectModelBuilder.Build(data.Model, owner.transform, settings);
			if (data.ShowMarker)
				site.BuildMarker(settings);
			owner.SetActive(true);
			if (mapObjectsRevealAll)
				site.Reveal();
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

		// 개발자 창 mapobj reveal. 판 전체 스캐너와 같은 처리
		public int RevealAllMapObjects() => RevealMapObjectSites(null, Vector3.zero, 0f);

		// 스캐너. 반경 0 이면 판 전체, 이후 나올 오브젝트도 발견. source 는 자기 자신 제외
		private int RevealMapObjectSites(MapObjectSite source, Vector3 center, float radius)
		{
			if (radius <= 0f)
				mapObjectsRevealAll = true;
			int revealed = 0;
			float radiusSquared = radius * radius;
			foreach (MapObjectSite site in mapObjectSites)
			{
				if (site == null || site == source || site.IsSpent || site.IsKnown)
					continue;
				Vector3 offset = site.transform.position - center;
				offset.y = 0f;
				if (radius > 0f && offset.sqrMagnitude > radiusSquared)
					continue;
				site.Reveal();
				revealed++;
			}
			return revealed;
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
			mapObjectsRevealAll = false;
		}
	}
}
