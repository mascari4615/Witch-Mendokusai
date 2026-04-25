using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WitchMendokusai
{
	[RequireComponent(typeof(RectTransform))]
	public class UIWindow : MonoBehaviour, IPointerDownHandler
	{
		[SerializeField] private string windowId;
		[SerializeField] private RectTransform header;
		[SerializeField] private Button closeButton;
		[SerializeField] private bool closeOnStart = false;

		public string WindowId => windowId;
		public bool IsOpen => gameObject.activeSelf;

		public event Action OnOpened = delegate { };
		public event Action OnClosed = delegate { };

		private RectTransform rectTransform;
		private Canvas rootCanvas;

		private void Awake()
		{
			rectTransform = (RectTransform)transform;

			Canvas canvas = GetComponentInParent<Canvas>();
			if (canvas == null)
				throw new InvalidOperationException($"[UIWindow] '{name}' must be inside a Canvas hierarchy");
			rootCanvas = canvas.rootCanvas;

			SetupDragHandle();

			if (closeButton != null)
				closeButton.onClick.AddListener(Close);

			UIWindowManager.Instance.Register(this);

			// 시작 시 활성 상태인 prefab도 위치 복원
			RestorePosition();
		}

		private void Start()
		{
			if (closeOnStart)
				gameObject.SetActive(false);
		}

		private void OnDestroy()
		{
			if (UIWindowManager.TryGetExistingInstance(out UIWindowManager manager))
				manager.Unregister(this);
		}

		private void SetupDragHandle()
		{
			RectTransform handleTarget = header != null ? header : rectTransform;
			UIWindowDragHandle handle = handleTarget.GetComponent<UIWindowDragHandle>();
			if (handle == null)
				handle = handleTarget.gameObject.AddComponent<UIWindowDragHandle>();
			handle.SetWindow(this);
		}

		public void Open()
		{
			gameObject.SetActive(true);
			RestorePosition();
			BringToFront();
			OnOpened.Invoke();
		}

		public void Close()
		{
			SavePosition();
			gameObject.SetActive(false);
			OnClosed.Invoke();
		}

		public void Toggle()
		{
			if (IsOpen)
				Close();
			else
				Open();
		}

		public void OnPointerDown(PointerEventData eventData) => BringToFront();

		public void OnDragBegin(PointerEventData eventData) => BringToFront();

		public void OnDragMove(PointerEventData eventData)
		{
			rectTransform.anchoredPosition += eventData.delta / rootCanvas.scaleFactor;
		}

		public void OnDragEnd(PointerEventData eventData) => SavePosition();

		private void BringToFront()
		{
			transform.SetAsLastSibling();
			UIWindowManager.Instance.SetTopmost(this);
		}

		private void RestorePosition()
		{
			Vector2? saved = SOManager.Instance.WindowLayoutData.Get(windowId);
			if (saved.HasValue)
				rectTransform.anchoredPosition = saved.Value;
		}

		private void SavePosition()
		{
			SOManager.Instance.WindowLayoutData.Set(windowId, rectTransform.anchoredPosition);
		}
	}
}
