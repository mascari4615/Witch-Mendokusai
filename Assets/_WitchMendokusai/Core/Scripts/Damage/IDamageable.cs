using UnityEngine;

namespace WitchMendokusai
{
	public enum DamageType
	{
		Normal = 0,
		Critical = 1
	}

	public class DamageContext
	{
		public UnitObject DamageSource { get; set; }
		public EquipmentData EquipmentData { get; set; }
		public SkillData SkillData { get; set; }

		public DamageContext(UnitObject damageSource, EquipmentData equipmentData = null, SkillData skillData = null)
		{
			DamageSource = damageSource;
			EquipmentData = equipmentData;
			SkillData = skillData;
		}
	}

	public struct DamageInfo
	{
		public int damage;
		public DamageType type;
		public UnitObject damageSource;
		public EquipmentData equipmentData;
		public SkillData skillData;
		public bool ignoreInvincible;

		// Knockback / hit-stop 채널 — caller가 매 hit 채움. 0이면 효과 없음.
		// SO 디폴트(무기/스킬)값 + caller 동적 보정 (charge / 약점 / 페이즈) 둘 다 가능.
		public float knockbackForce;
		public float knockbackDuration;
		public float hitstopDuration;
		// null이면 damageSource → victim 자동 계산. 폭발/AOE caller가 자기 중심 기반으로 override 가능.
		public Vector3? knockbackDirectionOverride;

		public DamageInfo(int damage, DamageType type, DamageContext context, bool ignoreInvincible = false)
		{
			this.damage = damage;
			this.type = type;
			this.damageSource = context.DamageSource;
			this.equipmentData = context.EquipmentData;
			this.skillData = context.SkillData;
			this.ignoreInvincible = ignoreInvincible;
			this.knockbackForce = 0f;
			this.knockbackDuration = 0f;
			this.hitstopDuration = 0f;
			this.knockbackDirectionOverride = null;
		}
	}

	public interface IDamageable
	{
		public void ReceiveDamage(DamageInfo damageInfo);
	}
}