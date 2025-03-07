using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class DungeonStat : Stat<DungeonStatType>
	{
		public void UpdateData()
		{
			stats[DungeonStatType.MONSTER_KILL] = 0;
			stats[DungeonStatType.BOSS_KILL] = 0;
			stats[DungeonStatType.DUNGEON_TIME] = 0;
		}

		public DungeonStat()
		{
			InitAllZero();
		}
	}
}