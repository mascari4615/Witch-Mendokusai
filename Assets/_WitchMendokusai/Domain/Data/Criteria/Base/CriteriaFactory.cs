using System;

namespace WitchMendokusai
{
	public static class CriteriaFactory
	{
		public static Criteria Create(CriteriaInfo criteriaInfo)
		{
			return criteriaInfo.Type switch
			{
				CriteriaType.ItemCount => new ItemCountCriteria(criteriaInfo),
				CriteriaType.UnitStat => new StatCriteria(criteriaInfo),
				CriteriaType.GameStat => new GameStatCriteria(criteriaInfo),
				CriteriaType.DungeonStat => new DungeonStatCriteria(criteriaInfo),
				_ => throw new ArgumentOutOfRangeException(),
			};
		}
	}
}
