
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

			record.PlayTime = dungeonContext.DungeonCurTime;
			record.KillCount = dataManager.DungeonStat[DungeonStatType.MONSTER_KILL];
			record.BossKillCount = dataManager.DungeonStat[DungeonStatType.BOSS_KILL];
			record.Nyang = dataManager.GameStat[GameStatType.NYANG];
		}

		public DungeonRecord CaptureResultRecord()
		{
			DungeonRecord endRecord = new();
			SetRecord(ref endRecord);

			DungeonRecord result = new()
			{
				PlayTime = endRecord.PlayTime - startRecord.PlayTime,
				KillCount = endRecord.KillCount - startRecord.KillCount,
				BossKillCount = endRecord.BossKillCount - startRecord.BossKillCount,
				Nyang = endRecord.Nyang - startRecord.Nyang
			};

			ResultRecord = result;
			return result;
		}
	}
}
