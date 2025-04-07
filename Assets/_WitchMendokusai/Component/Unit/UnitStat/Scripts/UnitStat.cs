using UnityEngine;

namespace WitchMendokusai
{
	public class UnitStat : Stat<UnitStatType>
	{
		public override void Init(Stat<UnitStatType> newStats)
		{
			base.Init(newStats);

			this[UnitStatType.HP_MAX] = this[UnitStatType.HP_MAX_STAT];
			this[UnitStatType.HP_CUR] = this[UnitStatType.HP_MAX];

			this[UnitStatType.MANA_MAX] = this[UnitStatType.MANA_MAX_STAT];
			this[UnitStatType.MANA_CUR] = this[UnitStatType.MANA_MAX];

			this[UnitStatType.CRITICAL_CHANCE] = 5;
		}
	}
}