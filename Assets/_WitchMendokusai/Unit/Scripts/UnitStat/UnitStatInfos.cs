using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[Serializable]
	public struct UnitStatInfo
	{
		public UnitStatType unitStat;
		public int value;
	}

	[Serializable]
	public class UnitStatInfos
	{
		[SerializeField] private List<UnitStatInfo> initStats = new()
		{
			new() { unitStat = UnitStatType.HP_MAX, value = 20 },
			new() { unitStat = UnitStatType.MOVEMENT_SPEED, value = 3 },
		};

		public UnitStat GetUnitStat()
		{
			UnitStat unitStat = new();
			foreach (UnitStatInfo statInfo in initStats)
				unitStat[statInfo.unitStat] = statInfo.value;

			return unitStat;
		}
	}
}