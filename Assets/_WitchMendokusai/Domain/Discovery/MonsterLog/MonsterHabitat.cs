namespace WitchMendokusai
{
	/// <summary>몬스터 하나가 던전 하나에 나오는 시간대. 이어진 웨이브는 하나로 합침. 사건 (정예, 무리) 으로만 나오면 HasTime 이 거짓</summary>
	public readonly struct MonsterHabitat
	{
		public readonly Dungeon Dungeon;
		public readonly bool HasTime;
		public readonly float StartSeconds;
		public readonly float EndSeconds;

		public MonsterHabitat(Dungeon dungeon, bool hasTime, float startSeconds, float endSeconds)
		{
			Dungeon = dungeon;
			HasTime = hasTime;
			StartSeconds = startSeconds;
			EndSeconds = endSeconds;
		}
	}
}
