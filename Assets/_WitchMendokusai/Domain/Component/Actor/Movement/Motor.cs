using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// Kinematic Character Motor — sweep 기반 위치 결정 엔진.
	/// 캐릭터는 자기 위치를 자기가 결정한다. Rigidbody는 isKinematic=true / useGravity=false 전제.
	///
	/// 구조 (TASK-WM-029 — vertical 을 horizontal slide-iterate 프레임워크에서 분리):
	/// - Horizontal: <see cref="SweepAndSlide"/> — capsule cast 기반 collide-and-slide.
	///   Step offset / crease handling / wall slide / floor tangent slide 포함.
	/// - Vertical descent: <see cref="SweepDescend"/> — 발 중심 raycast 로 walkable + stable floor 찾기.
	///   CapsuleCast 의 sphere edge 가 cliff face 모서리를 잡는 spurious contact 회피.
	/// - Vertical ascent: <see cref="SweepAscend"/> — 머리 중심 raycast 로 ceiling 찾기.
	/// - Depenetration via ComputePenetration (시작 + sweep 후).
	/// - Ground stick (직전 grounded면 발 아래 짧은 거리 sweep 으로 ground 찾아 snap).
	/// </summary>
	public class Motor
	{
		// 알고리즘 파라미터 — 디자인 조절값이 아니라 구현 상수라 여기 남는다 (TASK-WM-199).
		// 조작감을 정하는 수치는 MotorTuning 으로 나갔다.
		private const int MAX_SLIDE_ITERATIONS = 4;
		private const float CAPSULE_SHRINK = 0.99f;
		private const float MIN_REMAINING_SQR = 0.0001f;
		private const int MAX_DEPENETRATION_ITERATIONS = 4;
		private const float MIN_CREASE_SIN_SQR = 0.01f;      // 두 wall normal 외적 크기 제곱이 이 값 이상이면 crease 처리
		private const float MIN_STEP_MAGNITUDE = 0.001f;     // step offset 시도할 잔여 horizontal 이동 최소량

		// 조작감 수치 — 매 사용 시 read (캐싱하면 인스펙터 변경이 런타임에 안 먹는다).
		private float SkinWidth => tuning.SkinWidth;
		private float WallNormalYMax => tuning.WallNormalYMax;
		private float GroundNormalYMin => tuning.GroundNormalYMin;
		private float GroundSnapDistance => tuning.GroundSnapDistance;
		private float GroundTouchDistance => tuning.GroundTouchDistance;
		private float StepOffsetHeight => tuning.StepOffsetHeight;
		private float StabilityProbeDistance => tuning.StabilityProbeDistance;

		private static readonly RaycastHit[] HIT_BUFFER = new RaycastHit[8];
		private static readonly Collider[] OVERLAP_BUFFER = new Collider[16];

		private readonly Transform unitTransform;
		private readonly Rigidbody unitRigidBody;
		private readonly CapsuleCollider unitCapsule;
		private readonly MotorTuning tuning;
		private readonly List<IVelocityContributor> contributors = new();
		private readonly MotorContext context = new();

		public MotorContext Context => context;

		public Motor(Transform unitTransform, Rigidbody unitRigidBody, CapsuleCollider unitCapsule, MotorTuning tuning)
		{
			this.unitTransform = unitTransform;
			this.unitRigidBody = unitRigidBody;
			this.unitCapsule = unitCapsule;
			this.tuning = tuning;
		}

		public void AddContributor(IVelocityContributor contributor) => contributors.Add(contributor);

		public void Tick(float deltaTime)
		{
			Vector3 position = unitRigidBody.position;
			Depenetrate(ref position);

			context.Position = position;
			context.ResetPerTick();

			// 직전 tick 이 남긴 접지 상태 = 이번 tick 에 「붙여도 되는지」의 유일한 근거.
			// (예전엔 wasGroundedPrevTick 이라는 별도 플래그가 이 일만 하고 있었다.)
			bool wasGrounded = context.GroundState == MotorGroundState.Grounded;
			ResolveGround(ref position, wasGrounded);
			bool groundedAtTickStart = context.GroundState == MotorGroundState.Grounded;

			for (int i = 0; i < contributors.Count; i++)
				contributors[i].Contribute(context, deltaTime);

			Vector3 velocity = context.Velocity;
			Vector3 horizontalDelta = new(velocity.x * deltaTime, 0f, velocity.z * deltaTime);
			float verticalDeltaY = velocity.y * deltaTime;

			Vector3 newPosition = SweepAndSlide(position, horizontalDelta);
			if (verticalDeltaY < 0f)
				newPosition = SweepDescend(newPosition, verticalDeltaY);
			else if (verticalDeltaY > 0f)
				newPosition = SweepAscend(newPosition, verticalDeltaY);

			// 이동이 끝난 *최종* 위치에서 같은 규칙으로 접지를 다시 결정한다.
			// 시작 위치 기준 판정만 쓰면 수평 이동 뒤 stale 이 된다 (TASK-WM-029-B).
			ResolveGround(ref newPosition, groundedAtTickStart);

			Depenetrate(ref newPosition);

			unitRigidBody.MovePosition(newPosition);
			context.LastMoveDelta = newPosition - position;
			context.Position = newPosition;
		}

		/// <summary>
		/// 접지 판정 단일 정본 (TASK-WM-029-B). 발 밑으로 한 번 훑어서 세 가지를 *같이* 결정한다:
		/// ① Grounded / Airborne ② ground normal ③ **표면에 붙이는 위치 보정**.
		///
		/// 왜 붙이기까지 여기서 하나 — 판정과 보정이 따로 놀면 「Grounded 인데 공중에 뜬」 상태가 생긴다.
		/// 예전 구조가 정확히 그랬다: 판정은 발 밑 0.2m 까지를 접지로 쳐주는데, Grounded 가 되는 순간
		/// 중력은 0 으로 눌리고 snap 은 Airborne 일 때만 돌았다 → 그 간격을 닫아주는 주체가 없었다.
		/// 실측(EditMode): 낮은 턱을 내려가면 0.1m 뜬 채로 걷고, 낙하 뒤엔 0.14m 위에 멈춰 섰다.
		///
		/// <paramref name="allowSnapDown"/> = 직전에 서 있었나. 서 있었다면 계단·낮은 턱을 따라
		/// <see cref="GroundSnapDistance"/> 까지 붙여 내려가고, 아니었다면(낙하 중) 발끝에 정말
		/// 닿았을 때만 접지로 친다 — 떨어지는 캐릭터를 공중에서 낚아채지 않도록.
		/// 위로 솟는 중(점프)이면 어떤 경우에도 안 붙인다. 붙이면 점프가 도로 땅에 눌러앉는다.
		/// </summary>
		private void ResolveGround(ref Vector3 position, bool allowSnapDown)
		{
			GetCapsuleEnds(position, out Vector3 capsuleBottom, out _, out float radius);
			Vector3 feet = capsuleBottom - Vector3.up * radius;
			Vector3 origin = feet + Vector3.up * SkinWidth;

			bool rising = context.Velocity.y > 0f;
			float reach = (allowSnapDown && rising == false) ? GroundSnapDistance : GroundTouchDistance;
			float castDistance = SkinWidth + reach;

			int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, HIT_BUFFER, castDistance, ~0, QueryTriggerInteraction.Ignore);

			float closestDistance = float.PositiveInfinity;
			int closestIndex = -1;
			for (int i = 0; i < hitCount; i++)
			{
				RaycastHit candidate = HIT_BUFFER[i];
				if (candidate.collider == null)
					continue;
				if (candidate.collider.transform.IsChildOf(unitTransform))
					continue;
				if (candidate.distance < closestDistance)
				{
					closestDistance = candidate.distance;
					closestIndex = i;
				}
			}

			if (closestIndex < 0 || IsWalkable(HIT_BUFFER[closestIndex]) == false)
			{
				context.GroundState = MotorGroundState.Airborne;
				context.HasGroundNormal = false;
				return;
			}

			RaycastHit hit = HIT_BUFFER[closestIndex];

			// 표면 위로 내려놓는다 — 「떠 있는 Grounded」를 없애는 대목.
			//
			// ★ 얼마나 내려놓을지는 ray 가 아니라 *캡슐* 이 정한다. 발 중심 ray 거리만큼 내리면
			//   비탈에서 캡슐 아랫부분이 오르막 쪽 지면을 파고들고, 그걸 Depenetrate 가 법선
			//   방향(비탈에선 옆으로도 향한다)으로 밀어낸다 → 스냅과 밀어내기가 매 tick 싸우며
			//   흘러내린다. 실측: 30° 비탈에서 60 tick 에 2.3m.
			//   ray 는 「땅 위에 있나」를, 캡슐 sweep 은 「어디까지 내려갈 수 있나」를 답한다.
			float dropDistance = 0f;
			if (CapsuleSweep(position, Vector3.down, reach + SkinWidth, out RaycastHit capsuleHit))
				dropDistance = Mathf.Max(0f, capsuleHit.distance - SkinWidth);

			if (dropDistance > 0f)
				position += Vector3.down * dropDistance;

			context.GroundState = MotorGroundState.Grounded;
			context.GroundNormal = hit.normal;
			context.HasGroundNormal = true;
			if (context.Velocity.y < 0f)
				context.Velocity.y = 0f;
			context.Position = position;
		}

		/// <summary>
		/// 캡슐이 다른 collider와 겹쳐있으면 ComputePenetration으로 빼낸다. 최대 N회 수렴.
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
					if (other.transform.IsChildOf(unitTransform))
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
						context.OnHitCollider.Invoke(other);
						totalCorrection += direction * distance;
						anyCorrection = true;
					}
				}

				if (anyCorrection == false)
					return;

				position += totalCorrection;
			}
		}

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

		/// <summary>
		/// 발 정 직 아래로 짧은 raycast — walkable ground 있으면 stable. 절벽 끝 capsule 일부만 걸친 경우는
		/// capsule sweep은 hit이지만 정 직 아래 ray는 miss → unstable. 표준 KCC stability 기준.
		/// capsuleBottom은 capsule segment center(sphere center)이므로 실제 발은 그보다 radius만큼 아래.
		/// </summary>
		private bool IsStableGroundDirectlyBelow(Vector3 capsuleBottom, float radius)
		{
			Vector3 feet = capsuleBottom - Vector3.up * radius;
			Vector3 origin = feet + Vector3.up * SkinWidth;
			float distance = SkinWidth + StabilityProbeDistance;
			int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, HIT_BUFFER, distance, ~0, QueryTriggerInteraction.Ignore);

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
				return false;
			return HIT_BUFFER[closestIndex].normal.y >= GroundNormalYMin;
		}

		private bool IsWalkable(RaycastHit hit)
		{
			return hit.normal.y >= GroundNormalYMin;
		}
	}
}
