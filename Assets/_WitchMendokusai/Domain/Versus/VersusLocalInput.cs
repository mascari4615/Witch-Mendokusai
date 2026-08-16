using UnityEngine;
using UnityEngine.InputSystem;

namespace WitchMendokusai
{
	/// <summary>
	/// 이 컴퓨터 앞에 앉은 사람의 손 (TASK-WM-411). <b>2컴 온라인 대전</b>이라 한 대에 한 명이고,
	/// 그래서 조준축을 줄 수 있다 — 마우스(또는 패드 오른쪽 스틱)로 겨눈다. ROUNDS 와 같은 결.
	///
	/// 기존 <see cref="InputManager"/> 는 본편용 전역 1인 전제라 여기서는 쓰지 않는다(본편 입력 경로 무변경).
	/// New Input System 만 사용한다.
	/// </summary>
	public sealed class VersusLocalInput : IVersusInput
	{
		private readonly InputAction moveAction;
		private readonly InputAction fireAction;
		private readonly InputAction dashAction;
		private readonly InputAction pointAction;
		private readonly InputAction stickAimAction;
		private readonly Camera viewCamera;

		public VersusLocalInput(Camera viewCamera)
		{
			this.viewCamera = viewCamera;

			moveAction = new InputAction("VersusMove", InputActionType.Value);
			moveAction.AddCompositeBinding("2DVector")
				.With("Up", "<Keyboard>/w")
				.With("Down", "<Keyboard>/s")
				.With("Left", "<Keyboard>/a")
				.With("Right", "<Keyboard>/d");
			moveAction.AddBinding("<Gamepad>/leftStick");

			fireAction = new InputAction("VersusFire", InputActionType.Button, "<Mouse>/leftButton");
			fireAction.AddBinding("<Gamepad>/rightTrigger");

			dashAction = new InputAction("VersusDash", InputActionType.Button, "<Keyboard>/space");
			dashAction.AddBinding("<Gamepad>/buttonSouth");

			pointAction = new InputAction("VersusPoint", InputActionType.Value, "<Mouse>/position");
			stickAimAction = new InputAction("VersusStickAim", InputActionType.Value, "<Gamepad>/rightStick");

			moveAction.Enable();
			fireAction.Enable();
			dashAction.Enable();
			pointAction.Enable();
			stickAimAction.Enable();
		}

		public VersusInputFrame Read(VersusRoundState state, int selfIndex, float deltaTime)
		{
			Vector2 move = moveAction.ReadValue<Vector2>();
			Vector2 stick = stickAimAction.ReadValue<Vector2>();
			Vector2 aim = stick.sqrMagnitude > 0.16f ? stick : AimFromPointer(state, selfIndex);

			return new VersusInputFrame
			{
				Move = new Numerics.Vector2(move.x, move.y),
				Aim = new Numerics.Vector2(aim.x, aim.y),
				Fire = fireAction.IsPressed(),
				Dash = dashAction.WasPressedThisFrame(),
			};
		}

		public void Dispose()
		{
			moveAction.Dispose();
			fireAction.Dispose();
			dashAction.Dispose();
			pointAction.Dispose();
			stickAimAction.Dispose();
		}

		// 화면의 마우스 자리를 판 바닥(y=0)으로 내려 「내가 저기를 겨눈다」로 바꾼다.
		// 쿼터뷰 고정 카메라라 바닥 평면 한 번 교차로 충분하다.
		private Vector2 AimFromPointer(VersusRoundState state, int selfIndex)
		{
			if (viewCamera == null)
				return Vector2.right;

			Ray ray = viewCamera.ScreenPointToRay(pointAction.ReadValue<Vector2>());

			if (Mathf.Approximately(ray.direction.y, 0f))
				return Vector2.right;

			float distance = -ray.origin.y / ray.direction.y;
			Vector3 ground = ray.origin + ray.direction * distance;

			Numerics.Vector2 self = state.PositionOf(selfIndex);
			Vector2 toPointer = new Vector2(ground.x - self.x, ground.z - self.y);
			return toPointer.sqrMagnitude > 0.0001f ? toPointer.normalized : Vector2.right;
		}
	}
}
