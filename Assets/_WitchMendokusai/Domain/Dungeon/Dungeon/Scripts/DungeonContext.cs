using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum DungeonDifficulty
	{
		Easy,
		Normal,
		Hard
	}

	public class DungeonContext
	{
		public static readonly TimeSpan TimeUpdateInterval = new(0, 0, 0, 0, 100);

		public List<DungeonConstraint> Constraints { get; private set; } = new();
		public DungeonDifficulty CurDifficulty { get; private set; } = DungeonDifficulty.Easy;
		public TimeSpan InitialDungeonTime { get; private set; } = new(0, 0, 15, 0, 0);
		public TimeSpan DungeonCurTime { get; private set; } = new(0, 0, 15, 0, 0);

		public DungeonContext(TimeSpan initialDungeonTime, List<DungeonConstraint> constraints)
		{
			Constraints = constraints;
			InitialDungeonTime = initialDungeonTime;
			DungeonCurTime = InitialDungeonTime;
		}

		public void UpdateTime()
		{
			DungeonCurTime -= TimeUpdateInterval;
			DataManager.Instance.DungeonStat[DungeonStatType.DUNGEON_TIME] = (int)(InitialDungeonTime.TotalSeconds - DungeonCurTime.TotalSeconds);
		}

		public void UpdateDifficulty()
		{
			CurDifficulty = (DungeonDifficulty)((InitialDungeonTime - DungeonCurTime).TotalMinutes / 3);
		}
	}
}