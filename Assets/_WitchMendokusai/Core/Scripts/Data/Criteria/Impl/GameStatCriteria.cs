using UnityEngine;

namespace WitchMendokusai
{
	public class GameStatCriteria : NumCriteria
	{
		public GameStatType Type { get; private set; }

		public GameStatCriteria(CriteriaInfo criteriaInfo) : base(criteriaInfo)
		{
			Type = (criteriaInfo.Data as GameStatData).Type;
		}

		public override int GetCurValue()
		{
			return DataManager.Instance.GameStat[Type];
		}
	}
}