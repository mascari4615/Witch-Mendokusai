namespace WitchMendokusai
{
	// 타격 피해가 정해진 뒤, 대상에게 들어가기 전에 고치는 부품. 상태 폭발, 연속 적중 가산 같은 것
	public interface IDamageModifier
	{
		DamageInfo Modify(DamagingObject source, UnitObject victim, DamageInfo damageInfo);
	}
}
