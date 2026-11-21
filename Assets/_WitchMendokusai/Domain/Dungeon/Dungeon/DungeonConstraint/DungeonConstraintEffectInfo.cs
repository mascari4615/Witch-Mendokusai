using System;

namespace WitchMendokusai
{
	[Serializable]
	public struct DungeonConstraintEffectInfo
	{
		// Stat 이면 Affiliation 유닛의 StatType 에 Value 가산. 나머지는 판 규칙, Value 만 씀 (DungeonConstraintRunRules)
		public DungeonConstraintEffectKind Kind;
		// 몬스터 타입
		public UnitAffiliation Affiliation;
		public UnitStatType StatType;
		public int Value;
	}
}