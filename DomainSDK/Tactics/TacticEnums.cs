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
		AllyCount = 6,     // 생존 아군 수(자기 제외, 판 전체) vs Value (Operator)
	}

	/// <summary> 전술 룰의 행동 종류. ExecAction(Domain) 가 ActionKind 별로 수행. </summary>
	public enum ActionKind
	{
		Hold = 0,         // 제자리 대기
		UseSkill = 1,     // SkillSlot 스킬 시전 (타겟 = 룰의 TargetQuery)
		MoveToTarget = 2, // 선정된 타겟에게 이동
		Approach = 3,     // 타겟에게 붙되 정지 거리에서 멈춤(MoveToTarget = 끝까지 파고듦)
		Retreat = 4,      // 타겟 반대 방향으로 후퇴
	}

	/// <summary> 타겟 진영 필터. 매치 스코프 TeamId 기준(로어 진영 UnitAffiliation 과 별개). </summary>
	public enum TargetSide
	{
		Enemy = 0,
		Ally = 1,
		Self = 2,

		// 적 진영의 *목표물* (TD 코어 / MOBA 넥서스 / TAB 사령부). append-only.
		// ITargetResolver 구현이 별도 등록된 objective 만 후보로 삼는다 → "전진해서 기지를 친다" 를
		// 일반 Enemy 질의(= 앞을 막는 아무 유닛)와 분리. TASK-WM-194, WM-165 가 예고한 Lane/넥서스 확장과 동일 개념.
		EnemyObjective = 3,
	}

	/// <summary>
	/// 타겟 우선순위(후보 정렬 키). 동률은 <b>CombatantId</b> 타이브레이크로 결정성 보장 —
	/// InstanceId 가 아니다(그건 실행마다 바뀌어 리플레이가 깨진다. 매치 셋업이 스폰 시 0..N 을 준다).
	/// </summary>
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
