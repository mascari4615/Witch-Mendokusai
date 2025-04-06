using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class DungeonStrategyTimeSurvival : DungeonStrategy
	{
		protected override string GetQuestName(Dungeon dungeon, QuestInfo questInfo)
		{
			return "시간 동안 생존";
		}

		protected override void SetQuestInfo(Dungeon dungeon, ref QuestInfo questInfo)
		{
			questInfo.Criteria = new()
			{
				new CriteriaInfo()
				{
					Type = CriteriaType.DungeonStat,
					Data = GetDungeonStatData(DungeonStatType.DUNGEON_TIME),
					ComparisonOperator = ComparisonOperator.GreaterThanOrEqualTo,
					Value = dungeon.TimeBySecond,
					JustOnce = true,
				}
			};
		}
	}
}