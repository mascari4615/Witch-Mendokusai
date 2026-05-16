using UnityEngine;

namespace WitchMendokusai
{
	// TASK-WM-107 Slice 2C-4 — Bridge 의존 완전 폐기. ctx 단일 지점(QuestManager.CreateCriteriaContext()) 공급,
	// null 시 NRE = FastFail (방어 fallback X — WM FastFail 룰).
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
			DungeonStat dungeonStat = context.DataManager.DungeonStat;
			return dungeonStat[Type];
		}
	}
}