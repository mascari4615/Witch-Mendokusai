namespace WitchMendokusai
{
	public enum DamageType
	{
		Normal = 0,
		Critical = 1
	}

	public struct DamageInfo
	{
		public int damage;
		public DamageType type;
		public bool ignoreInvincible;

		public DamageInfo(int damage, DamageType type, bool ignoreInvincible = false)
		{
			this.damage = damage;
			this.type = type;
			this.ignoreInvincible = ignoreInvincible;
		}
	}

	public interface IDamageable
	{
		public void ReceiveDamage(DamageInfo damageInfo);
	}
}