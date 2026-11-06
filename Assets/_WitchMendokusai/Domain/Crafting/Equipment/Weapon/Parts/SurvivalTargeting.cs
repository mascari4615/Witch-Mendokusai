using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public static class SurvivalTargeting
	{
		public static Transform Nearest(Vector3 origin, float range, List<GameObject> excluded)
		{
			List<GameObject> monsters = ObjectBufferManager.GetObjects(ObjectType.Monster);
			Transform nearest = null;
			float best = range * range;
			for (int i = 0; i < monsters.Count; i++)
			{
				GameObject monster = monsters[i];
				if (excluded != null && excluded.Contains(monster))
					continue;
				float distance = (monster.transform.position - origin).sqrMagnitude;
				if (distance > best)
					continue;
				best = distance;
				nearest = monster.transform;
			}
			return nearest;
		}

		public static Transform RandomInRange(Vector3 origin, float range)
		{
			List<GameObject> monsters = ObjectBufferManager.GetObjects(ObjectType.Monster);
			float rangeSquared = range * range;
			Transform picked = null;
			int seen = 0;
			for (int i = 0; i < monsters.Count; i++)
			{
				GameObject monster = monsters[i];
				if ((monster.transform.position - origin).sqrMagnitude > rangeSquared)
					continue;
				seen++;
				if (Random.Range(0, seen) == 0)
					picked = monster.transform;
			}
			return picked;
		}
	}
}
