using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	public struct DamageInfo
	{
		// DomainSDK 자체 const — Domain 의 DataSO.NONE_ID 의존 회피 (단방향 정합).
		public const int NO_DATA_ID = -1;

		public int damage;
		public DamageType type;
		public IDamageSource damageSource;
		// DataSO.ID 매핑 — NO_DATA_ID 면 없음. caller 가 채움. lookup 은 Domain 사용처 (SOHelper.Get<T>) 책임.
		public int equipmentDataId;
		public int skillDataId;
		public bool ignoreInvincible;

		// Knockback / hit-stop 채널 — caller가 매 hit 채움. 0이면 효과 없음.
		// SO 디폴트(무기/스킬)값 + caller 동적 보정 (charge / 약점 / 페이즈) 둘 다 가능 (dual 구조).
		public float knockbackForce;
		public float knockbackDuration;
		public float hitstopDuration;
		// null이면 damageSource → victim 자동 계산. 폭발/AOE caller가 자기 중심 기반으로 override.
		public Vector3? knockbackDirectionOverride;
	}
}
