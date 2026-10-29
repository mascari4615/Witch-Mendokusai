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
		public TimeSpan ElapsedTime { get; private set; } = TimeSpan.Zero;
		public TimeSpan DungeonCurTime { get; private set; } = new(0, 0, 15, 0, 0);

		private readonly DungeonStat dungeonStat;

		public DungeonContext(TimeSpan initialDungeonTime, List<DungeonConstraint> constraints, DungeonStat dungeonStat)
		{
			this.dungeonStat = dungeonStat;
			Constraints = constraints;
			InitialDungeonTime = initialDungeonTime;
			DungeonCurTime = InitialDungeonTime;
		}

		public void UpdateTime()
		{
			ElapsedTime += TimeUpdateInterval;
			DungeonCurTime = InitialDungeonTime > ElapsedTime ? InitialDungeonTime - ElapsedTime : TimeSpan.Zero;
			dungeonStat[DungeonStatType.DUNGEON_TIME] = (int)ElapsedTime.TotalSeconds;
		}

		public void UpdateDifficulty()
		{
			CurDifficulty = (DungeonDifficulty)Math.Min((int)DungeonDifficulty.Hard, (int)(ElapsedTime.TotalMinutes / 3));
		}
	}
}