namespace WitchMendokusai
{
	// 박자가 드러나는 공격 행동. 공격 순서와 무리가 넘어갈 때를 앎
	public interface IAttackCycle
	{
		// 실행과 숨 고르기까지 끝난 횟수
		int CompletedCount { get; }

		// 예고부터 숨 고르기까지 도는 중
		bool IsAttacking { get; }

		// 마지막으로 끝난 한 번이 기절, 빙결로 끊겨 실행을 다 못 함
		bool WasCut => false;
	}
}
