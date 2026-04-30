using System;
using System.Collections;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 캐릭터 이동 facade. 내부적으로 Kinematic Motor + IVelocityContributor 레이어로 동작.
	/// 외부 API(SetMoveDirection / TryJump / OnLanded / IsLookingRight 등)는 보존.
	///
	/// 진행 상태:
	/// - γ1 ✅ Motor + Input/Gravity contributors + horizontal sweep+slide
	/// - γ2 ✅ JumpContributor — 가변 점프 / coyote / buffer / 착지 impact
	/// - γ3~ slope / step / external impulse / moving platform / zone (미시작)
	/// </summary>
	public class UnitMovement : MonoBehaviour
	{
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

		// Events
		public event Action<float> OnLanded;

		// Runtime properties
		public float MoveTick { get; set; } = 0.02f;

		public Vector3 MoveDirectionLocal { get; private set; }
		public Vector3 MoveDirectionWorld { get; private set; }
		public Vector3 LookDirection { get; private set; }
		public bool IsLookingRight => unitObject.SpriteRenderer.flipX == false;

		/// <summary>
		/// Motor가 이번 tick 결정한 실제 속도. Kinematic Rigidbody는 linearVelocity가 항상 0이라 사용 불가 →
		/// 애니메이션 / VFX / 로직이 "현재 움직이고 있는가?" 판단할 때 이 값을 사용해야 한다.
		/// </summary>
		public Vector3 Velocity => motor != null ? motor.Context.Velocity : Vector3.zero;

		private void Awake()
		{
			unitRigidBody = GetComponent<Rigidbody>();
			unitObject = GetComponent<UnitObject>();
			unitCapsule = GetComponent<CapsuleCollider>();

			// Kinematic 캐릭터 — 위치 결정권은 Motor에. Rigidbody는 충돌 트리거 송신용.
			unitRigidBody.isKinematic = true;
			unitRigidBody.useGravity = false;

			motor = new Motor(transform, unitRigidBody, unitCapsule);
			motor.AddContributor(new InputContributor(unitObject));
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
			StartCoroutine(MoveCoroutine());
		}

		private void OnDisable()
		{
			StopAllCoroutines();
		}

		private IEnumerator MoveCoroutine()
		{
			WaitForSeconds wait = new(MoveTick);
			while (true)
			{
				Move();
				yield return wait;
			}
		}

		private void Move()
		{
			MotorContext context = motor.Context;
			context.MoveDirection = MoveDirectionWorld;
			context.BlockedByExternal = IsMovementBlocked();

			motor.Tick(MoveTick);

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
			return GameManager.Instance.Conditions[GameConditionType.IsTyping] ||
				TimeManager.Instance.IsPaused;
		}

		public bool IsGrounded()
		{
			return motor != null && motor.Context.GroundState == MotorGroundState.Grounded;
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
	}
}
