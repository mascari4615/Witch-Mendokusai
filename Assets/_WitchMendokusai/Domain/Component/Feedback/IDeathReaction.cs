namespace WitchMendokusai
{
	/// <summary>
	/// UnitHealth.OnDied 시 호출되는 반응 1개. 같은 GameObject 의
	/// <see cref="DamageReaction"/> 디스패처가 단일 구독 소유.
	/// death 채널을 쓰는 반응(사망 SFX / VFX 등)만 구현한다.
	/// </summary>
	public interface IDeathReaction
	{
		void OnDeath();
	}
}
