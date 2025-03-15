using UnityEngine;

namespace WitchMendokusai
{
	public class DungeonStatCriteria : NumCriteria
	{
		public DungeonStatType Type { get; private set; }

		public DungeonStatCriteria(CriteriaInfo criteriaInfo) : base(criteriaInfo)
		{
			Type = (criteriaInfo.Data as DungeonStatData).Type;
		}

		public override int GetCurValue()
		{
			return DataManager.Instance.DungeonStat[Type];
		}
	}
}