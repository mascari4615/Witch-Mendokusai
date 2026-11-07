
using System;

namespace WitchMendokusai
{
	public struct DungeonRecord
	{
		public TimeSpan PlayTime;
		public int Level;
		public int KillCount;
		public int BossKillCount;
		public int Nyang;
		public bool IsCleared;
		public DungeonDifficulty Difficulty;
		public float DifficultyProgress;
		public int ThreatLevel;
	}

	public class DungeonRecorder
	{
		public DungeonRecord ResultRecord { get; private set; }
		private DungeonRecord startRecord;

		private readonly DungeonManager dungeonManager;
		private readonly DataManager dataManager;

		public DungeonRecorder(DungeonManager dungeonManager, DataManager dataManager)
		{
			this.dungeonManager = dungeonManager;
			this.dataManager = dataManager;
			startRecord = new DungeonRecord();
			SetRecord(ref startRecord);
		}

		private void SetRecord(ref DungeonRecord record)
		{
			DungeonContext dungeonContext = dungeonManager.Context;

			record.PlayTime = dungeonContext.ElapsedTime;
			record.KillCount = dataManager.DungeonStat[DungeonStatType.MONSTER_KILL];
			record.BossKillCount = dataManager.DungeonStat[DungeonStatType.BOSS_KILL];
			record.Nyang = dataManager.GameStat[GameStatType.NYANG];
		}

		public int KillCountSinceStart => dataManager.DungeonStat[DungeonStatType.MONSTER_KILL] - startRecord.KillCount;
		public int NyangSinceStart => dataManager.GameStat[GameStatType.NYANG] - startRecord.Nyang;

		public DungeonRecord CaptureResultRecord(int level, bool isCleared)
		{
			DungeonRecord endRecord = new();
			SetRecord(ref endRecord);
			DungeonContext context = dungeonManager.Context;

			DungeonRecord result = new()
			{
				PlayTime = endRecord.PlayTime - startRecord.PlayTime,
				Level = level,
				KillCount = endRecord.KillCount - startRecord.KillCount,
				BossKillCount = endRecord.BossKillCount - startRecord.BossKillCount,
				Nyang = endRecord.Nyang - startRecord.Nyang,
				IsCleared = isCleared,
				Difficulty = context.CurDifficulty,
				DifficultyProgress = context.CurDifficulty == DungeonDifficulty.Hard ? 1f : context.DifficultyProgress,
				ThreatLevel = dungeonManager.CurThreat.Level
			};

			ResultRecord = result;
			return result;
		}
	}
}
