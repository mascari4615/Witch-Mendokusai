using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace WitchMendokusai
{
	public static class DungeonStrategyFactory
	{
		public static DungeonStrategy Create(Dungeon dungeon)
		{
			switch (dungeon.Type)
			{

				case DungeonType.TimeSurvival:
					return new DungeonStrategyTimeSurvival();
				case DungeonType.Domination:
					return new DungeonStrategyDomination();
				case DungeonType.KillCount:
					return new DungeonStrategyKillCount();
				case DungeonType.Boss:
					return new DungeonStrategyBoss();
				default:
					throw new ArgumentOutOfRangeException(nameof(dungeon.Type), dungeon.Type, null);
			}
		}
	}
}