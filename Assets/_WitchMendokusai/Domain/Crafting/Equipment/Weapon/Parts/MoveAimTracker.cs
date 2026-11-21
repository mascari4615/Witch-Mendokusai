using UnityEngine;

namespace WitchMendokusai
{
	// 마지막 이동 방향을 따라 도는 수평 방향
	// 입력이 멈춰도 마지막 방향을 유지하고, 방향이 바뀌면 각도로 부드럽게 따라 돎
	public sealed class MoveAimTracker
	{
		private const float MIN_DIRECTION_SQUARED = 0.0001f;

		private float targetYaw;
		private float currentYaw;
		private bool initialized;

		public bool HasDirection => initialized;
		public Vector3 Target => FromYaw(targetYaw);
		public Vector3 Current => initialized ? FromYaw(currentYaw) : Vector3.forward;

		public void Reset() => initialized = false;

		// lastMoveDirection 은 입력이 멈춰도 마지막 값이 오는 방향. 추종 속도는 지수 감쇠, 한 프레임 비율 1 - exp(-속도 x 시간)
		public void Tick(Vector3 lastMoveDirection, float followRate, float deltaTime)
		{
			lastMoveDirection.y = 0f;
			if (lastMoveDirection.sqrMagnitude >= MIN_DIRECTION_SQUARED)
			{
				targetYaw = Mathf.Atan2(lastMoveDirection.x, lastMoveDirection.z) * Mathf.Rad2Deg;
				if (initialized == false)
				{
					currentYaw = targetYaw;
					initialized = true;
					return;
				}
			}
			if (initialized == false)
				return;
			float blend = followRate <= 0f ? 1f : 1f - Mathf.Exp(-followRate * deltaTime);
			currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, blend);
		}

		public static Vector3 FromYaw(float yawDegrees)
		{
			float radians = yawDegrees * Mathf.Deg2Rad;
			return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
		}
	}
}
