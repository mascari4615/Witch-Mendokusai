using UnityEngine;

namespace WitchMendokusai
{
	[RequireComponent(typeof(UIWindow))]
	public class UIWindowToggle : MonoBehaviour
	{
		[SerializeField] private InputEventType toggleKey;

		private UIWindow window;

		private void Awake()
		{
			window = GetComponent<UIWindow>();
			InputManager.Instance.RegisterInputEvent(toggleKey, InputEventResponseType.Performed, OnToggle);
		}

		private void OnDestroy()
		{
			if (InputManager.TryGetExistingInstance(out InputManager inputManager))
				inputManager.UnregisterInputEvent(toggleKey, InputEventResponseType.Performed, OnToggle);
		}

		private void OnToggle() => window.Toggle();
	}
}
