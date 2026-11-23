using UnityEngine;

namespace WitchMendokusai
{
	// 부품이 탄이나 장판을 하나 더 낼 때의 공용 길. 같은 판정 주인 (context) 과 피해 보너스를 물려받음
	// 호출한 쪽이 방향을 정한 뒤 SetActive(true)
	public static class SkillSpawn
	{
		public static GameObject Spawn(ObjectPoolManager objectPoolManager, GameObject prefab, Vector3 position, Quaternion rotation, SkillContext context, int damageBonus)
		{
			GameObject spawned = objectPoolManager.Spawn(prefab, position, rotation);
			spawned.transform.localScale = prefab.transform.localScale;
			if (spawned.TryGetComponent(out SkillObject skillObject))
				skillObject.InitAttack(context);
			DamagingObject damagingObject = spawned.GetComponentInChildren<DamagingObject>(true);
			if (damagingObject != null)
				damagingObject.SetDamageBonus(damageBonus);
			return spawned;
		}

		// 카드로 켜지는 부품의 켜짐 확인. NONE 이면 늘 켜짐
		public static int Stacks(SkillContext context, UnitStatType stat)
		{
			if (stat == UnitStatType.NONE)
				return 1;
			return context != null && context.User != null ? context.User.UnitStat[stat] : 0;
		}
	}
}
