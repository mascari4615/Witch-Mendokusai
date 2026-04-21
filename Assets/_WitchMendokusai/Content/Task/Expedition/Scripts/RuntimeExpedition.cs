using System;
using System.Globalization;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class RuntimeExpedition
	{
		public ExpeditionSO Data { get; private set; }
		public DateTime StartTime { get; private set; }

		public TimeSpan Elapsed => DateTime.UtcNow - StartTime;
		public TimeSpan Duration => TimeSpan.FromSeconds(Data.DurationSeconds);
		public TimeSpan Remaining => Duration - Elapsed;
		public bool IsComplete => Elapsed >= Duration;

		public RuntimeExpedition(ExpeditionSO data)
		{
			Data = data;
			StartTime = DateTime.UtcNow;
		}

		public RuntimeExpedition(ExpeditionSaveData saveData)
		{
			Data = GetExpeditionSO(saveData.ExpeditionId);
			StartTime = DateTime.Parse(saveData.StartTimeUtc, null, DateTimeStyles.RoundtripKind);
		}

		public ExpeditionSaveData Save() => new()
		{
			ExpeditionId = Data.ID,
			StartTimeUtc = StartTime.ToString("O")
		};
	}
}
