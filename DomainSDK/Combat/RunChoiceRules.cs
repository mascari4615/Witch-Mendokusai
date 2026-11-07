using System;

namespace WitchMendokusai
{
	// 판 시작 리롤, 스킵, 차단 횟수와 진화 상자 확정 규칙
	public static class RunChoiceRules
	{
		public static int StartCharges(int baseCharges, int upgradeBonus) => Math.Max(0, baseCharges) + Math.Max(0, upgradeBonus);

		// guaranteed면 확률과 무관하게 상자. roll은 0 이상 1 미만
		public static bool DropsEliteChest(bool guaranteed, float roll, float chestChance) => guaranteed || roll < chestChance;
	}
}
