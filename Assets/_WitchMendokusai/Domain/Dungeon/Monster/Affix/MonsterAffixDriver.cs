using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 몬스터 몸 하나에서 특징들 (MonsterAffix) 을 돌림. MonsterObject 가 켜질 때 Attach, 쓰러질 때 NotifyDeath (몸을 끄기 전), 꺼지면 스스로 멈춤
	// 특징이 없는 몬스터에는 붙지 않음. 풀에서 다시 켜지면 기억을 비우고 색을 다시 입힘. 정예가 되어 색이 바뀌면 ReapplyTint
	// 얼었거나 (기절, 빙결, 적 정지) 숨은 동안에는 매 프레임 일과 맞을 때 일을 쉼. 쓰러질 때 일은 그래도 함
	public sealed class MonsterAffixDriver : MonoBehaviour
	{
		private readonly List<AffixMemory> memories = new();
		private MonsterObject monster;
		private IReadOnlyList<MonsterAffix> affixes;
		private Color tint = Color.white;
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

		/// <summary>몸 색을 누가 새로 정한 뒤 (정예) 특징 색을 다시 곱함</summary>
		public static void ReapplyTint(MonsterObject monsterObject)
		{
			if (monsterObject.TryGetComponent(out MonsterAffixDriver driver) && driver.affixes != null)
				monsterObject.SpriteRenderer.color *= driver.tint;
		}

		/// <summary>MonsterObject 가 쓰러짐을 처리하는 맨 앞에서 부름. 몸이 꺼지기 전이라 자리가 맞음</summary>
		public static void NotifyDeath(MonsterObject monsterObject)
		{
			if (monsterObject.TryGetComponent(out MonsterAffixDriver driver))
				driver.HandleDeath();
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
				isListening = true;
			}
			while (memories.Count < list.Count)
				memories.Add(new AffixMemory());
			tint = Color.white;
			for (int i = 0; i < list.Count; i++)
			{
				memories[i].Reset(monster.transform.position);
				// 빈 칸은 검사 (Dungeon.ValidateAffixes) 가 알림. 여기서는 건너뜀 (소환, 분열 자식은 물결 검사 밖)
				if (list[i] != null)
					tint *= list[i].Tint;
			}
			monster.SpriteRenderer.color *= tint;
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i] != null)
					list[i].OnSpawn(monster, memories[i]);
			}
		}

		private void OnDestroy()
		{
			if (isListening && monster != null && monster.Health != null)
				monster.Health.OnTakeDamage -= HandleHurt;
		}

		private bool IsRunning => affixes != null && isActiveAndEnabled && monster.IsAlive && monster.IsActionSuspended == false && monster.IsHidden == false;

		private void Update()
		{
			if (IsRunning == false)
				return;
			float deltaTime = Time.deltaTime;
			for (int i = 0; i < affixes.Count; i++)
			{
				if (affixes[i] != null)
					affixes[i].Tick(monster, memories[i], deltaTime);
			}
		}

		private void HandleHurt(DamageInfo damageInfo)
		{
			if (IsRunning == false)
				return;
			for (int i = 0; i < affixes.Count; i++)
			{
				if (affixes[i] != null)
					affixes[i].OnHurt(monster, memories[i], damageInfo);
			}
		}

		private void HandleDeath()
		{
			if (affixes == null || isActiveAndEnabled == false)
				return;
			Vector3 position = monster.transform.position;
			for (int i = 0; i < affixes.Count; i++)
			{
				if (affixes[i] != null)
					affixes[i].OnDeath(monster, memories[i], position);
			}
		}
	}
}
