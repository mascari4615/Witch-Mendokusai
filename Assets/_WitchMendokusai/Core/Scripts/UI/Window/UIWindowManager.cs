using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	public class UIWindowManager : Singleton<UIWindowManager>
	{
		// Z-order list: last = topmost
		private readonly List<UIWindow> windows = new();

		protected override void Awake()
		{
			base.Awake();
			InputManager.Instance.RegisterInputEvent(InputEventType.Cancel, InputEventResponseType.Performed, OnCancel);
		}

		protected override void OnDestroy()
		{
			if (InputManager.TryGetExistingInstance(out InputManager inputManager))
				inputManager.UnregisterInputEvent(InputEventType.Cancel, InputEventResponseType.Performed, OnCancel);
			base.OnDestroy();
		}

		public void Register(UIWindow window)
		{
			if (windows.Contains(window) == false)
				windows.Add(window);
		}

		public void Unregister(UIWindow window) => windows.Remove(window);

		public void SetTopmost(UIWindow window)
		{
			if (windows.Contains(window) == false)
				return;

			windows.Remove(window);
			windows.Add(window);
		}

		public UIWindow Find(string windowId) => windows.FirstOrDefault(window => window.WindowId == windowId);

		public void Open(string windowId) => Find(windowId)?.Open();
		public void Close(string windowId) => Find(windowId)?.Close();
		public void Toggle(string windowId) => Find(windowId)?.Toggle();

		public UIWindow GetTopmostOpen()
		{
			for (int i = windows.Count - 1; i >= 0; i--)
				if (windows[i].IsOpen)
					return windows[i];
			return null;
		}

		private void OnCancel()
		{
			UIWindow topmost = GetTopmostOpen();
			if (topmost != null)
				topmost.Close();
		}
	}
}
