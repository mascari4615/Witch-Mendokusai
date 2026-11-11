using UnityEngine;

namespace WitchMendokusai
{
	public partial class MonsterSpawner
	{
		private readonly Collider[] mapRouteOverlaps = new Collider[16];
		private readonly RaycastHit[] mapRouteHits = new RaycastHit[16];

		private bool IsPlayerRouteClear(Vector3 startGround, Vector3 endGround)
		{
			SurvivalMapChallengeConfig config = MapConfig;
			CapsuleCollider capsule = playerProvider.CurrentObject.GetComponent<CapsuleCollider>();
			if (capsule == null || capsule.direction != 1) return false;
			Transform body = capsule.transform;
			Vector3 axis = body.up;
			if (Mathf.Approximately(Mathf.Abs(Vector3.Dot(axis, Vector3.up)), 1f) == false) return false;
			Vector3 scale = body.lossyScale;
			float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
			float height = Mathf.Max(capsule.height * Mathf.Abs(scale.y), radius * 2f);
			if (radius <= 0f) return false;
			Vector3 center = body.TransformPoint(capsule.center);
			Vector3 halfSegment = axis * (height * 0.5f - radius);
			Vector3 bottom = center - halfSegment;
			Vector3 top = center + halfSegment;
			float footHeight = Mathf.Min(bottom.y, top.y) - radius;
			if (Mathf.Abs(startGround.y - footHeight) > config.RouteGroundDeviation + config.GroundLift) return false;
			// 바닥 접촉 허용을 위한 contact offset만큼의 수직 여유
			float lift = Mathf.Max(config.GroundLift, capsule.contactOffset);
			Vector3 bodyOffset = new Vector3(startGround.x - body.position.x, startGround.y + lift - footHeight, startGround.z - body.position.z);
			bottom += bodyOffset;
			top += bodyOffset;
			float spacing = Mathf.Min(config.RouteSampleSpacing, radius);
			int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(startGround, endGround) / spacing));
			if (samples > config.RouteMaxSamples) return false;
			Vector3 previousGround = startGround;
			for (int index = 0; index <= samples; index++)
			{
				Vector3 expected = Vector3.Lerp(startGround, endGround, (float)index / samples);
				if (TryMapGround(expected, out Vector3 ground) == false || Mathf.Abs(ground.y - expected.y) > config.RouteGroundDeviation) return false;
				Vector3 delta = ground - previousGround;
				int overlaps = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, mapRouteOverlaps, config.ObstacleMask, QueryTriggerInteraction.Ignore);
				if (overlaps == mapRouteOverlaps.Length) return false;
				for (int overlapIndex = 0; overlapIndex < overlaps; overlapIndex++)
					if (IsPlayerRouteCollider(mapRouteOverlaps[overlapIndex]) == false) return false;
				float distance = delta.magnitude;
				if (distance > 0f)
				{
					int hits = Physics.CapsuleCastNonAlloc(bottom, top, radius, delta / distance, mapRouteHits, distance, config.ObstacleMask, QueryTriggerInteraction.Ignore);
					if (hits == mapRouteHits.Length) return false;
					for (int hitIndex = 0; hitIndex < hits; hitIndex++)
						if (IsPlayerRouteCollider(mapRouteHits[hitIndex].collider) == false) return false;
				}
				bottom += delta;
				top += delta;
				previousGround = ground;
			}
			// 마지막 도착 자세의 초기 겹침 검사
			int finalOverlaps = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, mapRouteOverlaps, config.ObstacleMask, QueryTriggerInteraction.Ignore);
			if (finalOverlaps == mapRouteOverlaps.Length) return false;
			for (int index = 0; index < finalOverlaps; index++)
				if (IsPlayerRouteCollider(mapRouteOverlaps[index]) == false) return false;
			return true;
		}

		private bool IsPlayerRouteCollider(Collider collider)
		{
			return collider != null && collider.transform.IsChildOf(playerProvider.CurrentObject.transform);
		}
	}
}
