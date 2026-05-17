namespace WitchMendokusai
{
	/// <summary>
	/// UnitHealth.OnTakeDamage 한 hit 당 호출되는 반응 1개. 구독 boilerplate 는
	/// 같은 GameObject 의 <see cref="DamageReaction"/> 디스패처가 단일 소유 —
	/// 구현체는 반응 본체만 보유한다(Awake/OnEnable 직접 구독 X).
	/// DamageInfo = caller-fill struct (dual 구조: SO 디폴트 + per-hit override).
	/// </summary>
	public interface IDamageReaction
	{
		void OnDamaged(DamageInfo damageInfo);
	}
}
