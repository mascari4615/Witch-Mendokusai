using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace WitchMendokusai
{
	/// <summary>
	/// 「가리키는 것」 하나 — 마우스든 손가락이든 게임 쪽에는 같은 얼굴로 보인다 (TASK-WM-200).
	///
	/// ★ 근본: 모바일 지원을 「터치용 코드를 따로 붙이기」로 하면 같은 개념(가리킨 자리 / 눌렀나)이
	///   두 군데 살게 된다 — 이 프로젝트가 이미 여러 번 앓은 병이다. 대신 *가리킨다*는 뜻을 한 곳에
	///   두고, 그 아래에서 장치가 갈린다. 그래서 배치·판매·미리보기 같은 게임 코드는 손댈 것이 없다.
	///
	/// ★ 마우스 우선: 컴퓨터에 터치 화면이 붙어 있어도(노트북) 마우스를 쓰는 동안은 마우스가 이긴다.
	///   「손가락 모드」는 *마지막으로 만진 장치*로 정한다 — 기기 종류로 정하면 터치 노트북에서 틀린다.
	/// </summary>
	public sealed class PointerDevice
	{
		private readonly TouchGesture gesture = new();
		private readonly List<Vector2> touchPositions = new();

		/// <summary> 지금 손가락으로 조작 중인가 — 화면 조작 UI 를 띄울지, 가장자리 밀기를 끌지의 단일 판정. </summary>
		public bool IsTouchMode { get; private set; }

		/// <summary> 가리키는 화면 좌표 (좌하단 원점). </summary>
		public Vector2 Position { get; private set; }

		/// <summary> 누르고 있나 (마우스 왼쪽 / 손가락 닿음). </summary>
		public bool IsPressed { get; private set; }

		/// <summary> 보조 누름 (마우스 오른쪽). 손가락엔 없다 — 있는 척하지 않는다. </summary>
		public bool IsSecondaryPressed { get; private set; }

		/// <summary> 이번 프레임에 「톡」이 끝났다. 마우스는 짧은 왼쪽 클릭이 톡이다. </summary>
		public bool TappedThisFrame { get; private set; }

		/// <summary> 톡이 일어난 자리. </summary>
		public Vector2 TapPosition { get; private set; }

		/// <summary> 눌린 채 끌고 있다 (한 손가락 / 마우스 왼쪽 드래그). </summary>
		public bool IsDragging { get; private set; }

		/// <summary> 이번 프레임 끌린 양. 손가락 수가 바뀐 프레임은 0. </summary>
		public Vector2 DragDelta { get; private set; }

		/// <summary> 확대·축소 요청 — 마우스는 휠, 손가락은 오므리기(픽셀을 휠 눈금 크기로 환산). </summary>
		public float ZoomDelta { get; private set; }

		/// <summary> 두 손가락 끌기 (지도·부감 시점 옮기기용). 마우스엔 없다. </summary>
		public Vector2 TwoFingerPanDelta { get; private set; }

		/// <summary> 두 손가락 비틀기(도) — 시점 회전용. </summary>
		public float TwistDelta { get; private set; }

		/// <summary> 시야 회전용 델타 — 마우스는 이동량, 손가락은 끌기량. </summary>
		public Vector2 LookDelta { get; private set; }

		public TouchGestureTuning Tuning
		{
			get => gesture.Tuning;
			set => gesture.Tuning = value;
		}

		/// <summary> 손가락 픽셀을 휠 눈금으로 바꾸는 비율 — 기기 밀도마다 달라서 값으로 꺼내 둔다. </summary>
		public float PinchToZoomScale { get; set; } = 0.5f;

		/// <summary>
		/// 한 프레임 분을 읽는다. 마우스가 이번 프레임에 움직이거나 눌렸으면 마우스 모드로,
		/// 손가락이 닿아 있으면 손가락 모드로 넘어간다.
		/// </summary>
		public void Update(float deltaSeconds)
		{
			ReadTouches();
			gesture.Update(touchPositions, deltaSeconds);

			bool touchActive = touchPositions.Count > 0;
			bool mouseActive = IsMouseActingThisFrame();

			if (touchActive)
				IsTouchMode = true;
			else if (mouseActive)
				IsTouchMode = false;

			if (IsTouchMode)
				ReadFromTouch();
			else
				ReadFromMouse();
		}

		private void ReadTouches()
		{
			touchPositions.Clear();

			Touchscreen screen = Touchscreen.current;
			if (screen == null)
				return;

			IReadOnlyList<TouchControl> touches = screen.touches;
			for (int i = 0; i < touches.Count; i++)
			{
				TouchControl touch = touches[i];
				if (touch.press.isPressed)
					touchPositions.Add(touch.position.ReadValue());
			}
		}

		private static bool IsMouseActingThisFrame()
		{
			Mouse mouse = Mouse.current;
			if (mouse == null)
				return false;

			return mouse.delta.ReadValue().sqrMagnitude > 0f
				|| mouse.leftButton.isPressed
				|| mouse.rightButton.isPressed
				|| mouse.middleButton.isPressed
				|| mouse.scroll.ReadValue().sqrMagnitude > 0f;
		}

		private void ReadFromTouch()
		{
			Position = gesture.PrimaryPosition;
			IsPressed = gesture.IsPressed;
			IsSecondaryPressed = false;
			TappedThisFrame = gesture.TappedThisFrame;
			TapPosition = gesture.TapPosition;
			IsDragging = gesture.IsDragging;
			DragDelta = gesture.TouchCount == 1 ? gesture.PrimaryDelta : Vector2.zero;
			ZoomDelta = gesture.PinchDelta * PinchToZoomScale;
			TwoFingerPanDelta = gesture.TwoFingerPanDelta;
			TwistDelta = gesture.TwistDelta;
			LookDelta = DragDelta;
		}

		private void ReadFromMouse()
		{
			Mouse mouse = Mouse.current;
			if (mouse == null)
			{
				IsPressed = false;
				IsSecondaryPressed = false;
				TappedThisFrame = false;
				IsDragging = false;
				DragDelta = Vector2.zero;
				ZoomDelta = 0f;
				TwoFingerPanDelta = Vector2.zero;
				TwistDelta = 0f;
				LookDelta = Vector2.zero;
				return;
			}

			Position = mouse.position.ReadValue();
			IsPressed = mouse.leftButton.isPressed;
			IsSecondaryPressed = mouse.rightButton.isPressed;
			// 마우스는 「눌렀다 뗐다」가 곧 톡이다 — 시간·흔들림을 재지 않는다(마우스는 안 흔들린다).
			TappedThisFrame = mouse.leftButton.wasReleasedThisFrame;
			TapPosition = Position;
			LookDelta = mouse.delta.ReadValue();
			IsDragging = mouse.leftButton.isPressed;
			DragDelta = IsDragging ? LookDelta : Vector2.zero;
			ZoomDelta = mouse.scroll.ReadValue().y;
			TwoFingerPanDelta = Vector2.zero;
			TwistDelta = 0f;
		}
	}
}
