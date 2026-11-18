using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 위험도와 보상의 연결 자리. 위험도 구간을 처음 깨면 받는 보상 목록
	/// 값은 사용자 결정 전. 던전에 이 에셋이 없으면 설정 창에 "보상 규칙 미정"
	/// 지급과 최고 위험도 기록은 아직 없음 (표시만)
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
	}
}
