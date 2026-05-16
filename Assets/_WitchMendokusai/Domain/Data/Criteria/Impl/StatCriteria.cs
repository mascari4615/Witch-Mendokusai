namespace WitchMendokusai
{
	// TASK-WM-107 Slice 2C-4 — Bridge 의존 완전 폐기. ctx 단일 지점(QuestManager.CreateCriteriaContext()) 공급,
	// null 시 NRE = FastFail (방어 fallback X — WM FastFail 룰).
	public class StatCriteria : NumCriteria
	{
		public UnitStatType Type { get; private set; }

		private readonly CriteriaContext context;

		public StatCriteria(CriteriaInfo criteriaInfo, CriteriaContext context = null) : base(criteriaInfo)
		{
			Type = (criteriaInfo.Data as UnitStatData).Type;
			this.context = context;
		}

		private UnitStat PlayerStat => context.PlayerProvider.Current.UnitStat;

		public override int GetCurValue()
		{
			return PlayerStat[Type];
		}
	}
}