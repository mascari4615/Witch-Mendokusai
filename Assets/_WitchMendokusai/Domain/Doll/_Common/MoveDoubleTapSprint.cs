using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 같은 방향 이동 입력을 짧게 두 번 누르면 달리기 (사용자 2026-11-18 "이동이 빠르게 두 번 누르면 달리기 상태 되게")
	/// - 입력은 InputManager.MoveInput 만 읽음. 키보드 직접 접근 없음
	/// - 첫 누름과 두 번째 누름 사이가 창 안이고 방향이 비슷하면 달리기 시작, 이동을 놓으면 풀림
	/// - 수치는 매 호출 인자로 받음. 인스펙터에서 바꾸면 바로 반영
	/// </summary>
	public class MoveDoubleTapSprint
	{
		private bool wasMoving;
		private float lastPressTime = float.NegativeInfinity;
		private Vector2 lastPressDirection;

		public bool IsSprinting { get; private set; }

		public void Tick(Vector2 moveInput, float now, bool isEnabled, float windowSeconds, float sameDirectionMinDot, float pressThreshold)
		{
			bool isMoving = moveInput.magnitude >= pressThreshold;
			if (isEnabled == false)
			{
				IsSprinting = false;
				wasMoving = isMoving;
				return;
			}

			if (isMoving && wasMoving == false)
			{
				Vector2 direction = moveInput.normalized;
				bool isQuick = now - lastPressTime <= windowSeconds;
				bool isSameDirection = Vector2.Dot(direction, lastPressDirection) >= sameDirectionMinDot;
				if (isQuick && isSameDirection)
					IsSprinting = true;

				lastPressTime = now;
				lastPressDirection = direction;
			}
			else if (isMoving == false)
			{
				IsSprinting = false;
			}

			wasMoving = isMoving;
		}

		public void Cancel()
		{
			IsSprinting = false;
			wasMoving = false;
			lastPressTime = float.NegativeInfinity;
		}
	}
}
