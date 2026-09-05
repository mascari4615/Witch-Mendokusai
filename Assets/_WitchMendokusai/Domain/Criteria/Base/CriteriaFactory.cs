using System;

namespace WitchMendokusai
{
	public static class CriteriaFactory
	{
		public static Criteria Create(CriteriaInfo criteriaInfo, CriteriaContext context = null)
		{
			return criteriaInfo.Type switch
			{
				CriteriaType.ItemCount => new ItemCountCriteria(criteriaInfo, context),
				CriteriaType.UnitStat => new StatCriteria(criteriaInfo, context),
				CriteriaType.GameStat => new GameStatCriteria(criteriaInfo, context),
				CriteriaType.DungeonStat => new DungeonStatCriteria(criteriaInfo, context),
				_ => throw new ArgumentOutOfRangeException(),
			};
		}
	}
}
