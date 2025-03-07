namespace WitchMendokusai
{
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
}