using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>던전 세부 최고 기록의 항목. 순위표 키의 끝 이름</summary>
	public enum DungeonRecordStat
	{
		LongestSurvival = 0,
		FastestClear = 1,
		MostKills = 2,
		HighestLevel = 3,
		MostNyang = 4,
		BestRisk = 5,
	}

	/// <summary>
	/// 던전 하나의 세부 최고 기록. Json.NET 저장 DTO (DungeonSaveData.Bests). 0 은 기록 없음, 옛 저장은 전부 0
	/// 최고 위험도는 이 묶음 밖 Dungeon.BestRisk (위험도 보상과 같이 쓰므로)
	/// </summary>
	public struct DungeonBestRecords
	{
		// 한 판에 버틴 초. 패배한 판 포함
		public int LongestSurvivalSeconds;
		// 클리어한 판의 초. 작을수록 좋음
		public int FastestClearSeconds;
		public int MostKills;
		public int HighestLevel;
		// 한 판에 번 냥 (위협 배율, 보너스 포함한 결과창 합계)
		public int MostNyang;
	}

	/// <summary>던전 세부 최고 기록 규칙. 저장 매체와 분리된 순수 함수, EditMode 로 검증</summary>
	public static class DungeonRecordRules
	{
		/// <summary>판 결과 하나를 최고 기록에 합침. 각 항목은 더 좋은 쪽만 남김</summary>
		public static DungeonBestRecords Merge(DungeonBestRecords best, DungeonRecord result)
		{
			int seconds = (int)result.PlayTime.TotalSeconds;
			best.LongestSurvivalSeconds = Higher(best.LongestSurvivalSeconds, seconds);
			if (result.IsCleared && seconds > 0)
			{
				best.FastestClearSeconds = best.FastestClearSeconds <= 0 ? seconds : System.Math.Min(best.FastestClearSeconds, seconds);
			}
			best.MostKills = Higher(best.MostKills, result.KillCount);
			best.HighestLevel = Higher(best.HighestLevel, result.Level);
			best.MostNyang = Higher(best.MostNyang, result.Nyang);
			return best;
		}

		/// <summary>순위표로 알릴 항목과 값. 0 인 항목도 포함 (받는 쪽이 0 을 기록 없음으로 거름)</summary>
		public static IEnumerable<(DungeonRecordStat Stat, int Value)> Values(DungeonBestRecords best, int bestRisk)
		{
			yield return (DungeonRecordStat.LongestSurvival, best.LongestSurvivalSeconds);
			yield return (DungeonRecordStat.FastestClear, best.FastestClearSeconds);
			yield return (DungeonRecordStat.MostKills, best.MostKills);
			yield return (DungeonRecordStat.HighestLevel, best.HighestLevel);
			yield return (DungeonRecordStat.MostNyang, best.MostNyang);
			yield return (DungeonRecordStat.BestRisk, bestRisk);
		}

		public static string RecordKey(int dungeonId, DungeonRecordStat stat) => "Dungeon." + dungeonId + "." + stat;

		private static int Higher(int best, int value) => value > best ? value : best;
	}
}
