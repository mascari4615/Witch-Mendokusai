namespace WitchMendokusai
{
	// TASK-WM-107 Slice 2C-2 — ctx 경로 = ctx.PlayerProvider.Current.UnitStat (DI caller thread).
	// ctx null = PlayerProviderBridge transitional fallback (미thread 호출처 — 후속 수렴 시 제거).
	public class StatCriteria : NumCriteria
	{
		public UnitStatType Type { get; private set; }

		private readonly CriteriaContext context;

		public StatCriteria(CriteriaInfo criteriaInfo, CriteriaContext context = null) : base(criteriaInfo)
		{
			Type = (criteriaInfo.Data as UnitStatData).Type;
			this.context = context;
		}

		private UnitStat PlayerStat => context == null ? PlayerProviderBridge.Current.UnitStat : context.PlayerProvider.Current.UnitStat;

		public override int GetCurValue()
		{
			return PlayerStat[Type];
		}
	}
}