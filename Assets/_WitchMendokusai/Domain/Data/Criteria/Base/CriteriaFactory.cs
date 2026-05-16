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
				CriteriaType.UnitStat => new StatCriteria(criteriaInfo),
				CriteriaType.GameStat => new GameStatCriteria(criteriaInfo),
				CriteriaType.DungeonStat => new DungeonStatCriteria(criteriaInfo),
				_ => throw new ArgumentOutOfRangeException(),
			};
		}
	}
}
