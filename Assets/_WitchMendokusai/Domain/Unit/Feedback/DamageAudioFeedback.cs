using UnityEngine;

namespace WitchMendokusai
{
	[RequireComponent(typeof(DamageReaction))]
	public class DamageAudioFeedback : MonoBehaviour, IDamageReaction, IDeathReaction
	{
		// 동시 발음 상한과 간격은 CombatSfxTable 의 사건 칸. 적 종류별 소리는 이 사건만 바꾸면 됨
		[SerializeField] private CombatSfxEvent hitSfx = CombatSfxEvent.MonsterHit;
		[SerializeField] private CombatSfxEvent dieSfx = CombatSfxEvent.MonsterDie;

		public void OnDamaged(DamageInfo damageInfo) => CombatSfx.Play(hitSfx, transform.position);

		public void OnDeath()
		{
			// 등장과 사망 연출이 있는 몬스터는 MonsterLifeFx 가 짧은 공용 사망음 (CombatSfx MonsterPop) 을 냄. 둘이 겹치지 않게 이쪽은 양보
			if (TryGetComponent(out MonsterLifeFx monsterLifeFx) && monsterLifeFx.PlaysDeathSfx)
				return;
			CombatSfx.Play(dieSfx, transform.position);
		}
	}
}
