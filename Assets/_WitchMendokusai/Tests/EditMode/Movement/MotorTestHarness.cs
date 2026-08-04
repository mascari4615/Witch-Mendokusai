using System;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// KCC <see cref="Motor"/> 를 EditMode 에서 결정적으로 굴리는 하네스 (TASK-WM-049).
	///
	/// 왜 있나 — Motor 는 596 줄짜리 캐릭터 이동 심장인데 회귀 그물이 0 이었다. TASK-028 γ3 가
	/// 「표준 KCC 패턴 다 흡수」라고 했는데도 TASK-029(절벽 끝 떠있음)가 빠져나갔다. 같은 bug class 가
	/// 또 빠져나가는 걸 막으려면 *재현 가능한* 판정 수단이 먼저 있어야 한다. 수동 인게임 플레이 +
	/// 우연한 목격은 판정 수단이 아니다.
	///
	/// ★ Rigidbody 적분 우회 — 이 하네스가 정직하려면 여기를 알아야 한다.
	/// Motor 는 tick 끝에 <c>Rigidbody.MovePosition(newPosition)</c> 을 부르고, 다음 tick 첫 줄에서
	/// <c>Rigidbody.position</c> 을 다시 읽는다. Play 에서는 그 사이 FixedUpdate 물리 스텝이 끼어들어
	/// 둘이 같은 값이 되지만, EditMode 에는 그 스텝이 없다. 그래서 하네스는 결과를 MovePosition 이 아니라
	/// Motor 가 스스로 기록한 <see cref="MotorContext.Position"/> 에서 읽고, 다음 스텝 전에 그 값을
	/// Rigidbody·Transform 에 직접 써넣는다 = **Play 의 계약을 그대로 재현하되 물리 스텝에 의존하지 않는다.**
	/// 이건 우회지 조작이 아니다 — kinematic Rigidbody 에 MovePosition 한 결과가 다음 FixedUpdate 의
	/// position 이라는 것이 Play 의 실제 계약이고, 하네스는 그 계약만 대신 이행한다.
	///
	/// 사용:
	/// <code>
	/// using (MotorTestHarness harness = new(new Vector3(0f, 5f, 0f)))
	/// {
	///     harness.AddGround(Vector3.zero, new Vector3(20f, 1f, 20f));
	///     harness.AddContributor(new GravityContributor());
	///     harness.StepMany(60);
	///     Assert.That(harness.IsGrounded, Is.True);
	/// }
	/// </code>
	/// </summary>
	public sealed class MotorTestHarness : IDisposable
	{
		/// <summary>캐릭터 캡슐 높이. 발이 transform.position 에 오도록 center 를 잡는다.</summary>
		public const float CAPSULE_HEIGHT = 2f;
		public const float CAPSULE_RADIUS = 0.5f;

		/// <summary>Play 의 기본 물리 스텝과 같은 값 — tick 수를 실제 시간으로 환산해서 읽을 수 있게.</summary>
		public const float FIXED_DELTA_TIME = 0.02f;

		/// <summary>하네스 작업 반경. 생성 시 이 반경 안에 남의 콜라이더가 있으면 씬 오염이므로 즉시 실패시킨다.</summary>
		private const float WORKSPACE_RADIUS = 200f;

		private const int POLLUTION_BUFFER_SIZE = 8;

		private readonly GameObject characterObject;
		private readonly GameObject geometryRoot;
		private readonly Transform characterTransform;
		private readonly Rigidbody characterRigidBody;

		private bool disposed;

		public Motor Motor { get; }

		public MotorContext Context => Motor.Context;

		/// <summary>Motor 가 마지막 tick 에 결정한 위치. 캐릭터 *발* 위치이기도 하다.</summary>
		public Vector3 Position { get; private set; }

		public bool IsGrounded => Context.GroundState == MotorGroundState.Grounded;

		public MotorTestHarness(Vector3 startPosition)
		{
			AssertWorkspaceClean(startPosition);

			geometryRoot = new GameObject("MotorTestHarness.Geometry");
			geometryRoot.transform.position = Vector3.zero;

			characterObject = new GameObject("MotorTestHarness.Character");
			characterTransform = characterObject.transform;
			characterTransform.position = startPosition;

			CapsuleCollider capsule = characterObject.AddComponent<CapsuleCollider>();
			capsule.height = CAPSULE_HEIGHT;
			capsule.radius = CAPSULE_RADIUS;
			// 발이 transform.position 에 오도록 — GetCapsuleEnds 가 bottom - radius 를 발로 본다.
			capsule.center = new Vector3(0f, CAPSULE_HEIGHT * 0.5f, 0f);

			characterRigidBody = characterObject.AddComponent<Rigidbody>();
			characterRigidBody.isKinematic = true;
			characterRigidBody.useGravity = false;
			characterRigidBody.position = startPosition;

			Physics.SyncTransforms();

			Position = startPosition;
			Motor = new Motor(characterTransform, characterRigidBody, capsule);
		}

		public void AddContributor(IVelocityContributor contributor) => Motor.AddContributor(contributor);

		/// <summary>축 정렬 정적 바닥/벽/천장 상자.</summary>
		public GameObject AddGround(Vector3 center, Vector3 size) => AddBox(center, size, Quaternion.identity);

		/// <summary>
		/// 경사면. <paramref name="slopeDegrees"/> 만큼 X 축 둘레로 기울인 상자 —
		/// 윗면 normal 의 y 가 cos(slopeDegrees) 가 되므로 walkable 임계(0.5 = 60°) 를 정확히 겨눌 수 있다.
		/// </summary>
		public GameObject AddSlope(Vector3 center, Vector3 size, float slopeDegrees)
			=> AddBox(center, size, Quaternion.Euler(slopeDegrees, 0f, 0f));

		public GameObject AddBox(Vector3 center, Vector3 size, Quaternion rotation)
		{
			GameObject box = new("MotorTestHarness.Box");
			box.transform.SetParent(geometryRoot.transform, worldPositionStays: true);
			box.transform.SetPositionAndRotation(center, rotation);
			box.transform.localScale = size;

			box.AddComponent<BoxCollider>();

			Physics.SyncTransforms();
			return box;
		}

		/// <summary>이번 tick 캐릭터가 가려는 수평 방향·속력. 다음 <see cref="Step"/> 에 반영된다.</summary>
		public void SetHorizontalIntent(Vector3 direction, float speed)
		{
			Vector3 flat = new(direction.x, 0f, direction.z);
			if (flat.sqrMagnitude > 0f)
				flat.Normalize();

			Context.MoveDirection = flat;
			HorizontalSpeed = speed;
		}

		/// <summary><see cref="SetHorizontalIntent"/> 가 정한 속력. <see cref="ConstantHorizontalContributor"/> 가 읽는다.</summary>
		public float HorizontalSpeed { get; private set; }

		/// <summary>
		/// 수직 속도를 직접 준다 = 점프 한 방. 실물 <see cref="JumpContributor"/> 는 UnitObject 스탯과
		/// coyote/buffer 타이머까지 끌고 오므로, 상승 sweep 만 보고 싶을 때는 이쪽이 맞다.
		/// (Grounded 상태에서도 GravityContributor 는 vy&gt;0 을 건드리지 않는다 — 음수만 0 으로 누른다.)
		/// </summary>
		public void SetVerticalVelocity(float velocityY) => Context.Velocity.y = velocityY;

		/// <summary>
		/// 한 물리 스텝 진행. Play 의 FixedUpdate → Motor.Tick 한 바퀴에 대응한다.
		/// </summary>
		public Vector3 Step(float deltaTime = FIXED_DELTA_TIME)
		{
			// Play 에서 MovePosition 결과가 다음 tick 의 Rigidbody.position 이 되는 계약을 대신 이행.
			// 쓴 직후 곧바로 SyncTransforms — PhysX 에 안 밀어넣으면 이번 tick 의 sweep 이 *옛 위치*를 본다.
			// (autoSyncTransforms 에 맡기지 않는다. deprecated 이기도 하고, 암묵 동기화에 판정을 걸면
			//  나중에 그 기본값이 바뀔 때 시험이 조용히 거짓말한다.)
			characterTransform.position = Position;
			characterRigidBody.position = Position;
			Physics.SyncTransforms();

			Motor.Tick(deltaTime);

			Position = Context.Position;
			return Position;
		}

		public Vector3 StepMany(int stepCount, float deltaTime = FIXED_DELTA_TIME)
		{
			for (int i = 0; i < stepCount; i++)
				Step(deltaTime);

			return Position;
		}

		/// <summary>
		/// 씬에 남의 콜라이더가 떠 있으면 Motor 의 sweep 이 그걸 잡아 테스트가 조용히 거짓말한다.
		/// 「0 개여야 한다」를 생성 시점에 못 박아, 오염을 테스트 실패가 아니라 하네스 실패로 드러낸다.
		/// </summary>
		private static void AssertWorkspaceClean(Vector3 startPosition)
		{
			Collider[] buffer = new Collider[POLLUTION_BUFFER_SIZE];
			int count = Physics.OverlapSphereNonAlloc(
				startPosition,
				WORKSPACE_RADIUS,
				buffer,
				~0,
				QueryTriggerInteraction.Ignore);

			if (count == 0)
				return;

			string first = buffer[0] == null ? "(null)" : buffer[0].name;
			throw new InvalidOperationException(
				$"MotorTestHarness: 작업 반경 {WORKSPACE_RADIUS}m 안에 남의 콜라이더 {count} 개 (예: {first}). " +
				"열린 씬이 비어있지 않다 — Motor sweep 이 그걸 잡으면 판정이 조용히 오염된다.");
		}

		public void Dispose()
		{
			if (disposed)
				return;

			disposed = true;

			if (characterObject != null)
				UnityEngine.Object.DestroyImmediate(characterObject);
			if (geometryRoot != null)
				UnityEngine.Object.DestroyImmediate(geometryRoot);
		}
	}

	/// <summary>
	/// 테스트용 수평 입력 contributor. 실물 <see cref="InputContributor"/> 는 <c>UnitObject</c>(스탯·DI 딸린
	/// MonoBehaviour) 를 요구해서 Motor 단독 판정에 끌어들이면 검증 대상이 흐려진다. 여기서는 Motor 만 겨눈다.
	///
	/// 실물의 계약 중 *하나는* 그대로 지킨다 — <see cref="MotorContext.IsExternallyDriven"/> 이면 손 뗀다.
	/// 대시·넉백이 도는 동안 입력이 수평 속도를 덮으면 임펄스가 무력화되기 때문이고,
	/// 그걸 안 지키는 대역을 쓰면 임펄스 시험이 실제와 다른 걸 재게 된다.
	/// </summary>
	public sealed class ConstantHorizontalContributor : IVelocityContributor
	{
		private readonly MotorTestHarness harness;

		public ConstantHorizontalContributor(MotorTestHarness harness)
		{
			this.harness = harness;
		}

		public void Contribute(MotorContext context, float deltaTime)
		{
			if (context.IsExternallyDriven)
				return;

			Vector3 direction = context.MoveDirection;
			context.Velocity.x = direction.x * harness.HorizontalSpeed;
			context.Velocity.z = direction.z * harness.HorizontalSpeed;
		}
	}
}
