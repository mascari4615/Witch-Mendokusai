using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// Hit 시 victim 에게 knockback impulse 적용. UnitMovement.ApplyImpulse.
	/// 구독 boilerplate 는 같은 GO 의 <see cref="DamageReaction"/> 디스패처가 소유.
	///
	/// 디자인:
	/// - Knockback 강도/시간 = caller 가 채운 DamageInfo 값 (per-hit 동적). SO 디폴트 + 런타임 override.
	/// - 방향 = damageInfo.knockbackDirectionOverride 우선, 없으면 damageSource → victim 자동.
	/// - attacker 의 KNOCKBACK_POWER stat 으로 강도 보정 (UPG_6_Knockback 효과).
	/// - victim 의 KNOCKBACK_RESISTANCE stat 은 follow-up 단계 (D)에서 적용.
	/// - DEAD victim 은 skip.
	/// </summary>
	[DisallowMultipleComponent]
	[RequireComponent(typeof(DamageReaction))]
	public class KnockbackFeedback : MonoBehaviour, IDamageReaction
	{
		private UnitObject unitObject;
		private UnitMovement unitMovement;

		private void Awake()
		{
			unitObject = GetComponent<UnitObject>();
			unitMovement = GetComponent<UnitMovement>();
		}

		public void OnDamaged(DamageInfo damageInfo)
		{
			if (damageInfo.knockbackForce <= 0f || damageInfo.knockbackDuration <= 0f)
				return;
			if (unitMovement == null)
				return;
			if (unitObject != null && unitObject.UnitStat[UnitStatType.DEAD] > 0)
				return;

			Vector3 direction = ResolveDirection(damageInfo);
			if (direction.sqrMagnitude < 0.0001f)
				return;
			direction.Normalize();

			float force = damageInfo.knockbackForce * GetAttackerPowerMultiplier(damageInfo);
			unitMovement.ApplyImpulse(direction * force, damageInfo.knockbackDuration);
		}

		private Vector3 ResolveDirection(DamageInfo damageInfo)
		{
			if (damageInfo.knockbackDirectionOverride.HasValue)
			{
				Vector3 directionOverride = damageInfo.knockbackDirectionOverride.Value.ToUnity();
				directionOverride.y = 0f;
				return directionOverride;
			}

			// Domain 안에서만 UnitObject downcast — DomainSDK 의 IDamageSource marker 위.
			if (damageInfo.damageSource is UnitObject sourceUnit == false)
				return Vector3.zero;

			Vector3 fromSourceToVictim = transform.position - sourceUnit.transform.position;
			fromSourceToVictim.y = 0f;
			return fromSourceToVictim;
		}

		// attacker 의 KNOCKBACK_POWER stat 비례 강도 보정. UPG_6_Knockback 레벨 1당 +10% (ValuePerLevel=1, /10f 패턴).
		private static float GetAttackerPowerMultiplier(DamageInfo damageInfo)
		{
			if (damageInfo.damageSource is UnitObject sourceUnit == false)
				return 1f;
			int knockbackPower = sourceUnit.UnitStat[UnitStatType.KNOCKBACK_POWER];
			return 1f + (knockbackPower / 10f);
		}
	}
}
