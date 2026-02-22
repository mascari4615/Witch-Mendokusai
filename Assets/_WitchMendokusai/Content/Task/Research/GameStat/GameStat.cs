using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class GameStat : Stat<GameStatType>, ISavable<Dictionary<GameStatType, int>>
	{
		public GameStat() : base()
		{
			RemoveAndAddEvent(GameStatType.VILLAGE_QUEST_EXP, UpdateVillageQuestStat);
			void UpdateVillageQuestStat()
			{
				if (stats[GameStatType.VILLAGE_QUEST_EXP] >= stats[GameStatType.VILLAGE_QUEST_EXP_MAX])
				{
					stats[GameStatType.VILLAGE_QUEST_EXP_MAX] += 100;
					stats[GameStatType.VILLAGE_QUEST_LEVEL]++;
					stats[GameStatType.VILLAGE_QUEST_EXP] -= stats[GameStatType.VILLAGE_QUEST_EXP_MAX];
				}
			}

			RemoveAndAddEvent(GameStatType.VILLAGE_REPUTATION_EXP, UpdateVillageReputationStat);
			void UpdateVillageReputationStat()
			{
				if (stats[GameStatType.VILLAGE_REPUTATION_EXP] >= stats[GameStatType.VILLAGE_REPUTATION_EXP_MAX])
				{
					stats[GameStatType.VILLAGE_REPUTATION_EXP_MAX] += 100;
					stats[GameStatType.VILLAGE_REPUTATION_LEVEL]++;
					stats[GameStatType.VILLAGE_REPUTATION_EXP] -= stats[GameStatType.VILLAGE_REPUTATION_EXP_MAX];
				}
			}

			void RemoveAndAddEvent(GameStatType statType, Action action)
			{
				RemoveListener(statType, action);
				AddListener(statType, action);
			}
		}

		protected override void InitValue()
		{
			foreach (GameStatType statType in Enum.GetValues(typeof(GameStatType)))
			{
				int value = statType switch
				{
					GameStatType.VILLAGE_QUEST_EXP_MAX => 100,
					GameStatType.VILLAGE_QUEST_LEVEL => 1,
					GameStatType.VILLAGE_REPUTATION_EXP_MAX => 100,
					GameStatType.VILLAGE_REPUTATION_LEVEL => 1,
					_ => 0,
				};

				stats[statType] = value;
			}
		}

		public void UpdateData()
		{
			// Dungeon
			{
				DungeonStat dungeonStat = DataManager.Instance.DungeonStat;

				stats[GameStatType.TOTAL_MONSTER_KILL] += dungeonStat[DungeonStatType.MONSTER_KILL];
				stats[GameStatType.TOTAL_MONSTER_KILL] += dungeonStat[DungeonStatType.BOSS_KILL];
				stats[GameStatType.TOTAL_DUNGEON_TIME] += dungeonStat[DungeonStatType.DUNGEON_TIME];

				dungeonStat.Init();
			}
		}

		public void Load(Dictionary<GameStatType, int> saveGameStat)
		{
			InitValue();

			// 저장된 통계 불러오기
			foreach ((GameStatType key, int value) in saveGameStat)
				stats[key] = value;
		}

		public Dictionary<GameStatType, int> Save()
		{
			return stats;
		}
	}
}