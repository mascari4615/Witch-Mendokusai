using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 윈도우 헤더에 부착. 마우스 좌클릭 드래그로 부모 WMWindow 이동.
	/// </summary>
	public class WMWindowDragManipulator : MouseManipulator
	{
		private readonly WMWindow window;
		private bool isDragging;
		private Vector2 startMouse;
		private Vector2 startPosition;

		public WMWindowDragManipulator(WMWindow window)
		{
			this.window = window;
			activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
		}

		protected override void RegisterCallbacksOnTarget()
		{
			target.RegisterCallback<MouseDownEvent>(OnMouseDown);
			target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
			target.RegisterCallback<MouseUpEvent>(OnMouseUp);
		}

		protected override void UnregisterCallbacksFromTarget()
		{
			target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
			target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
			target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
		}

		private void OnMouseDown(MouseDownEvent evt)
		{
			if (CanStartManipulation(evt) == false)
				return;

			isDragging = true;
			startMouse = evt.mousePosition;
			startPosition = new Vector2(window.resolvedStyle.left, window.resolvedStyle.top);
			target.CaptureMouse();
			evt.StopPropagation();
		}

		private void OnMouseMove(MouseMoveEvent evt)
		{
			if (isDragging == false)
				return;

			Vector2 delta = (Vector2)evt.mousePosition - startMouse;
			window.style.left = startPosition.x + delta.x;
			window.style.top = startPosition.y + delta.y;
		}

		private void OnMouseUp(MouseUpEvent evt)
		{
			if (isDragging == false || CanStopManipulation(evt) == false)
				return;

			isDragging = false;
			target.ReleaseMouse();
			window.OnDragEnd();
			evt.StopPropagation();
		}
	}
}
