using UnityEngine;

namespace WitchMendokusai
{
	// TASK-WM-107 Slice 2C-3 — ctx 경로 = ctx.DataManager.DungeonStat (DI caller thread).
	// ctx null = DataManagerBridge transitional fallback (미thread 호출처 — 후속 수렴 시 제거).
	public class DungeonStatCriteria : NumCriteria
	{
		public DungeonStatType Type { get; private set; }

		private readonly CriteriaContext context;

		public DungeonStatCriteria(CriteriaInfo criteriaInfo, CriteriaContext context = null) : base(criteriaInfo)
		{
			Type = (criteriaInfo.Data as DungeonStatData).Type;
			this.context = context;
		}

		public override int GetCurValue()
		{
			DungeonStat dungeonStat = context == null ? DataManagerBridge.DungeonStat : context.DataManager.DungeonStat;
			return dungeonStat[Type];
		}
	}
}