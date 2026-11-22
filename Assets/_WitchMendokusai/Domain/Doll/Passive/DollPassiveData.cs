using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum DollPassiveKind
	{
		// 피격할 때마다 스탯 상승, 판 내내 유지
		HitStack = 0,
		// 레벨 구간에 도달할 때마다 스탯 상승
		LevelTiers = 1,
		// 수선: 레벨업 몇 번마다 가장 덜 키운 장비의 MendStats 상승. 정본 DollMendRules
		Mend = 2,
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

		[field: Header("Mend")]
		[field: SerializeField, Tooltip("수선 주기 (레벨업 횟수)"), Min(1)] public int MendInterval { get; private set; } = 3;
		[field: SerializeField, Tooltip("실 카드로 줄여도 이 아래로는 안 감"), Min(1)] public int MendMinInterval { get; private set; } = 1;
		[field: SerializeField, Tooltip("주기를 줄이는 스탯 (실 한 타래)")] public UnitStatType MendIntervalStat { get; private set; } = UnitStatType.NONE;
		[field: SerializeField, Tooltip("한 번에 MendStats 에 더하는 양"), Min(0)] public int MendAmount { get; private set; } = 2;
		[field: SerializeField, Tooltip("한 번의 양을 늘리는 스탯 (튼튼한 바느질)")] public UnitStatType MendAmountStat { get; private set; } = UnitStatType.NONE;
		[field: SerializeField, Tooltip("수선할 때 주인공 머리 위 글자. {0} 장비 이름")] public string MendTextFormat { get; private set; } = "{0} 수선";
	}
}
