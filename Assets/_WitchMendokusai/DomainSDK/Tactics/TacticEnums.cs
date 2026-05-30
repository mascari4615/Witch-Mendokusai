namespace WitchMendokusai
{
	/// <summary>
	/// 전술 룰의 조건 주제. TacticConditions(Domain) 가 Kind 별로 평가.
	/// 수치 비교형(SelfHp/SelfHpRatio/TargetHpRatio/AllyCount)은 Operator+Value 로 방향 지정,
	/// 불리언형(Always/EnemyInRange/SkillReady)은 Operator 무시.
	/// </summary>
	public enum ConditionKind
	{
		Always = 0,        // 무조건 true (fallback 룰 — 맨 아래)
		SelfHp = 1,        // 내 현재 HP vs Value (Operator)
		SelfHpRatio = 2,   // 내 HP 비율(0~1) vs Value (Operator)
		TargetHpRatio = 3, // 선정된 타겟 HP 비율 vs Value (Operator)
		EnemyInRange = 4,  // 사거리 내 적 존재(타겟 해석 성공) — 불리언
		SkillReady = 5,    // SkillSlot 스킬 쿨다운 준비됨 — 불리언
		AllyCount = 6,     // 생존 아군 수 vs Value (v1 미구현 — 항상 false)
	}

	/// <summary> 전술 룰의 행동 종류. ExecAction(Domain) 가 ActionKind 별로 수행. </summary>
	public enum ActionKind
	{
		Hold = 0,         // 제자리 대기
		UseSkill = 1,     // SkillSlot 스킬 시전 (타겟 = 룰의 TargetQuery)
		MoveToTarget = 2, // 선정된 타겟에게 이동
		Approach = 3,     // v1 = MoveToTarget 과 동일(사거리-정지 미구현 — 후속 stopAtRange 자리)
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
