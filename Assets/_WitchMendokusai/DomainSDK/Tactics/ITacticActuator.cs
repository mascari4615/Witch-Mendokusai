namespace WitchMendokusai
{
	/// <summary>
	/// 전술 행동(ExecAction)의 실행 표면 — 스킬 시전 / 이동 / 후퇴 / 대기.
	/// 실제 구현(TacticDriver)은 UnitObject(SkillHandler/UnitMovement)에 적용,
	/// 테스트는 호출 기록 fake. DomainSDK 라 Domain 의존 0(ICombatant 만).
	/// </summary>
	public interface ITacticActuator
	{
		void UseSkill(int skillSlot, ICombatant target);
		void MoveToward(ICombatant target);

		/// <summary>
		/// 목표까지 접근하되 <paramref name="stopDistance"/> 안에 들면 멈춘다(ActionKind.Approach).
		///
		/// ★ MoveToward 와 갈라야 하는 이유(TASK-WM-194 실측): 멈추지 않는 이동은 공격 대상 *안으로*
		///   파고든다. 개척에서 마수들이 코어 좌표에 그대로 겹쳐 쌓여 **코어 스프라이트에 가려 안 보였고**,
		///   플레이어는 "다 잡았는데 웨이브가 안 넘어간다"고 판단했다. 겹쳐 선 유닛은 화면에서 사라진다.
		/// </summary>
		void Approach(ICombatant target, float stopDistance);
		void Retreat(ICombatant target);
		void Hold();

		/// <summary>
		/// 공격할 때 발을 멈추는가(기본 true — 투기장의 「서서 싸운다」).
		///
		/// ★ 왜 갈라야 하는가 (TASK-WM-194 실증, 사용자 2회 보고 "유닛이 가만히 있음"):
		///   개척의 마수는 *전진하는 것 자체가 위협*이다. 그런데 방어 인형이 사거리에 들어오는 순간
		///   발을 멈추고 때리기 시작하면, 서로 못 죽이는 짝을 만난 마수는 **판이 끝날 때까지 그 자리에 선다**.
		///   살아있는 마수가 0이 안 되니 파도도 안 넘어간다. 「걸으면서 쏜다」로 두면 전진은 절대 멈추지 않고,
		///   건물이 부서지는 긴장도 그대로 남는다(그래서 공격 자체를 빼지 않는다).
		/// </summary>
		bool StopsToAttack { get; }
	}
}
