namespace WitchMendokusai
{
	/// <summary>
	/// Motor의 매 tick velocity 합산에 기여하는 모듈 인터페이스.
	/// Input/Gravity/Jump/External impulse/Zone force 등이 모두 이 인터페이스로 통합된다.
	/// </summary>
	public interface IVelocityContributor
	{
		/// <summary>
		/// 현재 tick의 velocity 기여분을 누적한다. context.Velocity를 직접 수정.
		/// 등록 순서대로 호출되며, 뒤 contributor가 앞 contributor의 결과를 보고 판단할 수 있다.
		/// </summary>
		void Contribute(MotorContext context, float deltaTime);
	}
}
