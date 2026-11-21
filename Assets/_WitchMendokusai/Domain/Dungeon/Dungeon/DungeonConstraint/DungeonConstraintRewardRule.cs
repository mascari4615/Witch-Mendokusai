using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 위험도 단계 첫 클리어 보상 (사용자 판정 2026-11-20). 위험도 N 을 처음 깨면 N 이하의 안 받은 구간을 한 번씩
	/// 낮은 구간을 크게, 높은 구간을 작게. 마지막 구간 위는 보상 없이 최고 기록만 (상한 없는 도전)
	/// 던전에 이 에셋이 없으면 보상 없는 순수 도전. 지급은 DungeonManager.EndDungeon, 판정은 DungeonRiskRewardRules
	/// </summary>
	[CreateAssetMenu(fileName = "DCR_", menuName = "WM/Variable/" + nameof(DungeonConstraintRewardRule))]
	public class DungeonConstraintRewardRule : ScriptableObject
	{
		[Serializable]
		public class RiskMilestone
		{
			[Tooltip("이 위험도 이상으로 처음 깨면")]
			[Min(1)] public int Risk = 1;
			public List<RewardInfo> Rewards = new();
		}

		[field: SerializeField] public List<RiskMilestone> Milestones { get; private set; } = new();

		[Tooltip("이 위험도 이상이면 설정 창과 입장 창에 고위험 경고. 0 이면 경고 없음")]
		[field: SerializeField, Min(0)] public int WarningRisk { get; private set; } = 0;

		public bool IsWarning(int risk) => WarningRisk > 0 && risk >= WarningRisk;

		public List<int> MilestoneRisks() => Milestones.ConvertAll(milestone => milestone.Risk);
	}
}
