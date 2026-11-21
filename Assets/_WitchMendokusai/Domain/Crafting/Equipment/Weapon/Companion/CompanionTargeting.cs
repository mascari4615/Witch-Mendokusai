using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 소환수와 동료가 쓰는 적 탐색. 기준점에서 반경 안 가장 가까운 활성 몬스터
	public static class CompanionTargeting
	{
		public static GameObject NearestMonster(Vector3 origin, float radius)
		{
			List<GameObject> monsters = ObjectBufferManager.GetObjects(ObjectType.Monster);
			GameObject nearest = null;
			float bestSqr = radius * radius;
			foreach (GameObject monster in monsters)
			{
				if (monster == null || monster.activeInHierarchy == false)
					continue;
				Vector3 offset = monster.transform.position - origin;
				offset.y = 0f;
				float sqr = offset.sqrMagnitude;
				if (sqr < bestSqr)
				{
					bestSqr = sqr;
					nearest = monster;
				}
			}
			return nearest;
		}

		// 공용 공격속도 카드와 쿨타임 감소로 빨라지는 쿨의 진행 배율. 1 이면 기본
		public static float CooldownRate(UnitStat stat, float reductionPerStack, float minRatio)
			=> 1f / AttackCadence.WeaponInterval(1f, 0, stat[UnitStatType.ATTACK_SPEED_BONUS], reductionPerStack, minRatio, stat[UnitStatType.COOLTIME_BONUS]);

		public static Vector3 Flat(Vector3 position)
		{
			position.y = 0f;
			return position;
		}
	}
}
