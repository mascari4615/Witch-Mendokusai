using System;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 캐릭터 이동 facade. 내부적으로 Kinematic Motor + IVelocityContributor 레이어로 동작.
	/// 외부 API(SetMoveDirection / TryJump / OnLanded / IsLookingRight 등)는 보존.
	///
	/// 진행 상태:
	/// - γ1 ✅ Motor + Input/Gravity contributors + horizontal sweep+slide
	/// - γ2 ✅ JumpContributor — 가변 점프 / coyote / buffer / 착지 impact
	/// - γ3 ✅ slope(walkable 한계 + tangent slide) / step offset / ExternalImpulseContributor(대시·넉백)
	/// - γ4 ⬜ moving platform / zone force — **미시작** (관련 코드 0)
	///
	/// 접지 판정은 <c>Motor.ResolveGround</c> 단일 정본 (TASK-WM-029-B, 2026-08-05).
	/// 회귀 그물 = <c>Tests/EditMode/Movement/</c> 36건 — 이동 쪽을 손대면 그걸 먼저 돌릴 것.
	/// </summary>
	public class UnitMovement : MonoBehaviour
	{
		// Move tuning.
		[Header("Move Tuning")]
		[SerializeField] private float sprintSpeedMultiplier = 2f;

		// 캐릭터가 지형을 어떻게 밟는지 — 오를 수 있는 턱 높이 / 걸을 수 있는 경사 / 계단 따라 붙는 거리 등.
		// 지형 제작의 암묵 규칙이 되는 값들이라 prefab 별로 다르게 줄 수 있어야 한다 (TASK-WM-199).
		[SerializeField] private MotorTuning motorTuning = new();

		// Jump tuning. 디폴트는 *비점프 unit 중립 값* (multiplier 1.0 = 추가 중력 없음).
		// 점프하는 unit(Player 등)은 prefab에서 오버라이드.
		[Header("Jump Tuning")]
		[SerializeField] private float jumpForce = 5.6f;
		[SerializeField] private float fallGravityMultiplier = 1f;
		[SerializeField] private float lowJumpGravityMultiplier = 1f;
		[SerializeField] private float coyoteTime = 0.1f;
		[SerializeField] private float jumpBufferTime = 0.12f;
		[SerializeField] private float landingImpactMinFallSpeed = 1.2f;
		[SerializeField] private float landingImpactMaxFallSpeed = 8f;

		// Cached components
		protected Rigidbody unitRigidBody;
		protected UnitObject unitObject;
		private CapsuleCollider unitCapsule;

		// Movement core
		private Motor motor;
		private JumpContributor jumpContributor;
		private ExternalImpulseContributor externalImpulse;

		// Hit-stop — motor tick skip 동안 victim 만 멈춤. timescale 안 건드림.
		private float pauseRemaining;

		private System.Collections.Generic.HashSet<Collider> currentHits = new();
		private System.Collections.Generic.HashSet<Collider> previousHits = new();

		// Events
		public event Action<float> OnLanded;

		public Vector3 MoveDirectionLocal { get; private set; }
		public Vector3 MoveDirectionWorld { get; private set; }
		public Vector3 LookDirection { get; private set; }
		public bool IsLookingRight => unitObject.SpriteRenderer.flipX == false;

		/// <summary>
		/// Motor가 이번 tick 결정한 실제 속도. Kinematic Rigidbody는 linearVelocity가 항상 0이라 사용 불가 →
		/// 애니메이션 / VFX / 로직이 "현재 움직이고 있는가?" 판단할 때 이 값을 사용해야 한다.
		/// </summary>
		public Vector3 Velocity => motor != null ? motor.Context.Velocity : Vector3.zero;

		/// <summary>
		/// 직전 tick 이 *실제로* 옮긴 거리. <see cref="Velocity"/> 는 sweep 이 깎기 전후가 섞인 값이라
		/// 0 이 아니어도 몸은 한 발도 못 나갔을 수 있다 — 「굳었다」의 원인을 가르려면 이쪽을 봐야 한다.
		/// </summary>
		public Vector3 LastMoveDelta => motor != null ? motor.Context.LastMoveDelta : Vector3.zero;

		/// <summary> 직전 tick 에 벽에 몇 번 닿았나 — 0 이면 막힌 게 아니라 스스로 안 간 것이다. </summary>
		public int WallContactCount => motor != null ? motor.Context.WallContactNormals.Count : 0;

		private GameManager gameManager;
		private TimeManager timeManager;

		[Inject]
		public void Construct(GameManager gameManager, TimeManager timeManager)
		{
			this.gameManager = gameManager;
			this.timeManager = timeManager;
		}

		private void Awake()
		{
			unitRigidBody = GetComponent<Rigidbody>();
			unitObject = GetComponent<UnitObject>();
			unitCapsule = GetComponent<CapsuleCollider>();

			// Kinematic 캐릭터 — 위치 결정권은 Motor에. Rigidbody는 충돌 트리거 송신용.
			unitRigidBody.isKinematic = true;
			unitRigidBody.useGravity = false;
			// Render frame이 fixed step 사이에 있어도 Unity가 자동 interpolation으로 부드럽게 보여주도록.
			unitRigidBody.interpolation = RigidbodyInterpolation.Interpolate;

			motor = new Motor(transform, unitRigidBody, unitCapsule, motorTuning);
			motor.Context.OnHitCollider = HandleMotorHit;

			// ExternalImpulse는 Input 보다 *먼저* 등록 — horizontal velocity를 먼저 채우고
			// IsExternallyDriven=true 표시. Input은 그 플래그 보고 자기 기여 보류.
			externalImpulse = new ExternalImpulseContributor();
			motor.AddContributor(externalImpulse);
			motor.AddContributor(new InputContributor(unitObject, sprintSpeedMultiplier));
			motor.AddContributor(new GravityContributor());

			jumpContributor = new JumpContributor(
				unitObject,
				jumpForce,
				fallGravityMultiplier,
				lowJumpGravityMultiplier,
				coyoteTime,
				jumpBufferTime,
				landingImpactMinFallSpeed,
				landingImpactMaxFallSpeed);
			motor.AddContributor(jumpContributor);
		}

		private void OnEnable()
		{
			UpdateLookDirection(Vector3.right);
			unitObject.UnitStat[UnitStatType.IS_JUMPING] = 0;
			jumpContributor?.Reset(IsGrounded());
		}

		private void FixedUpdate()
		{
			if (pauseRemaining > 0f)
			{
				pauseRemaining -= Time.fixedDeltaTime;
				return;
			}

			System.Collections.Generic.HashSet<Collider> temp = previousHits;
			previousHits = currentHits;
			currentHits = temp;
			currentHits.Clear();

			MotorContext context = motor.Context;
			context.MoveDirection = MoveDirectionWorld;
			context.BlockedByExternal = IsMovementBlocked();

			motor.Tick(Time.fixedDeltaTime);

			if (jumpContributor.HasPendingLanded)
			{
				OnLanded?.Invoke(jumpContributor.ConsumeLandedImpact());
			}

			unitObject.UnitStat[UnitStatType.IS_JUMPING] = jumpContributor.IsJumping ? 1 : 0;
		}

		public void SetMoveDirection(Vector3 input) => SetMoveDirection(new Vector2(input.x, input.z));

		public void SetMoveDirection(Vector2 input)
		{
			float horizontalInput = input.x;
			float verticalInput = input.y;

			MoveDirectionLocal = new Vector3(horizontalInput, 0f, verticalInput).normalized;
			MoveDirectionWorld = ((horizontalInput * transform.right) + (verticalInput * transform.forward)).normalized;

			unitObject.SpriteRenderer.flipX = (horizontalInput == 0f) ? unitObject.SpriteRenderer.flipX : (horizontalInput < 0f);

			if (horizontalInput != 0f || verticalInput != 0f)
				UpdateLookDirection(MoveDirectionWorld);
		}

		private void UpdateLookDirection(Vector3 newDirection)
		{
			LookDirection = newDirection;
		}

		private bool IsMovementBlocked()
		{
			return gameManager.Conditions[GameConditionType.IsTyping] ||
				timeManager.IsPaused;
		}

		public bool IsGrounded()
		{
			return motor != null && motor.Context.GroundState == MotorGroundState.Grounded;
		}

		private void HandleMotorHit(Collider other)
		{
			if (other == null) return;
			
			if (currentHits.Add(other))
			{
				if (previousHits.Contains(other) == false)
				{
					foreach (IKinematicCollisionReceiver receiver in other.GetComponentsInParent<IKinematicCollisionReceiver>())
						receiver.OnKinematicCollisionEnter(unitCapsule);

					foreach (IKinematicCollisionReceiver receiver in GetComponents<IKinematicCollisionReceiver>())
						receiver.OnKinematicCollisionEnter(other);
				}
			}
		}

		public void TryJump()
		{
			if (IsMovementBlocked())
				return;
			jumpContributor.RequestJump();
		}

		public void StopJump()
		{
			jumpContributor.ReleaseJump();
		}

		/// <summary>
		/// 외부 horizontal impulse 적용 (dash / knockback / 폭발 등). duration 동안 InputContributor의
		/// horizontal 기여를 덮어쓰고 점프를 차단한다.
		/// </summary>
		public void ApplyImpulse(Vector3 worldHorizontalVelocity, float duration)
		{
			externalImpulse?.Push(worldHorizontalVelocity, duration);
		}

		public void CancelImpulse()
		{
			externalImpulse?.Cancel();
		}

		public bool IsExternallyDriven => externalImpulse != null && externalImpulse.IsActive;

		/// <summary>
		/// Hit-stop 등으로 이 unit 만 잠깐 정지. timescale 손대지 않음. 매 호출은 *latest wins* 가 아닌 *max* —
		/// 짧은 hitstop 진행 중에 더 긴 hitstop 들어오면 더 긴 쪽으로 연장.
		/// </summary>
		public void Pause(float duration)
		{
			if (duration <= 0f)
				return;
			pauseRemaining = Mathf.Max(pauseRemaining, duration);
		}

		public bool IsPaused => pauseRemaining > 0f;
	}
}
