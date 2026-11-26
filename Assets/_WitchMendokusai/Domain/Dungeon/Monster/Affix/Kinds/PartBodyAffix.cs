using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 부위 몸 쪽 일. 맞은 피해의 몫을 보스에게 넘기고, 주인 보스가 없어지면 (쓰러짐, 꺼짐) 스스로 거둠. 세우는 쪽은 PartsAffix
	[CreateAssetMenu(fileName = "AF_PartBody", menuName = "WM/Monster/Affix/PartBody")]
	public sealed class PartBodyAffix : MonsterAffix
	{
		public override string KindName => "보스 부위";

		public override void OnHurt(MonsterObject monster, AffixMemory memory, DamageInfo damageInfo)
		{
			if (damageInfo.type == DamageType.StatusTick || BossParts.TryGetBoss(monster, out MonsterObject boss, out float share) == false)
				return;
			int shared = BossParts.SharedDamage(damageInfo.damage, share);
			if (shared <= 0)
				return;
			DamageInfo forwarded = damageInfo;
			forwarded.damage = shared;
			// 강제 피해로 넘김: 보스의 피격 무적을 켜지도 막히지도 않고 (보스를 직접 때린 공격이 안 막힘), 부위에서 이미 깎인 방어력을 다시 안 깎음
			forwarded.ignoreInvincible = true;
			// 부위만 맞았는데 보스가 밀리거나 멈칫하거나 상태 폭발 표시가 두 번 뜨지 않게
			forwarded.knockbackForce = 0f;
			forwarded.knockbackDuration = 0f;
			forwarded.hitstopDuration = 0f;
			forwarded.knockbackDirectionOverride = null;
			forwarded.statusDetonated = false;
			boss.ReceiveDamage(forwarded);
		}

		// 주인 없는 부위 (보스가 풀로 돌아감, 판 정리) 는 남지 않게
		public override void Tick(MonsterObject monster, AffixMemory memory, float deltaTime)
		{
			if (BossParts.TryGetBoss(monster, out _, out _))
				return;
			BossParts.Forget(monster);
			ObjectPoolManager.Instance.Despawn(monster.gameObject);
		}

		public override void OnDeath(MonsterObject monster, AffixMemory memory, Vector3 position) => BossParts.Forget(monster);

		public override void Validate(Monster owner, List<string> errors)
		{
			if (owner.Attack != null)
				errors.Add($"{name}: 부위 {owner.name} 가 따로 공격함. 부위 패턴은 보스의 {nameof(PartGatedMonsterAttack)} 로");
		}
	}
}
