using UnityEngine;

namespace WitchMendokusai
{
	// 적 정지 상태. 일반 적과 보스의 끝나는 시각을 따로 보관
	public static class EnemyFreezeState
	{
		private static float normalEnd;
		private static float bossEnd;

		public static void Start(float seconds, float bossRatio)
		{
			float now = Time.time;
			normalEnd = FloorItemRules.FreezeEndTime(normalEnd, now, seconds);
			bossEnd = FloorItemRules.FreezeEndTime(bossEnd, now, FloorItemRules.BossFreezeSeconds(seconds, bossRatio));
		}

		public static bool IsFrozen(bool isBoss) => FloorItemRules.IsFrozen(Time.time, isBoss ? bossEnd : normalEnd);

		public static float RemainingSeconds(bool isBoss) => Mathf.Max(0f, (isBoss ? bossEnd : normalEnd) - Time.time);
		public static float NormalRemainingSeconds => RemainingSeconds(false);
		public static float BossRemainingSeconds => RemainingSeconds(true);

		public static void Reset()
		{
			normalEnd = 0f;
			bossEnd = 0f;
		}
	}
}
