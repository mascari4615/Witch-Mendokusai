using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace WitchMendokusai
{
	public abstract class Criteria : ICriteria
	{
		public abstract int GetCurValue();
		public abstract int GetTargetValue();
		public abstract bool Evaluate();
		public virtual float GetProgress()
		{
			return (float)GetCurValue() / GetTargetValue();
		}

		public static Criteria CreateCriteria(CriteriaInfo criteriaInfo)
		{
			return criteriaInfo.Type switch
			{
				CriteriaType.ItemCount => new ItemCountCriteria(criteriaInfo),
				CriteriaType.UnitStat => new StatCriteria(criteriaInfo),
				CriteriaType.GameStat => new GameStatCriteria(criteriaInfo),
				CriteriaType.DungeonStat => new DungeonStatCriteria(criteriaInfo),
				_ => throw new ArgumentOutOfRangeException(),
			};
		}
	}
}