using System;

namespace WitchMendokusai
{
	// 레벨 기준 성장 카드의 정본 (알리사 정돈된 손, 무기고 마무리, 닌자 공통 마무리)
	// 카드를 받은 뒤 레벨업마다 대상 스탯이 조금씩 오르고 상한에서 멈춤. 처치 수 기준은 판마다 처치 속도가 2배 넘게 갈려 레벨로 바꿈 (사용자 결정 2026-11-22 22:07)
	public static class LevelGrowthRules
	{
		// 이번 레벨업에 더할 양. stacks 는 받은 카드 수, granted 는 이번 판에 이미 더한 양, cap 이 0 이하면 상한 없음
		public static int Grant(int granted, int stacks, int amountPerLevel, int cap)
		{
			if (stacks <= 0 || amountPerLevel <= 0)
				return 0;
			int amount = stacks * amountPerLevel;
			if (cap <= 0)
				return amount;
			return Math.Max(0, Math.Min(amount, cap - Math.Max(0, granted)));
		}
	}
}
