namespace WitchMendokusai
{
	public enum UnitStatType
	{
		#region Stat
		// 체력
		HP_CUR = 0, // 런타임
		HP_MAX = 1, // 런타임
		HP_MAX_STAT = 2, // 기반

		// 경험치, 레벨
		EXP_CUR = 100,
		EXP_MAX = 101,
		LEVEL_CUR = 102,

		// 마나
		MANA_CUR = 200, // 런타임
		MANA_MAX = 201, // 런타임
		MANA_MAX_STAT = 202, // 기반

		// 이동
		MOVEMENT_SPEED = 300,
		MOVEMENT_SPEED_BONUS = 301,

		// 스킬
		COOLTIME_BONUS = 400,
		ATTACK_SPEED_BONUS = 401,

		// 데미지
		DAMAGE_BONUS = 500,

		// 크리티컬
		CRITICAL_CHANCE = 600,
		CRITICAL_DAMAGE = 601,
		#endregion

		#region Keyword
		// 키워드
		EXP_COLLIDER_SCALE = 10_000,
		SATELLITE_COUNT = 10_001,
		SATELLITE_ROTATE_SPEED_BONUS = 10_002,
		SATELLITE_DAMAGE_BONUS = 10_003,
		SATELLITE_SCALE_BONUS = 10_004,

		FAIRY_COUNT = 10_100,
		FAIRY_DAMAGE_BONUS = 10_101,
		FAIRY_ATTACK_SPEED_BONUS = 10_102,

		TURRET_COUNT = 10_200,
		TURRET_DAMAGE_BONUS = 10_201,
		TURRET_ATTACK_SPEED_BONUS = 10_202,
		#endregion

		#region Status
		// 상태
		DEAD = 100_000_000,
		CASTING_SKILL = 100_000_001,
		FORCE_MOVE = 100_000_002,
		#endregion
	}
}