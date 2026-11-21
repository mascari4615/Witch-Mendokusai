using System;

namespace WitchMendokusai
{
	// 유닛 피격 무적 규칙. 엔진 무관
	// - 맞은 뒤 잠시 같은 유닛이 다시 안 맞음. 다발 무기가 보스에게 매 프레임 박히지 않게
	// - 값은 유닛 데이터 SO 가 우선, 없으면 종류 기본값
	public static class HitInvincibilityRules
	{
		public const float MAX_SECONDS = 5f;
		// 유닛 데이터가 정하지 않았다는 표시
		public const float NOT_SET = -1f;

		public static float Seconds(float unitSeconds, bool isBoss, float bossDefault, float normalDefault)
		{
			float seconds = unitSeconds >= 0f ? unitSeconds : (isBoss ? bossDefault : normalDefault);
			return Math.Min(MAX_SECONDS, Math.Max(0f, seconds));
		}

		public static bool IsActive(float lastHitTime, float now, float seconds) => seconds > 0f && now - lastHitTime < seconds;
	}
}
