using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum MotorGroundState
	{
		Airborne = 0,
		Grounded = 1,
		// 추후 확장: SlopeSliding, Swimming, Climbing
	}

	/// <summary>
	/// Motor가 매 tick 갱신·contributor가 읽고 쓰는 공유 상태.
	/// Tick 시작 시 ResetPerTick()으로 일회성 데이터 초기화.
	/// </summary>
	public class MotorContext
	{
		// 위치/속도
		public Vector3 Position;
		public Vector3 Velocity;

		// 지면 상태
		public MotorGroundState GroundState;
		public Vector3 GroundNormal;
		public bool HasGroundNormal;

		// 입력 (외부에서 매 tick 주입)
		public Vector3 MoveDirection;

		// 게임 컨디션 (typing / paused / dead 등) 으로 입력이 차단된 상태. InputContributor가 horizontal=0 set.
		public bool BlockedByExternal;

		// ExternalImpulseContributor가 horizontal velocity를 채우는 중 (dash / knockback 등).
		// Input은 자기 기여 보류, Jump는 점프 차단. ExternalImpulseContributor가 매 tick set/clear.
		public bool IsExternallyDriven;

		// 이번 tick 발생한 벽 충돌 노멀들 (디버그/이벤트 송출용)
		public readonly List<Vector3> WallContactNormals = new();

		// ★ 이번 tick 이 *실제로* 옮긴 거리. Velocity 는 「가려던 속도」를 sweep 이 깎은 값이라
		//   0 이 아니어도 몸은 한 발도 못 나갈 수 있다 — 둘을 같이 봐야 「벽에 눌림」과 「왕복」이 갈린다
		//   (실측: 전속 1.60 인데 4초 동안 제자리인 개체가 있었다).
		public Vector3 LastMoveDelta;

		public System.Action<Collider> OnHitCollider = delegate { };

		public void ResetPerTick()
		{
			WallContactNormals.Clear();
		}
	}
}
