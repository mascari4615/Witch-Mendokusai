using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum DollPassiveKind
	{
		// 피격할 때마다 스탯 상승, 판 내내 유지
		HitStack,
		// 레벨 구간에 도달할 때마다 스탯 상승
		LevelTiers,
	}

	[Serializable]
	public struct DollPassiveGrant
	{
		public UnitStatType stat;
		public int amount;
	}

	// 인형 고유 패시브 한 칸. 규칙은 종류 하나, 수치는 이 자산에
	[CreateAssetMenu(fileName = nameof(DollPassiveData), menuName = "WM/Variable/" + nameof(DollPassiveData))]
	public class DollPassiveData : ScriptableObject
	{
		[field: SerializeField] public string DisplayName { get; private set; }
		[field: SerializeField, TextArea] public string Description { get; private set; }
		[field: SerializeField] public DollPassiveKind Kind { get; private set; }

		[field: Header("HitStack")]
		[field: SerializeField] public UnitStatType HitStat { get; private set; } = UnitStatType.DAMAGE_BONUS;
		[field: SerializeField, Min(0)] public int PercentPerHit { get; private set; } = 3;
		// 0 이하면 무제한
		[field: SerializeField, Min(0)] public int MaxStacks { get; private set; } = 20;

		[field: Header("LevelTiers")]
		[field: SerializeField] public List<int> TierLevels { get; private set; } = new();
		// 구간 하나에 도달할 때마다 받는 스탯
		[field: SerializeField] public List<DollPassiveGrant> TierGrants { get; private set; } = new();
	}
}
