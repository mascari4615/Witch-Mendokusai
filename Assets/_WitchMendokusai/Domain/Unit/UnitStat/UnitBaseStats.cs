using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 모든 유닛 능력치의 시작값. 유닛 자산 InitStatInfos 가 같은 칸을 덮어씀
	[CreateAssetMenu(fileName = nameof(UnitBaseStats), menuName = "WM/Variable/" + nameof(UnitBaseStats))]
	public class UnitBaseStats : ScriptableObject
	{
		[Tooltip("치명타 확률과 피해는 퍼센트 단위")]
		public List<UnitStatInfo> BaseStats = new()
		{
			new() { unitStat = UnitStatType.CRITICAL_CHANCE, value = 5 },
			new() { unitStat = UnitStatType.CRITICAL_DAMAGE, value = 30 },
		};

		public void ApplyTo(UnitStat unitStat)
		{
			foreach (UnitStatInfo statInfo in BaseStats)
				unitStat[statInfo.unitStat] = statInfo.value;
		}
	}
}
