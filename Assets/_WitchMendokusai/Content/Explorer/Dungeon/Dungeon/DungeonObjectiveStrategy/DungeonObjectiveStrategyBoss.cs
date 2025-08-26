using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class DungeonObjectiveStrategyBoss : DungeonObjectiveStrategy
	{
		protected override string GetQuestName(Dungeon dungeon, QuestInfo questInfo)
		{
			return "보스 처치";
		}

		protected override void SetQuestInfo(Dungeon dungeon, ref QuestInfo questInfo)
		{
			questInfo.Criteria = new()
			{
				new CriteriaInfo()
				{
					Type = CriteriaType.DungeonStat,
					Data = GetDungeonStatData(DungeonStatType.BOSS_KILL),
					ComparisonOperator = ComparisonOperator.GreaterThanOrEqualTo,
					Value = dungeon.ClearValue,
					JustOnce = true,
				}
			};
		}
	}
}