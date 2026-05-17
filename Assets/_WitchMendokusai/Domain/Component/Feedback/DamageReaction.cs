using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// UnitHealth 의 OnTakeDamage / OnDied 를 *단일* 구독해, 같은 GameObject 의
	/// 모든 <see cref="IDamageReaction"/> / <see cref="IDeathReaction"/> 로 디스패치한다.
	///
	/// 기존 Damage*Feedback 6종이 각자 반복하던 구독 boilerplate
	/// (Awake GetComponent&lt;UnitHealth&gt; + OnEnable/OnDisable +=/-=)를 흡수.
	/// 반응 본체는 각 컴포넌트에 잔존 — 디스패처는 *구독 seam* 만 소유한다(Deep Modules).
	///
	/// 호출 순서 = GameObject 의 컴포넌트 순서(prefab 결정적, 디자이너 reorder 가능).
	/// FastFail — 반응 예외를 삼키지 않는다.
	/// </summary>
	[DisallowMultipleComponent]
	[RequireComponent(typeof(UnitHealth))]
	public class DamageReaction : MonoBehaviour
	{
		private UnitHealth unitHealth;
		private IDamageReaction[] damageReactions;
		private IDeathReaction[] deathReactions;

		private void Awake()
		{
			unitHealth = GetComponent<UnitHealth>();
			damageReactions = GetComponents<IDamageReaction>();
			deathReactions = GetComponents<IDeathReaction>();
		}

		private void OnEnable()
		{
			unitHealth.OnTakeDamage += DispatchDamaged;
			unitHealth.OnDied += DispatchDeath;
		}

		private void OnDisable()
		{
			unitHealth.OnTakeDamage -= DispatchDamaged;
			unitHealth.OnDied -= DispatchDeath;
		}

		private void DispatchDamaged(DamageInfo damageInfo)
		{
			for (int i = 0; i < damageReactions.Length; i++)
			{
				damageReactions[i].OnDamaged(damageInfo);
			}
		}

		private void DispatchDeath()
		{
			for (int i = 0; i < deathReactions.Length; i++)
			{
				deathReactions[i].OnDeath();
			}
		}
	}
}
