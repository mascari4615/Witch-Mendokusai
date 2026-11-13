using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// MobileControlsView 의 Stick 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 MobileControlsView.cs 를 본다.
	public partial class MobileControlsView
	{
		[Header("_" + nameof(MobileControlsView))]
		[Tooltip("스틱 판의 지름(픽셀).")]
		[SerializeField, Min(40f)] private float stickSize = 220f;
		[Tooltip("스틱 손잡이의 지름(픽셀).")]
		[SerializeField, Min(20f)] private float knobSize = 90f;
		[Tooltip("이 비율 안쪽은 안 민 것으로 본다 — 손가락이 살짝 흔들려도 캐릭터가 안 움직이게.")]
		[SerializeField, Range(0f, 0.5f)] private float stickDeadZone = 0.15f;
		[Tooltip("화면 왼쪽에서 이 비율 안을 누르면 그 자리에 스틱이 생긴다. 엄지가 스틱 칸을 정확히 찾지 않아도 걷게")]
		[SerializeField, Range(0f, 1f)] private float floatingStickZone = 0.5f;
		private VisualElement stickBase;
		private VisualElement stickKnob;

		private int stickPointerId = -1;
		// 스틱 중심의 패널 좌표. 누른 자리로 옮긴 뒤의 값
		private Vector2 stickPanelCenter;
		// 사용자가 자리 옮기기로 정한 쉬는 자리. 떼면 여기로 돌아감
		private Vector2 stickRestOffset;
		private Vector2 stickValue;

		private void BuildStick(VisualElement parent)
		{
			stickBase = new VisualElement { name = "MobileStick" };
			stickBase.style.position = Position.Absolute;
			stickBase.style.left = edgeMargin;
			stickBase.style.bottom = edgeMargin + bottomSafeOffset;
			stickBase.style.width = stickSize;
			stickBase.style.height = stickSize;
			stickBase.style.borderTopLeftRadius = stickSize;
			stickBase.style.borderTopRightRadius = stickSize;
			stickBase.style.borderBottomLeftRadius = stickSize;
			stickBase.style.borderBottomRightRadius = stickSize;
			stickBase.style.backgroundColor = new Color(0.08f, 0.09f, 0.13f, 0.35f);
			SetBorder(stickBase, new Color(0.75f, 0.8f, 0.9f, 0.35f), 2f);
			stickBase.pickingMode = PickingMode.Position;

			stickKnob = new VisualElement { name = "MobileStickKnob" };
			stickKnob.style.position = Position.Absolute;
			stickKnob.style.width = knobSize;
			stickKnob.style.height = knobSize;
			stickKnob.style.borderTopLeftRadius = knobSize;
			stickKnob.style.borderTopRightRadius = knobSize;
			stickKnob.style.borderBottomLeftRadius = knobSize;
			stickKnob.style.borderBottomRightRadius = knobSize;
			stickKnob.style.backgroundColor = new Color(0.85f, 0.88f, 0.95f, 0.55f);
			stickKnob.pickingMode = PickingMode.Ignore;
			stickBase.Add(stickKnob);
			CenterKnob();

			stickBase.RegisterCallback<PointerDownEvent>(OnStickDown);
			stickBase.RegisterCallback<PointerMoveEvent>(OnStickMove);
			stickBase.RegisterCallback<PointerUpEvent>(OnStickUp);
			// 동작 버튼과 같은 이유 — 손가락을 잡고 있던 권한을 잃으면 「뗐다」가 영영 안 온다.
			// 그러면 스틱이 기울어진 채로 굳어서 캐릭터가 그 방향으로 계속 걷는다.
			stickBase.RegisterCallback<PointerCaptureOutEvent>(_ => ReleaseStick());

			parent.Add(stickBase);
			RegisterMovable(stickBase);
		}

		private void CenterKnob()
		{
			stickKnob.style.left = (stickSize - knobSize) * 0.5f;
			stickKnob.style.top = (stickSize - knobSize) * 0.5f;
		}

		private void OnStickDown(PointerDownEvent evt)
		{
			// 자리를 옮기는 중엔 스틱이 「밀리면」 안 된다 — 옮기려는 손가락이 캐릭터를 걷게 한다.
			if (layoutEditMode)
				return;
			if (TryBeginStick(evt.pointerId, evt.position))
				evt.StopPropagation();
		}

		private bool IsInFloatingStickZone(Vector2 panelPosition) =>
			lookBackdrop != null && panelPosition.x <= lookBackdrop.worldBound.xMin + lookBackdrop.worldBound.width * floatingStickZone;

		/// <summary>
		/// 누른 자리를 스틱 중심으로 삼아 잡기. 스틱 칸과 왼쪽 빈 화면 공통 경로.
		/// 처음 누른 순간은 기울기 0. 엄지를 대자마자 엉뚱한 쪽으로 튀는 것 방지
		/// </summary>
		private bool TryBeginStick(int pointerId, Vector2 panelPosition)
		{
			// 스틱은 손가락 하나만 받는다. 둘째 손가락은 다른 조작의 것
			if (layoutEditMode || stickPointerId >= 0 || stickBase == null)
				return false;

			stickPointerId = pointerId;
			stickRestOffset = GetOffset(stickBase);
			Vector2 restCenter = stickBase.worldBound.center;
			ApplyOffset(stickBase, stickRestOffset + (panelPosition - restCenter));
			stickPanelCenter = panelPosition;
			stickBase.CapturePointer(pointerId);
			ApplyStick(panelPosition);
			return true;
		}

		private void OnStickMove(PointerMoveEvent evt)
		{
			if (evt.pointerId != stickPointerId)
				return;
			ApplyStick(evt.position);
		}

		private void OnStickUp(PointerUpEvent evt)
		{
			if (evt.pointerId != stickPointerId)
				return;
			ReleaseStick();
		}

		private void ApplyStick(Vector2 panelPosition)
		{
			float radius = (stickSize - knobSize) * 0.5f;
			Vector2 offset = Vector2.ClampMagnitude(panelPosition - stickPanelCenter, radius);

			stickKnob.style.left = (stickSize - knobSize) * 0.5f + offset.x;
			stickKnob.style.top = (stickSize - knobSize) * 0.5f + offset.y;

			Vector2 raw = radius <= 0f ? Vector2.zero : offset / radius;
			// 화면은 아래로 갈수록 y 가 커지고, 걷는 방향은 위가 앞이다.
			raw = new Vector2(raw.x, -raw.y);

			// 죽은 구역 — 살짝 스친 것을 「걸어라」로 읽으면 캐릭터가 계속 실룩거린다.
			stickValue = raw.magnitude <= stickDeadZone ? Vector2.zero : raw;
		}

		/// <summary> 누른 채로 적어 둔 버튼 하나를 놓는다. 두 번 놓아도 안전(멱등). </summary>
		private void ReleaseHeld(InputEventType inputEventType, VisualElement button)
		{
			if (button != null)
				button.style.backgroundColor = new Color(0.1f, 0.12f, 0.17f, 0.55f);
			if (heldButtons.Remove(inputEventType) == false)
				return;
			if (InputManager.TryGetExistingInstance(out InputManager inputManager))
				inputManager.ReleaseFromScreenButton(inputEventType);
		}

		/// <summary> 조작 장치가 사라질 때 누른 채로 남은 것을 전부 놓는다. </summary>
		private void ReleaseAllHeld()
		{
			if (heldButtons.Count == 0)
				return;

			if (InputManager.TryGetExistingInstance(out InputManager inputManager))
			{
				for (int i = 0; i < heldButtons.Count; i++)
					inputManager.ReleaseFromScreenButton(heldButtons[i]);
			}
			heldButtons.Clear();
		}

		private void ReleaseStick()
		{
			if (stickBase != null && stickPointerId >= 0)
			{
				if (stickBase.HasPointerCapture(stickPointerId))
					stickBase.ReleasePointer(stickPointerId);
				ApplyOffset(stickBase, stickRestOffset);
			}
			stickPointerId = -1;
			stickValue = Vector2.zero;
			if (stickKnob != null)
				CenterKnob();
			PushStickValue();
		}

		private void PushStickValue()
		{
			if (SOManagerBridge.HasInstance == false)
				return;
			SOManagerBridge.JoystickX.RuntimeValue = stickValue.x;
			SOManagerBridge.JoystickY.RuntimeValue = stickValue.y;
		}

		/// <summary> 지금 누른 채로 있는 동작 버튼들 — 뗄 기회를 잃었을 때 대신 놓아 주기 위한 목록. </summary>
		private readonly System.Collections.Generic.List<InputEventType> heldButtons = new();
	}
}
