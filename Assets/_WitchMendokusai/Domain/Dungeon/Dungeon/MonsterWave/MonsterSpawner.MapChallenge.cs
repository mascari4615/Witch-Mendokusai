using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public partial class MonsterSpawner
	{
		private GameObject challengeSite;
		private GameObject crossingMarker;
		private SurvivalEventEntry crossingEntry;
		private Vector3 crossingCenter;
		private Vector3 crossingForward;
		private bool crossingReady;
		private Coroutine pendingSiteInitialization;
		private int mapChallengeGeneration;
		private readonly List<SurvivalCrossingMember> crossingMembers = new();
		private SurvivalMapChallengeConfig MapConfig => eventTable != null ? eventTable.MapChallenge : null;

		private void InitMapChallenge()
		{
			StopMapChallenge();
			SurvivalMapChallengeConfig config = MapConfig;
			if (config == null || config.EnableSite == false || IsMapConfigValid(config) == false) return;
			pendingSiteInitialization = StartCoroutine(InitializeSiteAfterPhysics(mapChallengeGeneration, config));
		}

		private IEnumerator InitializeSiteAfterPhysics(int generation, SurvivalMapChallengeConfig config)
		{
			// GroundGenerator.FixedUpdate의 최초 지면 생성과 동기화 이후 배치
			yield return new WaitForFixedUpdate();
			if (generation != mapChallengeGeneration) yield break;
			pendingSiteInitialization = null;
			if (dungeonManager.IsDungeon == false || isActiveAndEnabled == false || MapConfig != config || config.EnableSite == false || IsMapConfigValid(config) == false)
			{
				Debug.Log("[MapChallenge] Deferred site initialization cancelled: run or config changed.");
				yield break;
			}
			CreateMapChallengeSite(config);
		}

		private void CreateMapChallengeSite(SurvivalMapChallengeConfig config)
		{
			SurvivalEventEntry template = FindSiteTemplate();
			if (template == null)
			{
				Debug.LogWarning("[MapChallenge] Site unavailable: guaranteed elite template missing.");
				return;
			}
			if (playerProvider.CurrentObject == null || TryMapGround(playerProvider.Current.transform.position, out Vector3 playerGround) == false)
			{
				Debug.LogWarning("[MapChallenge] Site unavailable after physics tick: player or supporting ground missing.");
				return;
			}
			int missingGroundCount = 0;
			int blockedSiteCount = 0;
			int blockedRouteCount = 0;
			for (int attempt = 0; attempt < config.SitePlacementAttempts; attempt++)
			{
				Vector2 offset = Random.insideUnitCircle.normalized * config.SiteDistance;
				Vector3 candidate = playerProvider.Current.transform.position + new Vector3(offset.x, 0f, offset.y);
				if (TryMapGround(candidate, out Vector3 ground) == false) { missingGroundCount++; continue; }
				float radius = PlayerInteraction.InteractionDistance;
				if (Physics.CheckSphere(ground + Vector3.up * radius, radius, config.ObstacleMask, QueryTriggerInteraction.Ignore)) { blockedSiteCount++; continue; }
				if (IsPlayerRouteClear(playerGround, ground) == false) { blockedRouteCount++; continue; }
				challengeSite = new GameObject("정예 도전, 처치하면 상자");
				challengeSite.SetActive(false);
				challengeSite.transform.position = ground;
				SurvivalChallengeSite site = challengeSite.AddComponent<SurvivalChallengeSite>();
				site.Init(TryStartSiteChallenge);
				challengeSite.AddComponent<InteractiveObject>();
				LineRenderer marker = AddMapLine(challengeSite, config.SiteColor, config.MarkerWidth);
				marker.positionCount = 4;
				marker.loop = true;
				marker.SetPosition(0, ground + Vector3.forward * radius);
				marker.SetPosition(1, ground + Vector3.right * radius);
				marker.SetPosition(2, ground + Vector3.back * radius);
				marker.SetPosition(3, ground + Vector3.left * radius);
				challengeSite.SetActive(true);
				Debug.Log($"[MapChallenge] Site created after physics tick: attempt {attempt + 1}/{config.SitePlacementAttempts}, missing ground {missingGroundCount}, blocked site {blockedSiteCount}, blocked route {blockedRouteCount}.");
				return;
			}
			Debug.LogWarning($"[MapChallenge] Site candidates exhausted: attempts {config.SitePlacementAttempts}, missing ground {missingGroundCount}, blocked site {blockedSiteCount}, blocked route {blockedRouteCount}.");
		}

		private SurvivalEventEntry FindSiteTemplate()
		{
			if (eventTable == null) return null;
			foreach (SurvivalEventEntry entry in eventTable.Events)
				if (entry.Type == SurvivalEventType.Elite && entry.GuaranteedChest && entry.Monster != null) return entry;
			return null;
		}

		private bool TryStartSiteChallenge(Vector3 position)
		{
			if (dungeonManager.IsDungeon == false || playerProvider.CurrentObject == null || playerProvider.CurrentObject.IsAlive == false) return false;
			SurvivalEventEntry template = FindSiteTemplate();
			if (template == null) return false;
			SurvivalRunRules rules = dungeonManager.RunRules;
			if (WaveDensityRules.AllowedCount(SpawnClass.Priority, CurrentMonsterCount, 1, rules.EnemySoftCap, rules.EnemyHardCap) == 0) return false;
			Vector3 offset = position - playerProvider.Current.transform.position;
			offset.y = 0f;
			Vector3 spawn = position + offset.normalized * PlayerInteraction.InteractionDistance;
			if (TryMapGround(spawn, out Vector3 ground) == false) return false;
			StartCoroutine(SpawnMonsterAt(template.Monster, ground, eventTable.Elite, true));
			return true;
		}

		private void PrepareCrossing(SurvivalEventEntry entry)
		{
			SurvivalMapChallengeConfig config = MapConfig;
			if (config == null || config.EnableCrossing == false || entry.Type != SurvivalEventType.Swarm || Mathf.Approximately(entry.Time, config.CrossingEventTime) == false) return;
			if (IsMapConfigValid(config) == false || entry.Monster == null || entry.Monster.Prefab == null)
			{
				Debug.LogWarning("[MapChallenge] Invalid crossing config, keeping original Swarm.");
				return;
			}
			CapsuleCollider capsule = entry.Monster.Prefab.GetComponent<CapsuleCollider>();
			if (capsule == null)
			{
				Debug.LogWarning("[MapChallenge] Crossing capsule missing, keeping original Swarm.");
				return;
			}
			crossingEntry = entry;
			crossingReady = false;
			float enemyRadius = capsule.radius * Mathf.Max(entry.Monster.Prefab.transform.lossyScale.x, entry.Monster.Prefab.transform.lossyScale.z);
			float spacing = Mathf.Max(config.CrossingSpacing, enemyRadius * 2f + config.EscapePadding);
			float halfWidth = spacing * Mathf.Max(0, entry.Count - 1) * 0.5f + enemyRadius;
			float playerRadius = playerProvider.CurrentObject.UnitMovement.BodyRadius;
			float requiredLead = (halfWidth + playerRadius + config.EscapePadding) / config.MinimumEscapeSpeed + config.MinimumEscapeSpeed / (2f * config.EscapeAcceleration) + config.ReactionSeconds + (float)DungeonContext.TimeUpdateInterval.TotalSeconds;
			float currentSpeed = (playerProvider.CurrentObject.UnitStat[UnitStatType.MOVEMENT_SPEED] + playerProvider.CurrentObject.UnitStat[UnitStatType.MOVEMENT_SPEED_BONUS]) / InputContributor.STAT_PER_UNIT_PER_SECOND;
			if (entry.AnnounceLead < requiredLead || currentSpeed * config.EscapeSpeedMultiplier < config.MinimumEscapeSpeed) { CancelCrossing("insufficient escape time or speed"); return; }
			crossingCenter = playerProvider.Current.transform.position;
			if (TryMapGround(crossingCenter, out crossingCenter) == false) { CancelCrossing("player ground missing"); return; }
			crossingForward = playerProvider.CurrentObject.UnitMovement.MoveDirectionLocal;
			crossingForward.y = 0f;
			if (crossingForward.sqrMagnitude == 0f) crossingForward = Vector3.forward;
			crossingForward.Normalize();
			Vector3 side = Vector3.Cross(Vector3.up, crossingForward);
			Vector3 exit = crossingCenter + crossingForward * (halfWidth + playerRadius + config.EscapePadding);
			if (TryMapGround(exit, out Vector3 exitGround) == false || IsPlayerRouteClear(crossingCenter, exitGround) == false) { CancelCrossing("escape route blocked or unsupported"); return; }
			if (TryMapGround(crossingCenter + side * eventTable.SwarmRadius, out Vector3 start) == false || TryMapGround(crossingCenter - side * eventTable.SwarmRadius, out Vector3 end) == false) { CancelCrossing("crossing ground missing"); return; }
			crossingMarker = new GameObject("Crossing Warning");
			LineRenderer marker = AddMapLine(crossingMarker, config.CrossingColor, halfWidth * 2f);
			marker.positionCount = 2;
			marker.SetPosition(0, start);
			marker.SetPosition(1, end);
			crossingReady = true;
		}

		private bool IsMapConfigValid(SurvivalMapChallengeConfig config)
		{
			return config.MarkerMaterial != null && config.GroundMask.value != 0 && config.ObstacleMask.value != 0
				&& config.MinimumEscapeSpeed > 0f && config.EscapeAcceleration > 0f && config.CrossingLifetime > 0f
				&& config.RouteSampleSpacing > 0f && config.RouteMaxSamples > 0 && config.GroundProbeHeight > 0f;
		}

		private void CancelCrossing(string reason)
		{
			Debug.LogWarning("[MapChallenge] Crossing safely cancelled: " + reason);
		}

		private bool TryStartCrossing(SurvivalEventEntry entry)
		{
			if (entry != crossingEntry) return false;
			if (crossingMarker != null) Destroy(crossingMarker);
			if (MapConfig == null || MapConfig.EnableCrossing == false || IsMapConfigValid(MapConfig) == false)
			{
				crossingEntry = null;
				Debug.LogWarning("[MapChallenge] Crossing disabled or invalid before start, keeping original Swarm.");
				return false;
			}
			if (crossingReady == false)
			{
				EventBusBridge.Publish(new SurvivalEventStartedEvent { Type = entry.Type, Monster = entry.Monster, Count = 0 });
				return true;
			}
			SurvivalRunRules rules = dungeonManager.RunRules;
			int count = WaveDensityRules.AllowedCount(SpawnClass.Event, CurrentMonsterCount, entry.Count, rules.EnemySoftCap, rules.EnemyHardCap);
			CapsuleCollider capsule = entry.Monster.Prefab.GetComponent<CapsuleCollider>();
			float radius = capsule.radius * Mathf.Max(entry.Monster.Prefab.transform.lossyScale.x, entry.Monster.Prefab.transform.lossyScale.z);
			float spacing = Mathf.Max(MapConfig.CrossingSpacing, radius * 2f + MapConfig.EscapePadding);
			Vector3 side = Vector3.Cross(Vector3.up, crossingForward);
			int spawnedCount = 0;
			for (int index = 0; index < count; index++)
			{
				Vector3 offset = crossingForward * ((index - (count - 1) * 0.5f) * spacing);
				Vector3 position = crossingCenter + side * eventTable.SwarmRadius + offset;
				if (TryMapGround(position, out Vector3 ground) == false) continue;
				StartCoroutine(SpawnCrossingMember(entry.Monster, ground, crossingCenter - side * eventTable.SwarmRadius + offset, -side));
				spawnedCount++;
			}
			if (spawnedCount > 0) SurvivalFeel.Instance.OnSwarmStarted(crossingCenter);
			EventBusBridge.Publish(new SurvivalEventStartedEvent { Type = entry.Type, Monster = entry.Monster, Count = spawnedCount });
			return true;
		}

		private IEnumerator SpawnCrossingMember(Monster monster, Vector3 position, Vector3 destination, Vector3 direction)
		{
			pendingSpawns++;
			GameObject circle = objectPoolManager.Spawn(spawnCirclePrefab);
			circle.transform.position = position;
			circle.SetActive(true);
			ObjectBufferManager.AddObject(ObjectType.SpawnCircle, circle);
			yield return new WaitForSeconds(spawnDelay);
			pendingSpawns = Mathf.Max(0, pendingSpawns - 1);
			GameObject instance = objectPoolManager.Spawn(monster.Prefab);
			MonsterObject unit = instance.GetComponent<MonsterObject>();
			instance.transform.position = position;
			unit.SetThreat(dungeonManager.CurThreat);
			unit.Init(monster);
			instance.SetActive(true);
			SurvivalCrossingMember member = instance.GetComponent<SurvivalCrossingMember>();
			if (member == null) member = instance.AddComponent<SurvivalCrossingMember>();
			member.Begin(unit, direction, destination, MapConfig.CrossingLifetime);
			crossingMembers.Add(member);
		}

		private bool TryMapGround(Vector3 position, out Vector3 ground)
		{
			SurvivalMapChallengeConfig config = MapConfig;
			if (Physics.Raycast(position + Vector3.up * config.GroundProbeHeight, Vector3.down, out RaycastHit hit, config.GroundProbeHeight * 2f, config.GroundMask, QueryTriggerInteraction.Ignore))
			{
				ground = hit.point + Vector3.up * config.GroundLift;
				return true;
			}
			ground = position;
			return false;
		}

		private LineRenderer AddMapLine(GameObject owner, Color color, float width)
		{
			owner.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
			LineRenderer line = owner.AddComponent<LineRenderer>();
			line.alignment = LineAlignment.TransformZ;
			line.useWorldSpace = true;
			line.sharedMaterial = MapConfig.MarkerMaterial;
			line.startColor = color;
			line.endColor = color;
			line.startWidth = width;
			line.endWidth = width;
			line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			line.receiveShadows = false;
			return line;
		}

		private void StopMapChallenge()
		{
			mapChallengeGeneration++;
			if (pendingSiteInitialization != null)
			{
				StopCoroutine(pendingSiteInitialization);
				pendingSiteInitialization = null;
			}
			if (challengeSite != null) { challengeSite.SetActive(false); Destroy(challengeSite); }
			if (crossingMarker != null) { crossingMarker.SetActive(false); Destroy(crossingMarker); }
			foreach (SurvivalCrossingMember member in crossingMembers)
				if (member != null && member.IsCrossing) member.gameObject.SetActive(false);
			crossingMembers.Clear();
			crossingEntry = null;
			crossingReady = false;
		}
	}
}
