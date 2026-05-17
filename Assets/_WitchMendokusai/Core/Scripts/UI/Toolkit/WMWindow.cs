using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	public enum WindowSize
	{
		Small,
		Large
	}

	[UxmlElement]
	public partial class WMWindow : VisualElement
	{
		public const string USS_CLASS = "wm-window";
		public const string USS_HEADER = "wm-window__header";
		public const string USS_TITLE = "wm-window__title";
		public const string USS_CLOSE = "wm-window__close";
		public const string USS_CONTENT = "wm-window__content";
		public const string USS_SIZE_TOGGLE = "wm-window__size-toggle";
		public const string USS_SIZE_SMALL = "wm-window--small";
		public const string USS_SIZE_LARGE = "wm-window--large";

		[UxmlAttribute] public string WindowId { get; set; }
		[UxmlAttribute] public string Title { get; set; }

		public bool IsOpen => style.display != DisplayStyle.None;
		public VisualElement Content { get; private set; }
		public WindowSize CurrentSize { get; private set; } = WindowSize.Small;
		public bool IsSizeToggleEnabled { get; private set; }

		public event Action OnOpened = delegate { };
		public event Action OnClosed = delegate { };
		public event Action<WindowSize> OnSizeChanged = delegate { };

		private readonly VisualElement header;
		private readonly Label titleLabel;
		private readonly Button closeButton;
		private Button sizeToggleButton;
		private WindowManager windowManager;

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
			// TASK-WM-133 — panel-root owner-push 된 WindowManager 를 attach 시
			// 1회 해결·캐싱(Core IUIWindowServices facet). detach 시 조상 walk
			// 불가 타이밍 안전. static Instance reach 제거.
			windowManager = this.GetUIWindowServices()?.WindowManager;
			windowManager?.Register(this);
			RestorePosition();
			RestoreSize();
		}

		private void OnDetached(DetachFromPanelEvent evt)
		{
			windowManager?.Unregister(this);
			windowManager = null;
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

			Vector2? saved = WindowLayoutBridge.GetPosition(WindowId);
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
			WindowLayoutBridge.SetPosition(WindowId, position);
		}

		public void EnableSizeToggle()
		{
			if (IsSizeToggleEnabled)
				return;

			IsSizeToggleEnabled = true;

			sizeToggleButton = new Button(ToggleSize);
			sizeToggleButton.AddToClassList(USS_SIZE_TOGGLE);

			// closeButton 직전에 삽입 (header 우측, X 왼쪽)
			int closeIndex = header.IndexOf(closeButton);
			header.Insert(closeIndex, sizeToggleButton);

			ApplySizeClass();
		}

		public void SetSize(WindowSize size)
		{
			if (CurrentSize == size && IsSizeToggleEnabled)
				return;

			CurrentSize = size;
			ApplySizeClass();
			SaveSize();
			OnSizeChanged.Invoke(size);
		}

		private void ToggleSize()
		{
			SetSize(CurrentSize == WindowSize.Small ? WindowSize.Large : WindowSize.Small);
		}

		private void ApplySizeClass()
		{
			EnableInClassList(USS_SIZE_SMALL, CurrentSize == WindowSize.Small);
			EnableInClassList(USS_SIZE_LARGE, CurrentSize == WindowSize.Large);

			if (sizeToggleButton != null)
			{
				// Small 일 때 → 클릭하면 large (▣). Large 일 때 → small 로 (▢).
				sizeToggleButton.text = CurrentSize == WindowSize.Small ? "▣" : "▢";
			}
		}

		private void SaveSize()
		{
			if (string.IsNullOrEmpty(WindowId) || IsSizeToggleEnabled == false)
				return;

			WindowLayoutBridge.SetExpanded(WindowId, CurrentSize == WindowSize.Large);
		}

		private void RestoreSize()
		{
			if (string.IsNullOrEmpty(WindowId) || IsSizeToggleEnabled == false)
				return;

			bool? saved = WindowLayoutBridge.GetExpanded(WindowId);
			if (saved.HasValue == false)
				return;

			CurrentSize = saved.Value ? WindowSize.Large : WindowSize.Small;
			ApplySizeClass();
		}
	}
}
