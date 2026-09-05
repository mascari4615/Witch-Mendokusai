using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace WitchMendokusai
{
	// InputManager 의 이동과 카메라 입력 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 InputManager.cs 를 본다.
	public partial class InputManager : MonoBehaviour
	{
		public Vector2 MoveInput { get; private set; }
		public float CameraRotateInput { get; private set; }
		// TASK-WM-163 — MouseLook 모드 시야 회전용 마우스 델타 (픽셀/프레임).
		// 캡슐화 경계(InputManager) 내부에서 Mouse.current 직접 read — UpdateMoveInput 패턴과 동일.
		public Vector2 LookDelta { get; private set; }
		// TASK-WM-193 — 자유 위치 카메라 전용 축 (플레이어 Move/Jump 와 분리).
		public Vector2 CameraMoveInput { get; private set; }
		public float CameraVerticalInput { get; private set; }
		public float ScrollWheelDelta { get; private set; }
		// 자유 카메라 가속 (Ctrl) — 캐릭터 sprint 와 동일 키 직관.
		public bool IsCameraBoost { get; private set; }
		// TASK-WM-203 — 줌 보조키 (Ctrl). 휠은 시점 조작과 겹치므로 보조키를 요구한다.
		// 게임 컴포넌트가 Keyboard.current 를 직접 읽지 않도록 여기(캡슐화 경계)서만 만진다.
		public bool IsZoomModifierHeld => Keyboard.current != null && Keyboard.current.ctrlKey.isPressed;

		private void UpdateMoveInput()
		{
			if (CurrentInputStrategy != null &&
				CurrentInputStrategy.TryGetAxisReturnConditions(InputAxisType.Move, out GameConditionType[] conditions) &&
				GameConditionBridge.IsGameConditionAny(conditions))
			{
				MoveInput = Vector2.zero;
				return;
			}

			Keyboard kb = Keyboard.current;
			float h = 0f;
			float v = 0f;
			if (kb != null)
			{
				if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
				if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1f;
				if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v += 1f;
				if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v -= 1f;
			}

			if (h == 0)
				h = JoystickBridge.GetX();
			if (v == 0)
				v = JoystickBridge.GetY();

			MoveInput = new Vector2(h, v).normalized;
		}

		private void UpdateCameraRotateInput()
		{
			if (CurrentInputStrategy != null &&
				CurrentInputStrategy.TryGetAxisReturnConditions(InputAxisType.CameraRotate, out GameConditionType[] conditions) &&
				GameConditionBridge.IsGameConditionAny(conditions))
			{
				CameraRotateInput = 0f;
				return;
			}

			Keyboard kb = Keyboard.current;
			if (kb == null)
			{
				CameraRotateInput = 0f;
				return;
			}

			float rotate = 0f;
			if (kb.qKey.isPressed) rotate += 1f;
			if (kb.eKey.isPressed) rotate -= 1f;
			CameraRotateInput = rotate;
		}

		private void UpdateLookInput()
		{
			if (CurrentInputStrategy != null &&
				CurrentInputStrategy.TryGetAxisReturnConditions(InputAxisType.Look, out GameConditionType[] conditions) &&
				GameConditionBridge.IsGameConditionAny(conditions))
			{
				LookDelta = Vector2.zero;
				return;
			}

			// TASK-WM-200 — 손가락에선 「화면 어디를 끌면 시점인가」를 화면 조작 UI 가 정한다.
			// 여기서 raw 끌기를 그대로 쓰면 조이스틱을 움직일 때마다 시점이 함께 돌아간다.
			LookDelta = IsTouchMode ? LookBridge.GetDelta() : pointer.LookDelta;
		}

		// TASK-WM-193 — 자유 위치 카메라 평면 이동 (WASD). 플레이어 Move 와 같은 물리 키지만 별도 축 —
		// 자유 카메라 모드에서 Move 는 차단(플레이어 정지)되고 이 축만 컨트롤러가 소비.
		private void UpdateCameraMoveInput()
		{
			if (CurrentInputStrategy != null &&
				CurrentInputStrategy.TryGetAxisReturnConditions(InputAxisType.CameraMove, out GameConditionType[] conditions) &&
				GameConditionBridge.IsGameConditionAny(conditions))
			{
				CameraMoveInput = Vector2.zero;
				return;
			}

			Keyboard kb = Keyboard.current;
			float h = 0f;
			float v = 0f;
			if (kb != null)
			{
				if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
				if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1f;
				if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v += 1f;
				if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v -= 1f;
			}

			CameraMoveInput = new Vector2(h, v).normalized;
		}

		// TASK-WM-193 — 자유비행 상하 이동 (Space=상승 / Shift=하강).
		private void UpdateCameraVerticalInput()
		{
			if (CurrentInputStrategy != null &&
				CurrentInputStrategy.TryGetAxisReturnConditions(InputAxisType.CameraVertical, out GameConditionType[] conditions) &&
				GameConditionBridge.IsGameConditionAny(conditions))
			{
				CameraVerticalInput = 0f;
				return;
			}

			Keyboard kb = Keyboard.current;
			float v = 0f;
			if (kb != null)
			{
				if (kb.spaceKey.isPressed) v += 1f;
				if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) v -= 1f;
			}

			CameraVerticalInput = v;
		}

		// TASK-WM-193 — 부감 높이 줌 (스크롤 휠 델타). 자유 카메라 컨트롤러가 소비.
		private void UpdateScrollWheelInput()
		{
			if (CurrentInputStrategy != null &&
				CurrentInputStrategy.TryGetAxisReturnConditions(InputAxisType.ScrollWheel, out GameConditionType[] conditions) &&
				GameConditionBridge.IsGameConditionAny(conditions))
			{
				ScrollWheelDelta = 0f;
				return;
			}

			// 손가락에선 오므리기가 곧 휠이다 — 부르는 쪽(부감 줌)은 어느 쪽인지 알 필요가 없다.
			ScrollWheelDelta = pointer.ZoomDelta;
		}

		// TASK-WM-193 — 자유 카메라 가속 (Ctrl). 캐릭터 sprint(ctrl) 와 동일 키라 직관적.
		private void UpdateCameraBoost()
		{
			Keyboard kb = Keyboard.current;
			IsCameraBoost = kb != null && kb.ctrlKey.isPressed;
		}
	}
}
