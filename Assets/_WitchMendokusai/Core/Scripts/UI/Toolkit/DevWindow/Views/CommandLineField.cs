using System;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 개발자 윈도우 명령 입력. 1단계: Enter 로 submit. Tab/↑↓ 자동완성·히스토리는 2단계에서 추가.
	/// TextField 내부 처리가 첫 Enter 를 소비하지 않도록 TrickleDown(capture phase)에서 가로챈다.
	/// </summary>
	public class CommandLineField : VisualElement
	{
		public const string USS_CLASS = "wm-dev-commandline";
		public const string USS_PROMPT = "wm-dev-commandline__prompt";
		public const string USS_INPUT = "wm-dev-commandline__input";

		public event Action<string> OnSubmit = delegate { };
		public event Action OnFocusIn = delegate { };
		public event Action OnFocusOut = delegate { };

		private readonly Label promptLabel;
		private readonly TextField inputField;

		public CommandLineField()
		{
			AddToClassList(USS_CLASS);

			promptLabel = new Label("> ");
			promptLabel.AddToClassList(USS_PROMPT);
			promptLabel.pickingMode = PickingMode.Ignore;
			Add(promptLabel);

			inputField = new TextField();
			inputField.AddToClassList(USS_INPUT);
			inputField.style.flexGrow = 1;

			// TextField 의 라벨 슬롯은 안 쓰는데 공간 잡아먹음 — 숨김.
			if (inputField.labelElement != null)
				inputField.labelElement.style.display = DisplayStyle.None;

			// TrickleDown — TextField 내부가 Enter 를 소비하기 전에 가로챈다.
			inputField.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
			inputField.RegisterCallback<FocusInEvent>(OnFocusInInternal);
			inputField.RegisterCallback<FocusOutEvent>(OnFocusOutInternal);

			Add(inputField);
		}

		public void FocusInput() => inputField.Focus();

		public void ClearInput() => inputField.SetValueWithoutNotify(string.Empty);

		public string CurrentText => inputField.value;

		public bool HasInputFocus => inputField.focusController?.focusedElement == inputField
			|| (inputField.focusController?.focusedElement is VisualElement visual && IsDescendant(visual));

		private bool IsDescendant(VisualElement element)
		{
			VisualElement cursor = element;
			while (cursor != null)
			{
				if (cursor == inputField)
					return true;
				cursor = cursor.parent;
			}
			return false;
		}

		private void OnKeyDown(KeyDownEvent evt)
		{
			if (evt.keyCode == UnityEngine.KeyCode.Return || evt.keyCode == UnityEngine.KeyCode.KeypadEnter)
			{
				string text = inputField.value;
				ClearInput();
				evt.StopPropagation();
				evt.PreventDefault();

				if (string.IsNullOrWhiteSpace(text))
					return;

				OnSubmit.Invoke(text);
			}
		}

		private void OnFocusInInternal(FocusInEvent evt) => OnFocusIn.Invoke();
		private void OnFocusOutInternal(FocusOutEvent evt) => OnFocusOut.Invoke();
	}
}
