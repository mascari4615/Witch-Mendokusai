using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 몬스터 몸 하나에서 특징들 (MonsterAffix) 을 돌림. MonsterObject 가 켜질 때 Attach, 꺼지면 스스로 멈춤
	// 특징이 없는 몬스터에는 붙지 않음. 풀에서 다시 켜지면 기억을 비우고 색을 다시 입힘
	public sealed class MonsterAffixDriver : MonoBehaviour
	{
		private readonly List<AffixMemory> memories = new();
		private MonsterObject monster;
		private IReadOnlyList<MonsterAffix> affixes;
		private bool isListening;

		public static void Attach(MonsterObject monsterObject)
		{
			IReadOnlyList<MonsterAffix> list = monsterObject.UnitData.Affixes;
			bool hasAffix = list != null && list.Count > 0;
			if (monsterObject.TryGetComponent(out MonsterAffixDriver driver) == false)
			{
				if (hasAffix == false)
					return;
				driver = monsterObject.gameObject.AddComponent<MonsterAffixDriver>();
			}
			driver.Begin(monsterObject, hasAffix ? list : null);
		}

		private void Begin(MonsterObject monsterObject, IReadOnlyList<MonsterAffix> list)
		{
			monster = monsterObject;
			affixes = list;
			enabled = list != null;
			if (list == null)
				return;
			if (isListening == false)
			{
				monster.Health.OnTakeDamage += HandleHurt;
				monster.Health.OnDied += HandleDeath;
				isListening = true;
			}
			while (memories.Count < list.Count)
				memories.Add(new AffixMemory());
			Color tint = Color.white;
			for (int i = 0; i < list.Count; i++)
			{
				memories[i].Reset(monster.transform.position);
				tint *= list[i].Tint;
			}
			monster.SpriteRenderer.color *= tint;
			for (int i = 0; i < list.Count; i++)
				list[i].OnSpawn(monster, memories[i]);
		}

		private void OnDestroy()
		{
			if (isListening == false || monster == null || monster.Health == null)
				return;
			monster.Health.OnTakeDamage -= HandleHurt;
			monster.Health.OnDied -= HandleDeath;
		}

		private bool IsRunning => affixes != null && isActiveAndEnabled && monster.IsAlive;

		private void Update()
		{
			if (IsRunning == false)
				return;
			float deltaTime = Time.deltaTime;
			for (int i = 0; i < affixes.Count; i++)
				affixes[i].Tick(monster, memories[i], deltaTime);
		}

		private void HandleHurt(DamageInfo damageInfo)
		{
			if (IsRunning == false)
				return;
			for (int i = 0; i < affixes.Count; i++)
				affixes[i].OnHurt(monster, memories[i], damageInfo);
		}

		private void HandleDeath()
		{
			if (affixes == null || isActiveAndEnabled == false)
				return;
			Vector3 position = monster.transform.position;
			for (int i = 0; i < affixes.Count; i++)
				affixes[i].OnDeath(monster, memories[i], position);
		}
	}
}
