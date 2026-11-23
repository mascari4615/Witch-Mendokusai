namespace WitchMendokusai
{
	public enum QuestType
	{
		None = -1,
		Normal = 0,
		VillageRequest = 1,
		Achievement = 2,
		Research = 50,
		// 마도서 한 페이지. 재료를 모아 채우는 퀘스트. 판 HUD 와 결과창이 이 값으로 지금 페이지를 찾음
		MagicBookPage = 51,
		Dungeon = 100,
	}
}