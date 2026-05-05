using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// Kinematic Character Controller(KCC)인 Motor가 이동 중 다른 Collider와 닿았을 때
	/// Unity의 기본 OnCollisionEnter를 대체하여 발생하는 이벤트를 수신하는 인터페이스입니다.
	/// </summary>
	public interface IKinematicCollisionReceiver
	{
		void OnKinematicCollisionEnter(Collider other);
	}
}
