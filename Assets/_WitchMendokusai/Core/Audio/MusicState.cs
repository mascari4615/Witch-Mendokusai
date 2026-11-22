namespace WitchMendokusai
{
	// 지금 어떤 장면의 음악을 틀지. 값은 MusicTable 에셋에 직렬화되므로 순서를 바꾸지 않음
	public enum MusicState
	{
		Title = 0,
		Lobby = 1,
		World = 2,
		Dungeon = 3,
		Boss = 4,
		Victory = 5,
		Defeat = 6
	}
}
