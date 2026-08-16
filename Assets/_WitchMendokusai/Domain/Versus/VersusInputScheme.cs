using System;
using UnityEngine.InputSystem;

namespace WitchMendokusai
{
	/// <summary>
	/// 대결 축 v0 의 2인 입력 (TASK-WM-411). 기존 <see cref="InputManager"/> 는 1인용 전역 전제(맵 하나·전역 이벤트)라
	/// 「한 키보드에 둘」이 성립하지 않는다. 그래서 v0 는 자기 입력을 직접 들고 다닌다 — 본편 입력 경로를 건드리지 않는다.
	/// New Input System 만 사용하며 <c>Keyboard.current</c> 폴링은 하지 않는다(바인딩 경로로 만든 InputAction).
	/// </summary>
	public sealed class VersusInputScheme : IVersusInput
	{
		private readonly InputAction moveAction;
		private readonly InputAction fireAction;
		private readonly InputAction dashAction;

		private VersusInputScheme(InputAction moveAction, InputAction fireAction, InputAction dashAction)
		{
			this.moveAction = moveAction;
			this.fireAction = fireAction;
			this.dashAction = dashAction;

			this.moveAction.Enable();
			this.fireAction.Enable();
			this.dashAction.Enable();
		}

		/// <summary> 1P — 키보드 왼쪽(WASD + F 발사 + G 대시) 또는 패드 1. </summary>
		public static VersusInputScheme CreatePlayerOne()
		{
			InputAction move = new InputAction("VersusMove1", InputActionType.Value);
			move.AddCompositeBinding("2DVector")
				.With("Up", "<Keyboard>/w")
				.With("Down", "<Keyboard>/s")
				.With("Left", "<Keyboard>/a")
				.With("Right", "<Keyboard>/d");
			move.AddBinding("<Gamepad>/leftStick");

			InputAction fire = new InputAction("VersusFire1", InputActionType.Button, "<Keyboard>/f");
			fire.AddBinding("<Gamepad>/rightTrigger");

			InputAction dash = new InputAction("VersusDash1", InputActionType.Button, "<Keyboard>/g");
			dash.AddBinding("<Gamepad>/buttonSouth");

			return new VersusInputScheme(move, fire, dash);
		}

		/// <summary> 2P — 키보드 오른쪽(방향키 + 오른쪽Ctrl 발사 + 오른쪽Shift 대시) 또는 패드 2. </summary>
		public static VersusInputScheme CreatePlayerTwo()
		{
			InputAction move = new InputAction("VersusMove2", InputActionType.Value);
			move.AddCompositeBinding("2DVector")
				.With("Up", "<Keyboard>/upArrow")
				.With("Down", "<Keyboard>/downArrow")
				.With("Left", "<Keyboard>/leftArrow")
				.With("Right", "<Keyboard>/rightArrow");
			// 패드가 두 개 꽂히면 두 번째 패드가 2P — 경로 인덱스로 기기를 가른다(같은 스킴 중복 X).
			move.AddBinding("<Gamepad>{2}/leftStick");

			InputAction fire = new InputAction("VersusFire2", InputActionType.Button, "<Keyboard>/rightCtrl");
			fire.AddBinding("<Gamepad>{2}/rightTrigger");

			InputAction dash = new InputAction("VersusDash2", InputActionType.Button, "<Keyboard>/rightShift");
			dash.AddBinding("<Gamepad>{2}/buttonSouth");

			return new VersusInputScheme(move, fire, dash);
		}

		/// <summary> 이동 입력(-1~1). </summary>
		public UnityEngine.Vector2 ReadMove() => moveAction.ReadValue<UnityEngine.Vector2>();

		/// <summary> 발사 = 꾹 눌러도 연사(간격은 스탯이 정한다). </summary>
		public bool IsFireHeld => fireAction.IsPressed();

		/// <summary> 이번 프레임에 눌렸나 — 카드 고르기 확정·대시에 쓴다. </summary>
		public bool WasFirePressedThisFrame => fireAction.WasPressedThisFrame();

		public bool WasDashPressedThisFrame => dashAction.WasPressedThisFrame();

		/// <summary> 사람 손은 매 프레임 생각할 것이 없다 — 인터페이스를 맞추기 위한 빈 구현. </summary>
		public void Tick(float deltaTime)
		{
		}

		public void Dispose()
		{
			moveAction.Disable();
			fireAction.Disable();
			dashAction.Disable();
			moveAction.Dispose();
			fireAction.Dispose();
			dashAction.Dispose();
		}
	}
}
