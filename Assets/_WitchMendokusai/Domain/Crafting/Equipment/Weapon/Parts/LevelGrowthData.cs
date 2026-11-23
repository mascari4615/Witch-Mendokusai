using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 레벨 기준 성장 카드 묶음. 장비 하나가 가짐 (EquipmentData.LevelGrowth). 판정 LevelGrowthRules
	[CreateAssetMenu(fileName = nameof(LevelGrowthData), menuName = "WM/Variable/" + nameof(LevelGrowthData))]
	public class LevelGrowthData : ScriptableObject
	{
		[Serializable]
		public class Entry
		{
			[field: SerializeField, Tooltip("카드가 올리는 켜짐 스탯. 값이 받은 장수")] public UnitStatType CardStat { get; private set; } = UnitStatType.NONE;
			[field: SerializeField, Tooltip("레벨업마다 오르는 스탯")] public UnitStatType TargetStat { get; private set; } = UnitStatType.NONE;
			[field: SerializeField, Min(1)] public int AmountPerLevel { get; private set; } = 1;
			[field: SerializeField, Min(0), Tooltip("이번 판에 더하는 총량 상한. 0 이면 없음")] public int Cap { get; private set; }
		}

		[field: SerializeField] public List<Entry> Entries { get; private set; } = new();
	}
}
