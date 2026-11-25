namespace WitchMendokusai
{
	/// <summary>몬스터 한 종의 기록. 세이브에 그대로 실림</summary>
	public sealed class MonsterLogRecord
	{
		public int Kills { get; set; }
		public int EliteKills { get; set; }

		/// <summary>이 종에게 쓰러진 횟수</summary>
		public int Defeats { get; set; }

		/// <summary>처음 잡은 날 (yyyy-MM-dd)과 던전. 잡은 적 없으면 null 과 -1</summary>
		public string FirstKilledOn { get; set; }
		public int FirstDungeonId { get; set; } = MonsterLogBook.NO_DUNGEON;

		/// <summary>도감에서 확인한 단계. 지금 단계보다 낮으면 새 기록 표시</summary>
		public int SeenStage { get; set; }
	}
}
