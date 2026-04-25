using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	[UxmlElement]
	public partial class WMWindow : VisualElement
	{
		public const string USS_CLASS = "wm-window";
		public const string USS_HEADER = "wm-window__header";
		public const string USS_TITLE = "wm-window__title";
		public const string USS_CLOSE = "wm-window__close";
		public const string USS_CONTENT = "wm-window__content";

		[UxmlAttribute] public string WindowId { get; set; }
		[UxmlAttribute] public string Title { get; set; }

		public bool IsOpen => style.display != DisplayStyle.None;
		public VisualElement Content { get; private set; }

		public event Action OnOpened = delegate { };
		public event Action OnClosed = delegate { };

		private readonly VisualElement header;
		private readonly Label titleLabel;
		private readonly Button closeButton;

		public WMWindow()
		{
			AddToClassList(USS_CLASS);
			focusable = true;
			pickingMode = PickingMode.Position;

			header = new VisualElement();
			header.AddToClassList(USS_HEADER);
			Add(header);

			titleLabel = new Label();
			titleLabel.AddToClassList(USS_TITLE);
			titleLabel.pickingMode = PickingMode.Ignore;
			header.Add(titleLabel);

			closeButton = new Button(Close)
			{
				text = "X"
			};
			closeButton.AddToClassList(USS_CLOSE);
			header.Add(closeButton);

			Content = new VisualElement();
			Content.AddToClassList(USS_CONTENT);
			Add(Content);

			header.AddManipulator(new WMWindowDragManipulator(this));

			RegisterCallback<PointerDownEvent>(_ => BringToFront());
			RegisterCallback<AttachToPanelEvent>(OnAttached);
			RegisterCallback<DetachFromPanelEvent>(OnDetached);

			SetVisibleInternal(false);
		}

		private void OnAttached(AttachToPanelEvent evt)
		{
			titleLabel.text = Title ?? string.Empty;
			WindowManager.Instance?.Register(this);
			RestorePosition();
		}

		private void OnDetached(DetachFromPanelEvent evt)
		{
			if (WindowManager.TryGetExistingInstance(out WindowManager manager))
				manager.Unregister(this);
		}

		public void Open()
		{
			SetVisibleInternal(true);
			BringToFront();
			OnOpened.Invoke();
		}

		public void Close()
		{
			SavePosition();
			SetVisibleInternal(false);
			OnClosed.Invoke();
		}

		public void Toggle()
		{
			if (IsOpen)
				Close();
			else
				Open();
		}

		public void OnDragEnd() => SavePosition();

		private void SetVisibleInternal(bool visible)
		{
			style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private void RestorePosition()
		{
			if (string.IsNullOrEmpty(WindowId))
				return;

			Vector2? saved = SOManager.Instance.WindowLayoutData.Get(WindowId);
			if (saved.HasValue == false)
				return;

			Vector2 position = saved.Value;

			// 화면 밖 위치 clamp — 헤더가 보이도록 최소 일부는 화면 안에 유지
			float screenWidth = Screen.width;
			float screenHeight = Screen.height;
			position.x = Mathf.Clamp(position.x, 0, Mathf.Max(0, screenWidth - 60));
			position.y = Mathf.Clamp(position.y, 0, Mathf.Max(0, screenHeight - 30));

			style.left = position.x;
			style.top = position.y;
		}

		private void SavePosition()
		{
			if (string.IsNullOrEmpty(WindowId))
				return;

			Vector2 position = new(resolvedStyle.left, resolvedStyle.top);
			SOManager.Instance.WindowLayoutData.Set(WindowId, position);
		}
	}
}
