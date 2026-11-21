namespace WitchMendokusai
{
	// 플레이어 피해를 먼저 받는 보호막. true 를 돌려주면 그 피해는 체력에 닿지 않음
	public interface IDamageAbsorber
	{
		bool TryAbsorb(DamageInfo damageInfo);
	}
}
