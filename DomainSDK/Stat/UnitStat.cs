using System;

namespace WitchMendokusai
{
	public class UnitStat : Stat<UnitStatType>
	{
		public const int PERCENT = 100;

		public UnitStat() : base()
		{
		}

		// 퍼센트 단위 스탯의 배율, 0이면 1
		public float PercentMultiplier(UnitStatType statType) => 1f + (float)this[statType] / PERCENT;

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