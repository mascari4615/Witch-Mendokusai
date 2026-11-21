using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// 위험도 보상 구간 하나의 상태. 설정 창 보상 목록 표시용
	public enum DungeonRiskMilestoneState
	{
		// 지금 고른 위험도로는 못 받음
		Unreached = 0,
		// 지금 고른 위험도로 깨면 처음 받음
		ThisClear = 1,
		// 이전 클리어에서 이미 받음
		Claimed = 2,
	}

	// 위험도 단계 첫 클리어 보상 규칙. 위험도 N 으로 깨면 N 이하의 안 받은 구간을 한 번씩
	// claimedRisks: 이미 받은 구간 위험도. 최고 기록에서 추정하지 않고 따로 저장 (구간을 나중에 넣거나 고쳐도 안 받은 구간은 받음)
	public static class DungeonRiskRewardRules
	{
		// 이번 클리어로 처음 받는 구간 번호 (milestoneRisks 의 인덱스). 안 받았고 이번 위험도 이하
		public static List<int> NewlyReached(IReadOnlyList<int> milestoneRisks, ICollection<int> claimedRisks, int clearedRisk)
		{
			List<int> reached = new List<int>();
			for (int i = 0; i < milestoneRisks.Count; i++)
			{
				int risk = milestoneRisks[i];
				if (risk <= clearedRisk && claimedRisks.Contains(risk) == false)
					reached.Add(i);
			}
			return reached;
		}

		// 최고 클리어 위험도 기록. 표시와 목록 배지용, 지급 판정에는 안 씀
		public static int AfterClear(int best, int clearedRisk) => Math.Max(best, clearedRisk);

		public static DungeonRiskMilestoneState State(int milestoneRisk, ICollection<int> claimedRisks, int selectedRisk)
		{
			if (claimedRisks.Contains(milestoneRisk))
				return DungeonRiskMilestoneState.Claimed;
			return milestoneRisk <= selectedRisk ? DungeonRiskMilestoneState.ThisClear : DungeonRiskMilestoneState.Unreached;
		}

		// 가장 높은 구간 위험도. 이 위의 위험도는 보상 없이 기록만 (상한 없는 도전 영역). 구간이 없으면 0
		public static int TopMilestone(IReadOnlyList<int> milestoneRisks)
		{
			int top = 0;
			foreach (int risk in milestoneRisks)
				top = Math.Max(top, risk);
			return top;
		}
	}
}
