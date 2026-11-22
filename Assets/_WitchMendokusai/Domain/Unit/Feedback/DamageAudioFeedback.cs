using UnityEngine;

namespace WitchMendokusai
{
	[RequireComponent(typeof(DamageReaction))]
	public class DamageAudioFeedback : MonoBehaviour, IDamageReaction, IDeathReaction
	{
		[SerializeField] private string hitEventPath = "event:/SFX/Monster/Hit";
		[SerializeField] private string dieEventPath = "event:/SFX/Monster/Die";
		[SerializeField, Min(1), Tooltip("같은 소리 (경로) 를 모든 개체 통틀어 동시에 몇 개까지 낼지. 차 있으면 새 소리를 건너뜀")] private int maxVoices = 6;
		[SerializeField, Min(0f), Tooltip("같은 소리를 다시 낼 최소 간격 초 (실시간)")] private float minIntervalSeconds = 0.03f;

		public void OnDamaged(DamageInfo damageInfo)
		{
			if (string.IsNullOrEmpty(hitEventPath) == false)
			{
				SfxVoiceGate.TryPlay(hitEventPath, transform.position, maxVoices, minIntervalSeconds);
			}
		}

		public void OnDeath()
		{
			// 등장과 사망 연출이 있는 몬스터는 MonsterLifeFx 가 짧은 공용 사망음 (CombatSfx MonsterPop) 을 냄. 둘이 겹치지 않게 이쪽은 양보
			if (TryGetComponent(out MonsterLifeFx monsterLifeFx) && monsterLifeFx.PlaysDeathSfx)
				return;
			if (string.IsNullOrEmpty(dieEventPath) == false)
			{
				SfxVoiceGate.TryPlay(dieEventPath, transform.position, maxVoices, minIntervalSeconds);
			}
		}
	}
}
