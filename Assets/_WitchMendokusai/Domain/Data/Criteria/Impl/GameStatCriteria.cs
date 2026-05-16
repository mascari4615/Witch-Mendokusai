using UnityEngine;

namespace WitchMendokusai
{
	// TASK-WM-107 Slice 2C-4 — Bridge 의존 완전 폐기. ctx 단일 지점(QuestManager.CreateCriteriaContext()) 공급,
	// null 시 NRE = FastFail (방어 fallback X — WM FastFail 룰).
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
			GameStat gameStat = context.DataManager.GameStat;
			return gameStat[Type];
		}
	}
}