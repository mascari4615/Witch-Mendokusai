using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// Motor 의 쓸어 밀기 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 Motor.cs 를 본다.
	public partial class Motor
	{
		private Vector3 SweepAndSlide(Vector3 startPosition, Vector3 delta)
		{
			Vector3 currentPosition = startPosition;
			Vector3 remaining = delta;
			bool stepOffsetAttempted = false;

			for (int iteration = 0; iteration < MAX_SLIDE_ITERATIONS; iteration++)
			{
				if (remaining.sqrMagnitude < MIN_REMAINING_SQR)
					break;

				Vector3 direction = remaining.normalized;
				float distance = remaining.magnitude + SkinWidth;

				if (CapsuleSweep(currentPosition, direction, distance, out RaycastHit hit) == false)
				{
					currentPosition += remaining;
					break;
				}

				float moveDistance = Mathf.Max(0f, hit.distance - SkinWidth);
				currentPosition += direction * moveDistance;

				Vector3 used = direction * moveDistance;
				Vector3 leftover = remaining - used;

				bool isWall = Mathf.Abs(hit.normal.y) < WallNormalYMax;
				bool isFloor = hit.normal.y >= GroundNormalYMin;

				if (isWall)
				{
					// 무엇이 막았는지 남긴다 — 「격자는 갈 수 있다는데 몸은 못 간다」의 범인을 이름으로 묻기 위해.
					context.LastWallCollider = hit.collider;

					// Step offset 시도 (한 sweep 당 한 번만): wall hit을 stepHeight 위로 들어올려 재sweep, walkable이면 step-up.
					if (stepOffsetAttempted == false &&
						TryStepOffset(currentPosition, direction, leftover.magnitude, out Vector3 steppedPosition))
					{
						stepOffsetAttempted = true;
						currentPosition = steppedPosition;
						remaining = Vector3.zero;
						break;
					}

					// Crease handling: 직전 wall normal과 외적 ≠ 0이면 corner. 잔여 velocity를 crease 방향으로 projection.
					if (context.WallContactNormals.Count > 0)
					{
						Vector3 previousWallNormal = context.WallContactNormals[context.WallContactNormals.Count - 1];
						Vector3 creaseDirection = Vector3.Cross(previousWallNormal, hit.normal);
						if (creaseDirection.sqrMagnitude > MIN_CREASE_SIN_SQR)
						{
							creaseDirection.Normalize();
							leftover = Vector3.Dot(leftover, creaseDirection) * creaseDirection;

							Vector3 horizontalVelocity = new(context.Velocity.x, 0f, context.Velocity.z);
							Vector3 horizontalVelocityProjected = Vector3.Dot(horizontalVelocity, creaseDirection) * creaseDirection;
							context.Velocity.x = horizontalVelocityProjected.x;
							context.Velocity.z = horizontalVelocityProjected.z;

							context.WallContactNormals.Add(hit.normal);
							remaining = leftover;
							continue;
						}
					}

					context.WallContactNormals.Add(hit.normal);

					// Wall tangent projection
					leftover -= Vector3.Dot(leftover, hit.normal) * hit.normal;
					leftover.y = 0f;

					Vector3 horizontalVelocityWall = new(context.Velocity.x, 0f, context.Velocity.z);
					horizontalVelocityWall -= Vector3.Dot(horizontalVelocityWall, hit.normal) * hit.normal;
					context.Velocity.x = horizontalVelocityWall.x;
					context.Velocity.z = horizontalVelocityWall.z;

					remaining = leftover;
					continue;
				}

				if (isFloor)
				{
					// 약간 비스듬한 바닥 horizontal 이동 — 바닥 tangent로 슬라이드
					leftover -= Vector3.Dot(leftover, hit.normal) * hit.normal;
					remaining = leftover;
					continue;
				}

				// 그 외 (예: ceiling — horizontal sweep 에선 사실상 발생 안 함) — 안전 break.
				break;
			}

			return currentPosition;
		}

		/// <summary>
		/// Slope sliding 헬퍼 — velocity의 surface normal 방향 (면 안으로 들어가는) 성분만 제거.
		/// 결과: capsule이 면을 따라 미끄러짐.
		/// </summary>
		private void SlideAlongSurface(Vector3 surfaceNormal)
		{
			Vector3 currentVelocity = context.Velocity;
			float velocityIntoSurface = Vector3.Dot(currentVelocity, surfaceNormal);
			if (velocityIntoSurface < 0f)
				context.Velocity = currentVelocity - velocityIntoSurface * surfaceNormal;
		}

		/// <summary>
		/// 떨어지는 캐릭터의 vertical 이동 — 발 중심 raycast 로 walkable + stable floor 찾기.
		/// CapsuleCast 의 sphere edge 가 cliff face 모서리를 잡는 spurious contact (TASK-WM-029) 회피.
		/// 발 중심 ray 라 capsule volume 밖 모서리는 안 잡고, 정 직 아래에 *진짜* ground 있을 때만 land.
		/// </summary>
		private Vector3 SweepDescend(Vector3 startPosition, float verticalDeltaY)
		{
			GetCapsuleEnds(startPosition, out Vector3 capsuleBottom, out _, out float radius);
			Vector3 feet = capsuleBottom - Vector3.up * radius;
			Vector3 origin = feet + Vector3.up * SkinWidth;
			float fallDistance = -verticalDeltaY;
			float castDistance = fallDistance + SkinWidth * 2f;

			int hitCount = Physics.RaycastNonAlloc(
				origin,
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
				if (hit.collider.transform.IsChildOf(unitTransform))
					continue;
				if (hit.distance < closestDistance)
				{
					closestDistance = hit.distance;
					closestIndex = i;
				}
			}

			if (closestIndex < 0)
			{
				// 발 정 직 아래 fallDistance 안에 surface 없음 — 자연 낙하 (Airborne, JumpContributor 가 g 누적).
				context.GroundState = MotorGroundState.Airborne;
				context.HasGroundNormal = false;
				return startPosition + Vector3.down * fallDistance;
			}

			RaycastHit closestHit = HIT_BUFFER[closestIndex];
			context.OnHitCollider.Invoke(closestHit.collider);

			float rayMoveDistance = Mathf.Max(0f, closestHit.distance - SkinWidth);
			Vector3 stoppedPosition = startPosition + Vector3.down * rayMoveDistance;

			if (IsWalkable(closestHit))
			{
				// Walkable — land grounded.
				context.Velocity.y = 0f;
				context.GroundState = MotorGroundState.Grounded;
				context.GroundNormal = closestHit.normal;
				context.HasGroundNormal = true;
				return stoppedPosition;
			}

			// 비-walkable surface (가파른 경사 등) — 충돌 정지 + slope tangent slide. 다음 tick 에 g 가 다시 누적.
			SlideAlongSurface(closestHit.normal);
			context.GroundState = MotorGroundState.Airborne;
			context.HasGroundNormal = false;
			return stoppedPosition;
		}

		/// <summary>
		/// 올라가는 캐릭터의 vertical 이동 — 머리 중심 raycast 로 ceiling 찾기. hit 시 vy=0 + 그 위치 정지.
		/// </summary>
		private Vector3 SweepAscend(Vector3 startPosition, float verticalDeltaY)
		{
			GetCapsuleEnds(startPosition, out _, out Vector3 capsuleTop, out float radius);
			Vector3 head = capsuleTop + Vector3.up * radius;
			Vector3 origin = head - Vector3.up * SkinWidth;
			float riseDistance = verticalDeltaY;
			float castDistance = riseDistance + SkinWidth * 2f;

			int hitCount = Physics.RaycastNonAlloc(
				origin,
				Vector3.up,
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
				if (hit.collider.transform.IsChildOf(unitTransform))
					continue;
				if (hit.distance < closestDistance)
				{
					closestDistance = hit.distance;
					closestIndex = i;
				}
			}

			if (closestIndex < 0)
				return startPosition + Vector3.up * riseDistance;

			RaycastHit closestHit = HIT_BUFFER[closestIndex];
			context.OnHitCollider.Invoke(closestHit.collider);

			float moveDistance = Mathf.Max(0f, closestHit.distance - SkinWidth);
			context.Velocity.y = 0f;
			return startPosition + Vector3.up * moveDistance;
		}

		/// <summary>
		/// 작은 턱/계단 자동 보행. capsule을 StepOffsetHeight 위로 들어올려 horizontal sweep,
		/// 미스 후 그 위치에서 down sweep으로 walkable + stable ground를 찾으면 그 위치 채택.
		/// </summary>
		private bool TryStepOffset(Vector3 position, Vector3 direction, float magnitude, out Vector3 result)
		{
			result = position;
			if (magnitude < MIN_STEP_MAGNITUDE)
				return false;

			// horizontal sweep direction 정규화 — y 성분 제거
			Vector3 flatDirection = new(direction.x, 0f, direction.z);
			if (flatDirection.sqrMagnitude < MIN_REMAINING_SQR)
				return false;
			flatDirection.Normalize();

			Vector3 raisedPosition = position + Vector3.up * StepOffsetHeight;

			// raised 위치에서 horizontal sweep — 막히면 step-up 불가
			float sweepDistance = magnitude + SkinWidth;
			if (CapsuleSweep(raisedPosition, flatDirection, sweepDistance, out _))
				return false;

			Vector3 horizontallyMoved = raisedPosition + flatDirection * magnitude;

			// 그 위치에서 down sweep으로 ground 찾기
			float downDistance = StepOffsetHeight + SkinWidth;
			if (CapsuleSweep(horizontallyMoved, Vector3.down, downDistance, out RaycastHit downHit) == false)
				return false;
			if (IsWalkable(downHit) == false)
				return false;

			float downMove = Mathf.Max(0f, downHit.distance - SkinWidth);
			Vector3 finalPosition = horizontallyMoved + Vector3.down * downMove;

			GetCapsuleEnds(finalPosition, out Vector3 capsuleBottom, out _, out float radius);
			if (IsStableGroundDirectlyBelow(capsuleBottom, radius) == false)
				return false;

			result = finalPosition;
			context.GroundState = MotorGroundState.Grounded;
			context.GroundNormal = downHit.normal;
			context.HasGroundNormal = true;
			return true;
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
				if (hit.collider.transform.IsChildOf(unitTransform))
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
			context.OnHitCollider.Invoke(closestHit.collider);
			return true;
		}

		/// <summary>
		/// 캡슐 양 끝점 + 반지름을 world 좌표계로 계산.
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
