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

		public void ResetPerTick()
		{
			WallContactNormals.Clear();
		}
	}
}
