using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// UI Toolkit 윈도우(WMWindow) 등록 관리. ESC = 최상단 윈도우 닫기.
	/// 기존 uGUI UIWindowManager의 UI Toolkit 버전.
	/// </summary>
	public class WindowManager : MonoBehaviour
	{
		// TASK-WM-133 — static Instance/TryGetExistingInstance 삭제. RegisterLeaf
		// prefab + DontDestroyOnLoad 가 단일성 보장(DI 소유), WMWindow 는 UIRoot
		// panel-root owner-push 된 IUIWindowServices facet 경유 획득.
		private InputManager inputManager;
		private readonly List<WMWindow> windows = new();

		[Inject]
		public void Construct(InputManager inputManager)
		{
			this.inputManager = inputManager;
		}

		private void Awake()
		{
			inputManager.RegisterInputEvent(InputEventType.Cancel, InputEventResponseType.Performed, OnCancel);
		}

		private void OnDestroy()
		{
			if (inputManager != null)
				inputManager.UnregisterInputEvent(InputEventType.Cancel, InputEventResponseType.Performed, OnCancel);
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
