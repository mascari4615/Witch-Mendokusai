namespace WitchMendokusai
{
	// 스탯 자산 Name 은 DataSOWindow 가 enum 이름으로 덮어씀. 화면 표시 이름의 단일 출처
	public static class StatDisplayName
	{
		public static string Get(GameStatType type) => type switch
		{
			GameStatType.TOTAL_MONSTER_KILL => "몬스터 처치",
			GameStatType.TOTAL_BOSS_KILL => "보스 처치",
			GameStatType.TOTAL_DUNGEON_CLEAR => "던전 클리어",
			GameStatType.TOTAL_DUNGEON_TIME => "던전 시간",
			GameStatType.NYANG => "냥",
			GameStatType.MANA => "마나",
			GameStatType.VILLAGE_QUEST_EXP => "마을 경험치",
			GameStatType.VILLAGE_QUEST_EXP_MAX => "마을 경험치 최대",
			GameStatType.VILLAGE_QUEST_LEVEL => "마을 레벨",
			GameStatType.VILLAGE_REPUTATION_EXP => "마을 평판",
			GameStatType.VILLAGE_REPUTATION_EXP_MAX => "마을 평판 최대",
			GameStatType.VILLAGE_REPUTATION_LEVEL => "평판 레벨",
			GameStatType.POT_COUNT => "솥 개수",
			_ => "기록",
		};

		// 퀘스트 조건 후보 기본 스탯만. 무기 키워드 스탯 제외
		public static string Get(UnitStatType type) => type switch
		{
			UnitStatType.HP_CUR => "체력",
			UnitStatType.HP_MAX or UnitStatType.HP_MAX_STAT => "최대 체력",
			UnitStatType.EXP_CUR => "경험치",
			UnitStatType.LEVEL_CUR => "레벨",
			UnitStatType.MANA_CUR => "마나",
			UnitStatType.MANA_MAX or UnitStatType.MANA_MAX_STAT => "최대 마나",
			UnitStatType.MOVEMENT_SPEED => "이동 속도",
			UnitStatType.DAMAGE_BONUS => "피해량",
			UnitStatType.CRITICAL_CHANCE => "치명타 확률",
			UnitStatType.ARMOR => "방어력",
			UnitStatType.REVIVE_COUNT => "부활 횟수",
			_ => "능력치",
		};
	}
}
