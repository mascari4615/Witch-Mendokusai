
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

		public DungeonRecorder()
		{
			startRecord = new DungeonRecord();
			SetRecord(ref startRecord);
		}

		private void SetRecord(ref DungeonRecord record)
		{
			DungeonContext dungeonContext = DungeonManager.Instance.Context;

			record.PlayTime = dungeonContext.DungeonCurTime;
			record.KillCount = DataManager.Instance.DungeonStat[DungeonStatType.MONSTER_KILL];
			record.BossKillCount = DataManager.Instance.DungeonStat[DungeonStatType.BOSS_KILL];
			record.Nyang = DataManager.Instance.GameStat[GameStatType.NYANG];
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