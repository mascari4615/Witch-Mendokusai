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

		public DamageInfo(int damage, DamageType type, DamageContext context, bool ignoreInvincible = false)
		{
			this.damage = damage;
			this.type = type;
			this.damageSource = context.DamageSource;
			this.equipmentData = context.EquipmentData;
			this.skillData = context.SkillData;
			this.ignoreInvincible = ignoreInvincible;
		}
	}

	public interface IDamageable
	{
		public void ReceiveDamage(DamageInfo damageInfo);
	}
}