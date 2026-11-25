using System;
using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
	// 낚시하는 동안 열리는 빈 화면. 입력만 받음
	// 열린 동안 이동, 상호작용, 전체 메뉴는 화면 규칙이 막음. X 와 Esc 는 이 화면을 닫음 (낚시 그만)
	public sealed class UIFishingPanel : UIToolkitPanel
	{
		[Inject] internal InputManager inputManager;

		private Action onPress = delegate { };
		private Action onRelease = delegate { };
		private Action onClosed = delegate { };
		private bool isListening;

		public override string Name => "낚시";

		public void Bind(Action press, Action release, Action closed)
		{
			onPress = press;
			onRelease = release;
			onClosed = closed;
		}

		protected override void BuildUI(VisualElement content)
		{
			Root.RegisterCallback<PointerDownEvent>(_ => onPress());
			Root.RegisterCallback<PointerUpEvent>(_ => onRelease());
			Root.RegisterCallback<PointerLeaveEvent>(_ => onRelease());
		}

		protected override void OnOpen() => Listen(true);

		protected override void OnClose()
		{
			Listen(false);
			onClosed();
		}

		private void OnDestroy()
		{
			if (inputManager != null)
				Listen(false);
		}

		private void Listen(bool listen)
		{
			if (isListening == listen)
				return;
			isListening = listen;
			if (listen)
			{
				inputManager.RegisterInputEvent(InputEventType.Submit, InputEventResponseType.Started, Press);
				inputManager.RegisterInputEvent(InputEventType.Submit, InputEventResponseType.Canceled, Release);
			}
			else
			{
				inputManager.UnregisterInputEvent(InputEventType.Submit, InputEventResponseType.Started, Press);
				inputManager.UnregisterInputEvent(InputEventType.Submit, InputEventResponseType.Canceled, Release);
			}
		}

		private void Press() => onPress();

		private void Release() => onRelease();

		public override void UpdateUI()
		{
		}
	}
}
