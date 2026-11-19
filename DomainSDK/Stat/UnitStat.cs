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

		// 시작값은 전부 0. 기본 치명타 같은 게임 수치는 Core 의 UnitBaseStats 자산이 채움
		protected override void InitValue()
		{
			foreach (UnitStatType statType in Enum.GetValues(typeof(UnitStatType)))
				stats[statType] = 0;
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