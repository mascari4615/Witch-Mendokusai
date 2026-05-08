using System;
using UnityEngine;

namespace WitchMendokusai
{
	public class UnitStat : Stat<UnitStatType>
	{
		public UnitStat() : base()
		{
		}

		protected override void InitValue()
		{
			foreach (UnitStatType statType in Enum.GetValues(typeof(UnitStatType)))
			{
				int value = statType switch
				{
					UnitStatType.CRITICAL_CHANCE => 5,
					UnitStatType.CRITICAL_DAMAGE => 30,
					_ => 0,
				};

				stats[statType] = value;
			}
		}

		public override void Set(Stat<UnitStatType> newStats)
		{
			base.Set(newStats);

			this[UnitStatType.HP_MAX] = this[UnitStatType.HP_MAX_STAT];
			this[UnitStatType.HP_CUR] = this[UnitStatType.HP_MAX];

			this[UnitStatType.MANA_MAX] = this[UnitStatType.MANA_MAX_STAT];
			this[UnitStatType.MANA_CUR] = this[UnitStatType.MANA_MAX];
		}
	}
}