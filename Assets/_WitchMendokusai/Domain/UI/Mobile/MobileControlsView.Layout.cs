using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// MobileControlsView 의 Layout 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 MobileControlsView.cs 를 본다.
	public partial class MobileControlsView
	{
		/// <summary>
		/// 화면 아래쪽에 이미 있는 것들(체력·이름표·핫바·날짜)을 피해 띄우는 높이 (실측으로 정한 값).
		/// ★ 겹치면 둘 다 못 쓴다 — 조작 장치를 누르려다 체력바를 누르고, 체력이 얼마인지도 안 보인다.
		/// </summary>
		[Tooltip("아래쪽 기존 화면 요소(체력·날짜 등)를 피해 띄우는 높이(픽셀).")]
		[SerializeField, Min(0f)] private float bottomSafeOffset = 140f;

		[Header("자리 옮기기")]
		[Tooltip("옮긴 조작 장치가 화면 안에 최소한 남겨야 하는 크기(픽셀). 다시 잡을 수 있게.")]
		[SerializeField, Min(8f)] private float layoutMinVisible = 44f;

		// ───────── 자리 옮기기 (TASK-WM-200) ─────────
		//
		// ★ 엄지가 닿는 자리는 사람마다·기기마다 다르다. 기본 자리는 내 손 기준의 추측일 뿐이라,
		//   「여기 말고 저기」를 사용자가 직접 정할 수 있어야 폰에서 오래 붙잡고 놀 수 있다.
		// ★ 왜 따로 「배치 모드」를 두나: 평소에 끌어서 움직이면 *조작과 구별이 안 된다* —
		//   스틱을 밀려던 손가락이 스틱을 옮겨버린다. 옮기는 동안엔 조작을 멈춘다.
		// ★ 되돌리기를 반드시 같이 둔다. 옮기다 망가뜨렸을 때 되돌릴 길이 없으면 사용자가
		//   기능 자체를 안 쓴다(그리고 화면 밖으로 나간 버튼은 손으로 못 되살린다).

		private readonly System.Collections.Generic.List<VisualElement> movable = new();
		private bool layoutEditMode;
		private VisualElement editBar;
		private Vector2 dragOriginOffset;

		private void RegisterMovable(VisualElement element)
		{
			movable.Add(element);
			element.RegisterCallback<PointerDownEvent>(OnMovePointerDown);
			element.RegisterCallback<PointerMoveEvent>(OnMovePointerMove);
			element.RegisterCallback<PointerUpEvent>(OnMovePointerUp);
		}

		private void RestoreMovedPositions()
		{
			foreach (VisualElement element in movable)
				ApplyOffset(element, MobileLayoutStore.Load(element.name) * ScreenSize);
		}

		private static Vector2 GetOffset(VisualElement element)
		{
			return new Vector2(element.style.translate.value.x.value, element.style.translate.value.y.value);
		}

		private static void ApplyOffset(VisualElement element, Vector2 offset)
		{
			element.style.translate = new Translate(offset.x, offset.y);
		}

		private void OnMovePointerDown(PointerDownEvent evt)
		{
			if (layoutEditMode == false || dragPointerId >= 0)
				return;
			dragTarget = evt.currentTarget as VisualElement;
			if (dragTarget == null)
				return;
			dragPointerId = evt.pointerId;
			dragStart = evt.position;
			dragOriginOffset = GetOffset(dragTarget);
			dragTarget.CapturePointer(evt.pointerId);
			evt.StopPropagation();
		}

		private void OnMovePointerMove(PointerMoveEvent evt)
		{
			if (dragTarget == null || evt.pointerId != dragPointerId)
				return;
			Vector2 desired = dragOriginOffset + ((Vector2)evt.position - dragStart);
			ApplyOffset(dragTarget, MobileLayoutStore.ClampToScreen(
				desired, dragTarget.worldBound, ScreenSize, layoutMinVisible));
			evt.StopPropagation();
		}

		private void OnMovePointerUp(PointerUpEvent evt)
		{
			if (dragTarget == null || evt.pointerId != dragPointerId)
				return;
			dragTarget.ReleasePointer(evt.pointerId);
			Vector2 screen = ScreenSize;
			// 화면 크기로 나눠 둔다 — 다음에 다른 화면에서 켜도 같은 「대충 그 자리」가 된다.
			MobileLayoutStore.Save(dragTarget.name, new Vector2(
				screen.x > 0 ? GetOffset(dragTarget).x / screen.x : 0f,
				screen.y > 0 ? GetOffset(dragTarget).y / screen.y : 0f));
			dragTarget = null;
			dragPointerId = -1;
			evt.StopPropagation();
		}

		private void SetLayoutEditMode(bool on)
		{
			layoutEditMode = on;
			// 옮기는 동안 화면을 문지르면 시점이 같이 돌아 무엇을 하는지 알 수 없게 된다.
			lookBackdrop.pickingMode = on ? PickingMode.Ignore : PickingMode.Position;
			foreach (VisualElement element in movable)
				SetBorder(element, on ? new Color(1f, 0.85f, 0.3f, 0.9f) : new Color(0.75f, 0.8f, 0.9f, 0.35f), 2f);
			if (editBar != null)
				editBar.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
			if (on == false)
				ReleaseStick();
		}

		private void ResetLayout()
		{
			foreach (VisualElement element in movable)
			{
				MobileLayoutStore.Clear(element.name);
				ApplyOffset(element, Vector2.zero);
			}
		}

		/// <summary>자리 옮기기로 들어가는 문 — 창 목록 맨 아래(자주 쓰는 것 아래)에 둔다.</summary>
		private Label MakeLayoutEditButton()
		{
			Label button = new Label("자리 옮기기") { name = "MobileLayoutEditEnter" };
			StyleRoundButton(button, actionButtonSize * 0.7f);
			button.style.width = actionButtonSize * 1.15f;
			button.RegisterCallback<PointerDownEvent>(evt =>
			{
				windowMenuColumn.style.display = DisplayStyle.None;
				SetLayoutEditMode(true);
				evt.StopPropagation();
			});
			return button;
		}

		/// <summary>
		/// 옮기는 동안만 뜨는 줄 — 「완료」와 「원래대로」.
		/// ★ 되돌릴 길을 같은 화면에 두는 게 이 기능의 안전장치다.
		/// </summary>
		private void BuildEditBar(VisualElement parent)
		{
			editBar = new VisualElement { name = "MobileLayoutEditBar" };
			editBar.style.position = Position.Absolute;
			editBar.style.left = 0;
			editBar.style.right = 0;
			editBar.style.top = edgeMargin;
			editBar.style.flexDirection = FlexDirection.Row;
			editBar.style.justifyContent = Justify.Center;
			editBar.style.display = DisplayStyle.None;

			Label done = new Label("완료");
			StyleRoundButton(done, actionButtonSize * 0.7f);
			done.style.width = actionButtonSize * 1.15f;
			done.RegisterCallback<PointerDownEvent>(evt => { SetLayoutEditMode(false); evt.StopPropagation(); });

			Label reset = new Label("원래대로");
			StyleRoundButton(reset, actionButtonSize * 0.7f);
			reset.style.width = actionButtonSize * 1.3f;
			reset.RegisterCallback<PointerDownEvent>(evt => { ResetLayout(); evt.StopPropagation(); });

			editBar.Add(reset);
			editBar.Add(done);
			parent.Add(editBar);
		}
	}
}
