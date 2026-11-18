using UnityEngine;
using FMODUnity;

namespace WitchMendokusai
{
	[RequireComponent(typeof(DamageReaction))]
	public class DamageAudioFeedback : MonoBehaviour, IDamageReaction, IDeathReaction
	{
		[SerializeField] private string hitEventPath = "event:/SFX/Monster/Hit";
		[SerializeField] private string dieEventPath = "event:/SFX/Monster/Die";

		public void OnDamaged(DamageInfo damageInfo)
		{
			if (string.IsNullOrEmpty(hitEventPath) == false)
			{
				RuntimeManager.PlayOneShot(hitEventPath, transform.position);
			}
		}

		public void OnDeath()
		{
			// 등장과 사망 연출이 있는 몬스터는 MonsterLifeFx 가 짧은 공용 사망음 (CombatSfx MonsterPop) 을 냄. 둘이 겹치지 않게 이쪽은 양보
			if (TryGetComponent(out MonsterLifeFx monsterLifeFx) && monsterLifeFx.PlaysDeathSfx)
				return;
			if (string.IsNullOrEmpty(dieEventPath) == false)
			{
				RuntimeManager.PlayOneShot(dieEventPath, transform.position);
			}
		}
	}
}
