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
		[SerializeField] private GroundMovementTuning groundMovementTuning = new();
		[SerializeField] private TraversalTuning traversalTuning = new();

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
		private CapsulePosture posture;
		private bool crouchRequested;
		public TraversalController Traversal { get; private set; }

		// Hit-stop — motor tick skip 동안 victim 만 멈춤. timescale 안 건드림.
		private float pauseRemaining;
		private float accumulatedSimulationTime;
		public float SimulationInterval { get; private set; }
		public float LastSimulationDeltaTime { get; private set; }

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
		// 렌더 보간 Transform이 아닌 실제 물리 위치. 복귀와 체크포인트 판정용
		public Vector3 Position => unitRigidBody.position;

		/// <summary>
		/// 직전 tick 이 *실제로* 옮긴 거리. <see cref="Velocity"/> 는 sweep 이 깎기 전후가 섞인 값이라
		/// 0 이 아니어도 몸은 한 발도 못 나갔을 수 있다 — 「굳었다」의 원인을 가르려면 이쪽을 봐야 한다.
		/// </summary>
		public Vector3 LastMoveDelta => motor != null ? motor.Context.LastMoveDelta : Vector3.zero;

		/// <summary> 직전 tick 에 벽에 몇 번 닿았나 — 0 이면 막힌 게 아니라 스스로 안 간 것이다. </summary>
		public int WallContactCount => motor != null ? motor.Context.WallContactNormals.Count : 0;

		/// <summary> 직전 tick 에 앞을 막은 그 물체 — 지형인지 다른 유닛인지는 이름을 봐야 갈린다. </summary>
		public Collider LastWallCollider => motor != null ? motor.Context.LastWallCollider : null;

		/// <summary>
		/// 이 몸이 실제로 차지하는 반경(크기 배율 반영). 길찾기는 유닛을 *점*으로 보는데 몸에는 굵기가 있다 —
		/// 몸이 칸보다 굵으면 「갈 수 있다」고 안내받은 칸으로도 못 들어간다.
		/// </summary>
		public float BodyRadius => unitCapsule != null
			? unitCapsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z)
			: 0f;

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
			posture = new CapsulePosture(unitCapsule);

			// Kinematic 캐릭터 — 위치 결정권은 Motor에. Rigidbody는 충돌 트리거 송신용.
			unitRigidBody.isKinematic = true;
			unitRigidBody.useGravity = false;
			// Render frame이 fixed step 사이에 있어도 Unity가 자동 interpolation으로 부드럽게 보여주도록.
			unitRigidBody.interpolation = RigidbodyInterpolation.Interpolate;

			motor = new Motor(transform, unitRigidBody, unitCapsule, motorTuning);
			Traversal = new TraversalController(unitCapsule, traversalTuning);
			motor.OverrideVelocity = OverrideTraversal;
			motor.Context.OnHitCollider = HandleMotorHit;

			// ExternalImpulse는 Input 보다 *먼저* 등록 — horizontal velocity를 먼저 채우고
			// IsExternallyDriven=true 표시. Input은 그 플래그 보고 자기 기여 보류.
			externalImpulse = new ExternalImpulseContributor();
			motor.AddContributor(externalImpulse);
			motor.AddContributor(new InputContributor(unitObject, sprintSpeedMultiplier, groundMovementTuning));
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
			ClearCollisionContacts();
			accumulatedSimulationTime = 0f;
			SimulationInterval = 0f;
			LastSimulationDeltaTime = 0f;
			UpdateLookDirection(Vector3.right);
			unitObject.UnitStat[UnitStatType.IS_JUMPING] = 0;
			jumpContributor?.Reset(IsGrounded());
		}

		private void OnDisable()
		{
			ClearCollisionContacts();
			// 풀 재사용 시 이전 생명의 밀림과 피격 정지 제거
			externalImpulse?.Cancel();
			pauseRemaining = 0f;
		}

		private void ClearCollisionContacts()
		{
			currentHits.Clear();
			previousHits.Clear();
		}

		private void FixedUpdate()
		{
			if (pauseRemaining > 0f || unitObject.IsActionSuspended)
			{
				accumulatedSimulationTime = 0f;
				pauseRemaining = Mathf.Max(0f, pauseRemaining - Time.fixedDeltaTime);
				return;
			}

			accumulatedSimulationTime += Time.fixedDeltaTime;
			if (accumulatedSimulationTime < SimulationInterval &&
				Mathf.Approximately(accumulatedSimulationTime, SimulationInterval) == false)
				return;
			float deltaTime = accumulatedSimulationTime;
			accumulatedSimulationTime = 0f;
			LastSimulationDeltaTime = deltaTime;

			System.Collections.Generic.HashSet<Collider> temp = previousHits;
			previousHits = currentHits;
			currentHits = temp;
			currentHits.Clear();

			MotorContext context = motor.Context;
			context.MoveDirection = MoveDirectionWorld;
			context.BlockedByExternal = IsMovementBlocked();
			if (groundMovementTuning.Enabled)
			{
				posture.SetCrouching(crouchRequested, groundMovementTuning.CrouchHeightFraction);
				unitObject.UnitStat[UnitStatType.IS_CROUCHING] = posture.IsCrouching ? 1 : 0;
			}

			motor.Tick(deltaTime);

			if (jumpContributor.HasPendingLanded)
			{
				OnLanded?.Invoke(jumpContributor.ConsumeLandedImpact());
			}

			unitObject.UnitStat[UnitStatType.IS_JUMPING] = jumpContributor.IsJumping ? 1 : 0;
		}

		public void SetMoveDirection(Vector3 input) => SetMoveDirection(new Vector2(input.x, input.z));

		// 화면 밖 이동도 누적 시간만큼 진행. 0은 원래의 매 물리 tick 갱신.
		public void SetSimulationInterval(float interval) => SimulationInterval = Mathf.Max(0f, interval);

		public void SetMoveDirection(Vector2 input) => SetInputDirection(input.normalized);

		// 플레이어 스틱 크기 보존. AI 방향 명령은 기존 단위 벡터 계약 유지
		public void SetAnalogMoveDirection(Vector2 input) => SetInputDirection(Vector2.ClampMagnitude(input, 1f));

		private void SetInputDirection(Vector2 input)
		{
			float horizontalInput = input.x;
			float verticalInput = input.y;

			MoveDirectionLocal = new Vector3(horizontalInput, 0f, verticalInput);
			MoveDirectionWorld = (horizontalInput * transform.right) + (verticalInput * transform.forward);

			unitObject.SpriteRenderer.flipX = (horizontalInput == 0f) ? unitObject.SpriteRenderer.flipX : (horizontalInput < 0f);

			if (horizontalInput != 0f || verticalInput != 0f)
				UpdateLookDirection(MoveDirectionWorld.normalized);
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
			if (currentHits.Add(other) == false || previousHits.Contains(other))
				return;

			// 콜백 중 재진입해도 바깥 조회 결과를 덮지 않도록 호출별 목록 대여
			System.Collections.Generic.List<IKinematicCollisionReceiver> receivers = UnityEngine.Pool.ListPool<IKinematicCollisionReceiver>.Get();
			try
			{
				other.GetComponentsInParent(false, receivers);
				foreach (IKinematicCollisionReceiver receiver in receivers)
					receiver.OnKinematicCollisionEnter(unitCapsule);

				GetComponents(receivers);
				foreach (IKinematicCollisionReceiver receiver in receivers)
					receiver.OnKinematicCollisionEnter(other);
			}
			finally
			{
				UnityEngine.Pool.ListPool<IKinematicCollisionReceiver>.Release(receivers);
			}
		}

		public void TryJump()
		{
			if (IsMovementBlocked())
				return;
			if (Traversal.HandleJump(motor.Context))
			{
				jumpContributor.Reset(false);
				return;
			}
			jumpContributor.RequestJump();
		}

		public void StopJump()
		{
			jumpContributor.ReleaseJump();
		}

		public void SetCrouching(bool requested)
		{
			if (requested && Traversal != null && Traversal.Mode != TraversalMode.None)
			{
				Traversal.Cancel();
				requested = false;
			}
			crouchRequested = requested;
			if (groundMovementTuning.Enabled == false)
				unitObject.UnitStat[UnitStatType.IS_CROUCHING] = requested ? 1 : 0;
		}

		public void CancelMovementInput()
		{
			MoveDirectionLocal = Vector3.zero;
			MoveDirectionWorld = Vector3.zero;
			unitObject.UnitStat[UnitStatType.IS_SPRINTING] = 0;
			SetCrouching(false);
			jumpContributor?.CancelInput();
			Traversal?.Cancel();
		}

		private bool OverrideTraversal(MotorContext context, float deltaTime)
		{
			Traversal.LocalInput = new Vector2(MoveDirectionLocal.x, MoveDirectionLocal.z);
			Traversal.Facing = LookDirection;
			Traversal.Unavailable = IsExternallyDriven || unitObject.UnitStat[UnitStatType.DEAD] > 0 ||
				unitObject.UnitStat[UnitStatType.IS_CROUCHING] > 0;
			bool overridden = Traversal.Apply(context, deltaTime);
			if (overridden)
				jumpContributor.Reset(false);
			return overridden;
		}

		public void Teleport(Vector3 position)
		{
			ClearCollisionContacts();
			accumulatedSimulationTime = 0f;
			CancelMovementInput();
			externalImpulse.Cancel();
			Traversal.Reset();
			jumpContributor.Reset(false);
			pauseRemaining = 0f;
			motor.Teleport(position);
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
