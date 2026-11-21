namespace WitchMendokusai
{
	/// <summary>
	/// 장소 표시 (ESC 장소 칸, 입장 팝업) 의 단일 규칙. 스테이지 이름과 던전 이름은 별개
	/// 던전 안이면 큰 글은 던전 이름, 작은 글은 그 던전이 쓰는 스테이지 이름 (모든 던전이 공터 하나를 씀). 밖이면 스테이지 이름만
	/// </summary>
	public static class StagePlaceText
	{
		public static (string Main, string Sub) Of(Stage stage)
		{
			string stageName = stage != null ? stage.DisplayName : string.Empty;
			bool inDungeon = DungeonManager.TryGetExistingInstance(out DungeonManager dungeonManager) && dungeonManager.IsDungeon;
			return inDungeon ? (dungeonManager.CurDungeon.Name, stageName) : (stageName, string.Empty);
		}
	}
}
