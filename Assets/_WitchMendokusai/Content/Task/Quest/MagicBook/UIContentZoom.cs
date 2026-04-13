using UnityEngine;
using UnityEngine.EventSystems;

namespace WitchMendokusai
{
	public class UIContentZoom : MonoBehaviour, IScrollHandler
	{
		[SerializeField] private RectTransform target;
		[SerializeField] private float zoomSpeed = 0.1f;
		[SerializeField] private float smoothSpeed = 10f;
		[SerializeField] private float minZoom = 0.3f;
		[SerializeField] private float maxZoom = 2f;

		private float targetZoom;

		private void Awake()
		{
			targetZoom = target != null ? target.localScale.x : 1f;
		}

		private void Update()
		{
			if (target == null)
				return;

			float current = target.localScale.x;
			if (Mathf.Abs(current - targetZoom) > 0.001f)
				target.localScale = Vector3.one * Mathf.Lerp(current, targetZoom, Time.unscaledDeltaTime * smoothSpeed);
		}

		public void OnScroll(PointerEventData eventData)
		{
			targetZoom = Mathf.Clamp(targetZoom + eventData.scrollDelta.y * zoomSpeed * 0.1f, minZoom, maxZoom);
		}
	}
}
