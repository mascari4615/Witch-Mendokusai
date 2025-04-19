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

		public DamageInfo(int damage, DamageType type)
		{
			this.damage = damage;
			this.type = type;
		}
	}

	public interface IDamageable
	{
		public void ReceiveDamage(DamageInfo damageInfo);
	}
}