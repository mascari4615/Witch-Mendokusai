namespace WitchMendokusai
{
	/// <summary>
	/// 판 결과를 던전 세부 최고 기록에 남기고 기록판 (GameRecords) 에 알림.
	/// 정본은 Dungeon.Bests 와 Dungeon.BestRisk (세이브), 기록판과 플랫폼 순위표는 사본
	/// </summary>
	public partial class DungeonManager
	{
		// 다음 판에 둘 갇힌 사람의 열쇠. 비면 안 둠. 마을이 적고 던전은 그대로 돌려줌 (생활을 모름)
		public string RescueWanted { get; set; } = string.Empty;

		// 위험도 보상 (GrantRiskRewards, BestRisk 갱신) 뒤에 부름
		private void RecordRunResult(DungeonRecord result)
		{
			CurDungeon.RecordRunResult(result);
			CurDungeon.ReportRecords();
		}
	}
}
