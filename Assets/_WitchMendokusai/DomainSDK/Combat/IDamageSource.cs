namespace WitchMendokusai
{
	/// <summary>
	/// DamageInfo.damageSource 의 marker. Domain (UnitObject 등) 가 구현.
	/// DomainSDK 가 Domain SO/MonoBehaviour 직접 의존 안 하도록 추상화.
	///
	/// caller 측에서 attacker 의 추가 정보 (stat / position) 가 필요하면
	/// downcast (`if (damageInfo.damageSource is UnitObject sourceUnit)`) 패턴.
	/// 모드/UGC 의 새 attacker 박을 자리 — 자체 IDamageSource 구현 후 DamageInfo 채우면 hit 발생.
	/// </summary>
	public interface IDamageSource { }
}
