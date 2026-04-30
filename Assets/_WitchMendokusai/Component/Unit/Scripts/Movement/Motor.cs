using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// Kinematic Character Motor — sweep+slide 기반 위치 결정 엔진.
	/// 캐릭터는 자기 위치를 자기가 결정한다. Rigidbody는 isKinematic=true / useGravity=false 전제.
	/// </summary>
	public class Motor
	{
		private const int MAX_SLIDE_ITERATIONS = 4;
		private const float SKIN_WIDTH = 0.02f;
		private const float WALL_NORMAL_Y_MAX = 0.5f;       // |normal.y| < 이 값 = 벽
		private const float GROUND_NORMAL_Y_MIN = 0.5f;     // normal.y >= 이 값 = 바닥
		private const float GROUND_PROBE_DISTANCE = 0.15f;  // 지면 감지 sphere cast 거리
		private const float GROUND_PROBE_LIFT = 0.05f;      // sphere가 지면과 시작 overlap 되지 않도록 origin 들어올림 buffer
		private const float CAPSULE_SHRINK = 0.99f;         // sweep용 캡슐 수축 (자기 collider 즉시 overlap 회피)
		private const float MIN_REMAINING_SQR = 0.0001f;
		private const int MAX_DEPENETRATION_ITERATIONS = 4;

		private static readonly RaycastHit[] HIT_BUFFER = new RaycastHit[8];
		private static readonly Collider[] OVERLAP_BUFFER = new Collider[16];

		private readonly Transform unitTransform;
		private readonly Rigidbody unitRigidBody;
		private readonly CapsuleCollider unitCapsule;
		private readonly List<IVelocityContributor> contributors = new();
		private readonly MotorContext context = new();

		public MotorContext Context => context;

		public Motor(Transform unitTransform, Rigidbody unitRigidBody, CapsuleCollider unitCapsule)
		{
			this.unitTransform = unitTransform;
			this.unitRigidBody = unitRigidBody;
			this.unitCapsule = unitCapsule;
		}

		public void AddContributor(IVelocityContributor contributor) => contributors.Add(contributor);

		public void Tick(float deltaTime)
		{
			Vector3 position = unitRigidBody.position;

			// Depenetration: 다른 collider와 겹쳐있으면 빼내고 시작
			Depenetrate(ref position);

			context.Position = position;
			context.ResetPerTick();
			DetectGround(position);

			for (int i = 0; i < contributors.Count; i++)
				contributors[i].Contribute(context, deltaTime);

			Vector3 velocity = context.Velocity;
			Vector3 horizontalDelta = new(velocity.x * deltaTime, 0f, velocity.z * deltaTime);
			Vector3 verticalDelta = new(0f, velocity.y * deltaTime, 0f);

			Vector3 newPosition = SweepAndSlide(position, horizontalDelta, isVertical: false);
			newPosition = SweepAndSlide(newPosition, verticalDelta, isVertical: true);

			// Final depenetration: sweep 후에도 겹쳐있으면 빼냄
			Depenetrate(ref newPosition);

			unitRigidBody.MovePosition(newPosition);
			context.Position = newPosition;
		}

		/// <summary>
		/// 캡슐이 다른 collider와 겹쳐있으면 ComputePenetration으로 빼낸다.
		/// 여러 겹침이 있을 수 있어 최대 N회 반복 (수렴).
		/// </summary>
		private void Depenetrate(ref Vector3 position)
		{
			for (int iteration = 0; iteration < MAX_DEPENETRATION_ITERATIONS; iteration++)
			{
				GetCapsuleEnds(position, out Vector3 capsuleBottom, out Vector3 capsuleTop, out float radius);

				int overlapCount = Physics.OverlapCapsuleNonAlloc(
					capsuleBottom,
					capsuleTop,
					radius * CAPSULE_SHRINK,
					OVERLAP_BUFFER,
					~0,
					QueryTriggerInteraction.Ignore);

				if (overlapCount == 0)
					return;

				Vector3 totalCorrection = Vector3.zero;
				bool anyCorrection = false;

				for (int i = 0; i < overlapCount; i++)
				{
					Collider other = OVERLAP_BUFFER[i];
					if (other == null)
						continue;
					if (other.transform.root == unitTransform.root)
						continue;

					if (Physics.ComputePenetration(
						unitCapsule,
						position,
						unitTransform.rotation,
						other,
						other.transform.position,
						other.transform.rotation,
						out Vector3 direction,
						out float distance))
					{
						totalCorrection += direction * distance;
						anyCorrection = true;
					}
				}

				if (anyCorrection == false)
					return;

				position += totalCorrection;
			}
		}

		/// <summary>
		/// 발 아래 ground 감지. SphereCast의 origin을 sphere 반지름 + buffer만큼 들어올려 시작 overlap 회피.
		/// 즉 sphere 바닥이 발 위 GROUND_PROBE_LIFT만큼 떠 있는 상태에서 아래로 sweep.
		/// </summary>
		private void DetectGround(Vector3 fromPosition)
		{
			GetCapsuleEnds(fromPosition, out Vector3 capsuleBottom, out _, out float radius);

			float sphereRadius = radius * CAPSULE_SHRINK;
			float liftAboveFeet = sphereRadius + GROUND_PROBE_LIFT;
			Vector3 origin = capsuleBottom + Vector3.up * liftAboveFeet;
			float castDistance = liftAboveFeet + GROUND_PROBE_DISTANCE;

			int hitCount = Physics.SphereCastNonAlloc(
				origin,
				sphereRadius,
				Vector3.down,
				HIT_BUFFER,
				castDistance,
				~0,
				QueryTriggerInteraction.Ignore);

			float closestDistance = float.PositiveInfinity;
			int closestIndex = -1;
			for (int i = 0; i < hitCount; i++)
			{
				RaycastHit hit = HIT_BUFFER[i];
				if (hit.collider == null)
					continue;
				if (hit.collider.transform.root == unitTransform.root)
					continue;
				if (hit.normal.y < GROUND_NORMAL_Y_MIN)
					continue;
				if (hit.distance < closestDistance)
				{
					closestDistance = hit.distance;
					closestIndex = i;
				}
			}

			if (closestIndex >= 0)
			{
				context.GroundState = MotorGroundState.Grounded;
				context.GroundNormal = HIT_BUFFER[closestIndex].normal;
				context.HasGroundNormal = true;
			}
			else
			{
				context.GroundState = MotorGroundState.Airborne;
				context.HasGroundNormal = false;
			}
		}

		private Vector3 SweepAndSlide(Vector3 startPosition, Vector3 delta, bool isVertical)
		{
			Vector3 currentPosition = startPosition;
			Vector3 remaining = delta;

			for (int iteration = 0; iteration < MAX_SLIDE_ITERATIONS; iteration++)
			{
				if (remaining.sqrMagnitude < MIN_REMAINING_SQR)
					break;

				Vector3 direction = remaining.normalized;
				float distance = remaining.magnitude + SKIN_WIDTH;

				if (CapsuleSweep(currentPosition, direction, distance, out RaycastHit hit) == false)
				{
					currentPosition += remaining;
					break;
				}

				float moveDistance = Mathf.Max(0f, hit.distance - SKIN_WIDTH);
				currentPosition += direction * moveDistance;

				Vector3 used = direction * moveDistance;
				Vector3 leftover = remaining - used;

				bool isWall = Mathf.Abs(hit.normal.y) < WALL_NORMAL_Y_MAX;
				bool isFloor = hit.normal.y >= GROUND_NORMAL_Y_MIN;
				bool isCeiling = hit.normal.y <= -GROUND_NORMAL_Y_MIN;

				if (isVertical)
				{
					if (isFloor)
					{
						context.Velocity.y = 0f;
						context.GroundState = MotorGroundState.Grounded;
						context.GroundNormal = hit.normal;
						context.HasGroundNormal = true;
						break;
					}
					if (isCeiling)
					{
						context.Velocity.y = 0f;
						break;
					}
					// vertical 이동 중 벽에 닿는 케이스 — 거의 없음. 안전 break.
					break;
				}

				if (isWall)
				{
					context.WallContactNormals.Add(hit.normal);

					// 잔여 이동을 벽 tangent로 projection
					leftover -= Vector3.Dot(leftover, hit.normal) * hit.normal;
					leftover.y = 0f;

					// horizontal velocity도 동기 — 벽 노멀 성분 제거 (다음 contributor 입력 시 재누적되도록)
					Vector3 horizontalVelocity = new(context.Velocity.x, 0f, context.Velocity.z);
					horizontalVelocity -= Vector3.Dot(horizontalVelocity, hit.normal) * hit.normal;
					context.Velocity.x = horizontalVelocity.x;
					context.Velocity.z = horizontalVelocity.z;

					remaining = leftover;
					continue;
				}

				if (isFloor)
				{
					// 약간 비스듬한 바닥 위로 horizontal 이동 — 바닥 tangent로 슬라이드
					leftover -= Vector3.Dot(leftover, hit.normal) * hit.normal;
					remaining = leftover;
					continue;
				}

				// 그 외 (가파른 경사 등 — γ3에서 처리). 일단 안전 break.
				break;
			}

			return currentPosition;
		}

		private bool CapsuleSweep(Vector3 origin, Vector3 direction, float distance, out RaycastHit closestHit)
		{
			GetCapsuleEnds(origin, out Vector3 capsuleBottom, out Vector3 capsuleTop, out float radius);

			int hitCount = Physics.CapsuleCastNonAlloc(
				capsuleBottom,
				capsuleTop,
				radius * CAPSULE_SHRINK,
				direction,
				HIT_BUFFER,
				distance,
				~0,
				QueryTriggerInteraction.Ignore);

			float closestDistance = float.PositiveInfinity;
			int closestIndex = -1;
			for (int i = 0; i < hitCount; i++)
			{
				RaycastHit hit = HIT_BUFFER[i];
				if (hit.collider == null)
					continue;
				if (hit.collider.transform.root == unitTransform.root)
					continue;
				if (hit.distance < closestDistance)
				{
					closestDistance = hit.distance;
					closestIndex = i;
				}
			}

			if (closestIndex < 0)
			{
				closestHit = default;
				return false;
			}
			closestHit = HIT_BUFFER[closestIndex];
			return true;
		}

		/// <summary>
		/// 캡슐 양 끝점 + 반지름을 world 좌표계로 계산. originPosition 기준으로 정렬.
		/// </summary>
		private void GetCapsuleEnds(Vector3 originPosition, out Vector3 capsuleBottom, out Vector3 capsuleTop, out float radius)
		{
			Vector3 worldCenter = originPosition + unitTransform.TransformVector(unitCapsule.center);
			float verticalScale = unitTransform.lossyScale.y;
			float halfSegment = Mathf.Max(0f, (unitCapsule.height * 0.5f - unitCapsule.radius) * verticalScale);
			capsuleTop = worldCenter + Vector3.up * halfSegment;
			capsuleBottom = worldCenter - Vector3.up * halfSegment;
			float horizontalScale = Mathf.Max(unitTransform.lossyScale.x, unitTransform.lossyScale.z);
			radius = unitCapsule.radius * horizontalScale;
		}
	}
}
