namespace WitchMendokusai
{
	public class DungeonRecorder
	{
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
			record.Nyang = SOManager.Instance.Nyang.RuntimeValue;
		}

		public DungeonRecord GetResultRecord()
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
			return result;
		}
	}
}