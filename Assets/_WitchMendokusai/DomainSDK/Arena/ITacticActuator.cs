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
		void Retreat(ICombatant target);
		void Hold();
	}
}
