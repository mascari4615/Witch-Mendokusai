namespace WitchMendokusai
{
	/// <summary> 슬라임 왕 패턴이 플레이어에게 주는 피해 한 건 </summary>
	public static class SlimeKingDamage
	{
		public static DamageInfo Make(UnitObject source, int damage) => new()
		{
			type = DamageType.Normal,
			damage = damage,
			damageSource = source,
			equipmentDataId = DamageInfo.NO_DATA_ID,
			skillDataId = DamageInfo.NO_DATA_ID,
		};
	}
}
