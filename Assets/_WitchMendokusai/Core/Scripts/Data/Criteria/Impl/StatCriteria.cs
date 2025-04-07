namespace WitchMendokusai
{
	public class StatCriteria : NumCriteria
	{
		public UnitStatType Type { get; private set; }

		public StatCriteria(CriteriaInfo criteriaInfo) : base(criteriaInfo)
		{
			Type = (criteriaInfo.Data as UnitStatData).Type;
		}

		private UnitStat PlayerStat => Player.Instance.UnitStat;

		public override int GetCurValue()
		{
			return PlayerStat[Type];
		}
	}
}