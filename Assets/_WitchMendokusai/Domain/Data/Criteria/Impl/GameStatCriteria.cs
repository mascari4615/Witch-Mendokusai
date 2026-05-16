using UnityEngine;

namespace WitchMendokusai
{
	// TASK-WM-107 Slice 2C-3 — ctx 경로 = ctx.DataManager.GameStat (DI caller thread).
	// ctx null = DataManagerBridge transitional fallback (미thread 호출처 — 후속 수렴 시 제거).
	public class GameStatCriteria : NumCriteria
	{
		public GameStatType Type { get; private set; }

		private readonly CriteriaContext context;

		public GameStatCriteria(CriteriaInfo criteriaInfo, CriteriaContext context = null) : base(criteriaInfo)
		{
			Type = (criteriaInfo.Data as GameStatData).Type;
			this.context = context;
		}

		public override int GetCurValue()
		{
			GameStat gameStat = context == null ? DataManagerBridge.GameStat : context.DataManager.GameStat;
			return gameStat[Type];
		}
	}
}