namespace WitchMendokusai
{
	/// <summary>
	/// 전술 룰의 타겟 선정 계약. 매치 스코프 구현(TargetingSystem, Domain)이 살아있는 참가자 중
	/// TargetQuery(진영·우선순위·사거리)에 맞는 단일 타겟을 반환. 후보 없으면 null.
	/// </summary>
	public interface ITargetResolver
	{
		ICombatant Query(ICombatant self, TargetQuery query);
	}
}
