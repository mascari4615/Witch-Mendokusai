namespace WitchMendokusai
{
	/// <summary>
	/// 전술 룰의 타겟 선정 계약. 매치 스코프 구현(TargetingSystem, Domain)이 살아있는 참가자 중
	/// TargetQuery(진영·우선순위·사거리)에 맞는 단일 타겟을 반환. 후보 없으면 null.
	/// </summary>
	public interface ITargetResolver
	{
		ICombatant Query(ICombatant self, TargetQuery query);

		/// <summary>
		/// 같은 필터를 통과하는 <b>살아있는 후보의 수</b>. 「아군이 몇 남았나」처럼 한 명을 고르는 게
		/// 아니라 머릿수를 묻는 조건이 쓴다. `Query` 와 같은 필터를 타야 「보이는 것」과 「세는 것」이
		/// 갈리지 않는다(자기 자신은 진영 필터가 이미 제외한다).
		/// </summary>
		int CountAlive(ICombatant self, TargetQuery query);
	}
}
