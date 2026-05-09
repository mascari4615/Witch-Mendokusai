using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace WitchMendokusai
{
	public static class DungeonObjectiveStrategyFactory
	{
		public static DungeonObjectiveStrategy Create(Dungeon dungeon)
		{
			switch (dungeon.ObjectiveType)
			{

				case DungeonObjectiveType.TimeSurvival:
					return new DungeonObjectiveStrategyTimeSurvival();
				case DungeonObjectiveType.Domination:
					return new DungeonObjectiveStrategyDomination();
				case DungeonObjectiveType.KillCount:
					return new DungeonObjectiveStrategyKillCount();
				case DungeonObjectiveType.Boss:
					return new DungeonObjectiveStrategyBoss();
				default:
					throw new ArgumentOutOfRangeException(nameof(dungeon.ObjectiveType), dungeon.ObjectiveType, null);
			}
		}
	}
}