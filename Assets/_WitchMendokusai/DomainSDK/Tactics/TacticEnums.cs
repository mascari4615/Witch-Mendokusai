namespace WitchMendokusai
{
	/// <summary> 전술 룰의 조건 종류. EvalConditions(Domain) 가 ConditionKind 별로 평가. </summary>
	public enum ConditionKind
	{
		Always = 0,           // 무조건 true (fallback 룰 — 맨 아래)
		SelfHpBelow = 1,      // 내 HP 절대값 vs Value
		SelfHpRatioBelow = 2, // 내 HP 비율(0~1) vs Value
		EnemyInRange = 3,     // 사거리(Value) 내 적 존재
		TargetHpBelow = 4,    // 선정된 타겟 HP 비율 vs Value
		AllyDown = 5,         // 쓰러진 아군 존재
		AllyCount = 6,        // 생존 아군 수 vs Value
		SkillReady = 7,       // SkillSlot 스킬 쿨다운 준비됨
	}

	/// <summary> 전술 룰의 행동 종류. ExecAction(Domain) 가 ActionKind 별로 수행. </summary>
	public enum ActionKind
	{
		Hold = 0,         // 제자리 대기
		UseSkill = 1,     // SkillSlot 스킬 시전 (타겟 = 룰의 TargetQuery)
		MoveToTarget = 2, // 선정된 타겟에게 이동
		Approach = 3,     // 타겟에게 접근 (사거리까지)
		Retreat = 4,      // 타겟 반대 방향으로 후퇴
	}

	/// <summary> 타겟 진영 필터. 매치 스코프 TeamId 기준(로어 진영 UnitAffiliation 과 별개). </summary>
	public enum TargetSide
	{
		Enemy = 0,
		Ally = 1,
		Self = 2,
	}

	/// <summary> 타겟 우선순위(후보 정렬 키). 동률은 InstanceId 타이브레이크로 결정성 보장. </summary>
	public enum TargetPriority
	{
		Nearest = 0,
		Farthest = 1,
		LowestHp = 2,
		HighestHp = 3,
		LowestHpRatio = 4,
		HighestHpRatio = 5,
	}
}
