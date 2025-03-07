using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class GameStat : Stat<GameStatType>, ISavable<Dictionary<GameStatType, int>>
	{
		public void UpdateData()
		{
			DungeonStat dungeonStat = DataManager.Instance.DungeonStat;

			stats[GameStatType.TOTAL_MONSTER_KILL] += dungeonStat[DungeonStatType.MONSTER_KILL];
			stats[GameStatType.TOTAL_MONSTER_KILL] += dungeonStat[DungeonStatType.BOSS_KILL];
			stats[GameStatType.TOTAL_DUNGEON_TIME] += dungeonStat[DungeonStatType.DUNGEON_TIME];

			dungeonStat.UpdateData();
		}

		public GameStat()
		{
			InitAllZero();
		}

		public void Load(Dictionary<GameStatType, int> saveGameStat)
		{
			InitAllZero();

			// 저장된 통계 불러오기
			foreach (var (key, value) in saveGameStat)
				stats[key] = value;
		}

		public Dictionary<GameStatType, int> Save()
		{
			return stats;
		}
	}
}