using System;

namespace WitchMendokusai
{
	// 바닥 아이템 판정의 정본. 엔진 무관
	public static class FloorItemRules
	{
		// 적 정지가 끝나는 시각. 지속 중 다시 먹으면 연장하되 겹쳐 쌓지 않음
		public static float FreezeEndTime(float currentEnd, float now, float seconds)
		{
			if (seconds <= 0f)
				return currentEnd;
			return Math.Max(currentEnd, now + seconds);
		}

		// 보스 정지 시간. 비율 0이면 보스 면역
		public static float BossFreezeSeconds(float seconds, float bossRatio) => Math.Max(0f, seconds) * Math.Min(1f, Math.Max(0f, bossRatio));

		public static bool IsFrozen(float now, float endTime) => now < endTime;

		// 화면 청소 대상. 보스와 정예 제외
		public static bool ClearsTarget(bool isBoss, bool isElite) => isBoss == false && isElite == false;
	}
}
