using System;

namespace WitchMendokusai
{
	// 연속 적중 누적과 처치 시 추가 공격 규칙. 엔진 무관
	// - 연속 적중마다 피해 증가, 빗나가면 0
	// - 처치하면 한 번 더 공격하고 그 공격이 또 처치하면 이어짐. 연쇄 상한이 끊음
	public static class HitComboRules
	{
		public static int AfterHit(int count, int maxCount) => Math.Min(Math.Max(1, maxCount), Math.Max(0, count) + 1);

		public static int AfterMiss() => 0;

		// 지금까지 이어진 적중 수만큼 피해 증가. 첫 적중은 증가 없음
		public static float DamageMultiplier(int count, float bonusPercentPerHit, int maxCount)
		{
			int effective = Math.Min(Math.Max(0, count), Math.Max(0, maxCount));
			return 1f + effective * Math.Max(0f, bonusPercentPerHit) / 100f;
		}

		public static int Apply(int damage, float multiplier) => StatusDetonationRules.Scale(damage, multiplier);

		public static bool CanFollowUp(int followUpsDone, int maxFollowUps) => followUpsDone < maxFollowUps;
	}
}
