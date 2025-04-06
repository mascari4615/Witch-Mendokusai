using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public abstract class DungeonStrategy
	{
		public RuntimeQuest CreateRuntimeQuest(Dungeon dungeon)
		{
			QuestInfo questInfo = CreateQuestInfo(dungeon);

			SetQuestInfo(dungeon, ref questInfo);
			string questName = GetQuestName(dungeon, questInfo);

			RuntimeQuest runtimeQuest = new(questInfo, questName);
			return runtimeQuest;
		}

		protected abstract void SetQuestInfo(Dungeon dungeon, ref QuestInfo questInfo);
		protected abstract string GetQuestName(Dungeon dungeon, QuestInfo questInfo);

		protected QuestInfo CreateQuestInfo(Dungeon dungeon)
		{
			return new()
			{
				Type = QuestType.Dungeon,
				GameEvents = new()
				{
					GameEventType.OnTick,
				},
				Criteria = new(),
				CompleteEffects = new()
				{
					new EffectInfo()
					{
						Type = EffectType.DungeonStat,
						Data = GetDungeonStatData(DungeonStatType.DUNGEON_CLEAR),
						ArithmeticOperator = ArithmeticOperator.Add,
						Value = 1,
					}
				},
				RewardEffects = new(),
				Rewards = dungeon.Rewards,

				WorkTime = 0,
				AutoWork = false,
				AutoComplete = true,
			};
		}
	}
}