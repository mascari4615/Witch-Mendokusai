using UnityEngine;
using UnityEngine.EventSystems;

namespace WitchMendokusai
{
	public class UIWindowDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
	{
		private UIWindow window;

		public void SetWindow(UIWindow window) => this.window = window;

		public void OnBeginDrag(PointerEventData eventData) => window.OnDragBegin(eventData);
		public void OnDrag(PointerEventData eventData) => window.OnDragMove(eventData);
		public void OnEndDrag(PointerEventData eventData) => window.OnDragEnd(eventData);
	}
}
