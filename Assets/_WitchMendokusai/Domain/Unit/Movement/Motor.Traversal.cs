using UnityEngine;

namespace WitchMendokusai
{
	public partial class Motor
	{
		// 등반과 올라서기는 수직 ray 대신 전신 캡슐 sweep. 턱 자동 오르기와 ground snap 제외
		private Vector3 SweepTraversal(Vector3 position, Vector3 remaining)
		{
			for (int i = 0; i < MAX_SLIDE_ITERATIONS; i++)
			{
				float distance = remaining.magnitude;
				if (remaining.sqrMagnitude < MIN_REMAINING_SQR)
					break;
				Vector3 direction = remaining / distance;
				if (CapsuleSweep(position, direction, distance + SkinWidth, out RaycastHit hit) == false)
					return position + remaining;
				float allowed = Mathf.Clamp(hit.distance - SkinWidth, 0f, distance);
				position += direction * allowed;
				remaining = Vector3.ProjectOnPlane(remaining - direction * allowed, hit.normal);
				context.Velocity = Vector3.ProjectOnPlane(context.Velocity, hit.normal);
				context.LastWallCollider = hit.collider;
			}
			return position;
		}

		public void Teleport(Vector3 position)
		{
			unitRigidBody.position = position;
			unitTransform.position = position;
			context.Position = position;
			context.Velocity = Vector3.zero;
			context.LastMoveDelta = Vector3.zero;
			context.GroundState = MotorGroundState.Airborne;
			context.HasGroundNormal = false;
			context.SuppressGrounding = false;
			context.FullBodySweep = false;
			context.ResetPerTick();
		}

		// 걷던 상태를 두고 자리만 옮김. 보간 Rigidbody 는 transform 만 쓰면 다음 프레임에 옛 자리로 되돌림
		public void Reposition(Vector3 position)
		{
			unitRigidBody.position = position;
			unitTransform.position = position;
			context.Position = position;
		}
	}
}
