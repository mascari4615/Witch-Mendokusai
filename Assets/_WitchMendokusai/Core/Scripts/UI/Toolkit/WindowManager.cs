using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// UI Toolkit 윈도우(WMWindow) 등록 관리. ESC = 최상단 윈도우 닫기.
	/// 기존 uGUI UIWindowManager의 UI Toolkit 버전.
	/// </summary>
	public class WindowManager : MonoBehaviour
	{
		public static WindowManager Instance { get; private set; }

		public static bool TryGetExistingInstance(out WindowManager mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		private readonly List<WMWindow> windows = new();

		private void Awake()
		{
			if (Instance != null && Instance != this) { Destroy(gameObject); return; }
			Instance = this;
			InputManager.Instance.RegisterInputEvent(InputEventType.Cancel, InputEventResponseType.Performed, OnCancel);
		}

		private void OnDestroy()
		{
			if (InputManager.TryGetExistingInstance(out InputManager inputManager))
				inputManager.UnregisterInputEvent(InputEventType.Cancel, InputEventResponseType.Performed, OnCancel);

			if (Instance == this)
				Instance = null;
		}

		public void Register(WMWindow window)
		{
			if (windows.Contains(window) == false)
				windows.Add(window);
		}

		public void Unregister(WMWindow window) => windows.Remove(window);

		public WMWindow Find(string windowId) => windows.FirstOrDefault(window => window.WindowId == windowId);

		public void Open(string windowId) => Find(windowId)?.Open();
		public void Close(string windowId) => Find(windowId)?.Close();
		public void Toggle(string windowId) => Find(windowId)?.Toggle();

		public WMWindow GetTopmostOpen()
		{
			for (int i = windows.Count - 1; i >= 0; i--)
				if (windows[i].IsOpen)
					return windows[i];
			return null;
		}

		private void OnCancel()
		{
			WMWindow topmost = GetTopmostOpen();
			topmost?.Close();
		}
	}
}
