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

	public static class DungeonDifficultyText
	{
		/// <summary> 화면에 쓰는 난이도 이름. HUD, 결과창, 알림 띠가 같이 씀 </summary>
		public static string Name(DungeonDifficulty difficulty) => difficulty switch
		{
			DungeonDifficulty.Easy => "쉬움",
			DungeonDifficulty.Normal => "보통",
			_ => "어려움",
		};
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

		public float DifficultyProgress => (float)(ElapsedTime.TotalSeconds % 180d / 180d);

		public void UpdateTime()
		{
			ElapsedTime += TimeUpdateInterval;
			DungeonCurTime = InitialDungeonTime > ElapsedTime ? InitialDungeonTime - ElapsedTime : TimeSpan.Zero;
			dungeonStat[DungeonStatType.DUNGEON_TIME] = (int)ElapsedTime.TotalSeconds;
		}

		public void UpdateDifficulty()
		{
			DungeonDifficulty previous = CurDifficulty;
			CurDifficulty = (DungeonDifficulty)Math.Min((int)DungeonDifficulty.Hard, (int)(ElapsedTime.TotalMinutes / 3));
			if (CurDifficulty > previous)
				EventBusBridge.Publish(new DifficultyRaisedEvent { Difficulty = CurDifficulty });
		}
	}
}