using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 개발자 윈도우 명령 입력. 2단계 — Tab 자동완성 + ↑↓ 히스토리 + 첫 Enter submit.
	/// dropdown 열린 동안에는 ↑↓·Enter·Tab 이 dropdown 조작으로 위임됨.
	/// 그 외 입력 시 dropdown 닫힘 + 히스토리 cursor 리셋.
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
		private readonly AutocompleteDropdown dropdown;
		private readonly DevHistory history = new();

		/// <summary>dropdown 은 CommandLineField 가 소유하지만 시각 배치는 DevWindowView 가 결정 (Console 과 CommandLine 사이에 둠).</summary>
		public AutocompleteDropdown Dropdown => dropdown;

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

			if (inputField.labelElement != null)
				inputField.labelElement.style.display = DisplayStyle.None;

			// TrickleDown — TextField 내부 / 패널 focus traversal 가 Tab/Enter/↑↓ 를 소비하기 전에 가로챈다.
			RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
			RegisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
			RegisterCallback<NavigationSubmitEvent>(OnNavigationSubmit, TrickleDown.TrickleDown);

			inputField.RegisterCallback<FocusInEvent>(OnFocusInInternal);
			inputField.RegisterCallback<FocusOutEvent>(OnFocusOutInternal);

			Add(inputField);

			// dropdown 은 부모(DevWindowView) 가 다른 위치에 Add 한다 — 여기서 자기 child 로 두지 않음.
			dropdown = new AutocompleteDropdown();
		}

		private void OnNavigationMove(NavigationMoveEvent evt)
		{
			// 이미 OnKeyDown 에서 ↑↓ 처리 — 여기선 포커스 이동 차단만.
			evt.StopPropagation();
		}

		private void OnNavigationSubmit(NavigationSubmitEvent evt)
		{
			// Enter 는 OnKeyDown 에서 처리 — navigation 측은 차단.
			evt.StopPropagation();
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
			KeyCode keyCode = evt.keyCode;

			if (keyCode == KeyCode.Tab)
			{
				HandleTab();
				evt.StopPropagation();
				return;
			}

			if (keyCode == KeyCode.UpArrow)
			{
				if (dropdown.IsOpen)
					dropdown.MoveSelection(-1);
				else
					NavigateHistory(true);
				evt.StopPropagation();
				return;
			}

			if (keyCode == KeyCode.DownArrow)
			{
				if (dropdown.IsOpen)
					dropdown.MoveSelection(1);
				else
					NavigateHistory(false);
				evt.StopPropagation();
				return;
			}

			if (keyCode == KeyCode.Return || keyCode == KeyCode.KeypadEnter)
			{
				if (dropdown.IsOpen)
				{
					ConfirmDropdownSelection();
					evt.StopPropagation();
					return;
				}

				string text = inputField.value;
				ClearInput();
				history.ResetCursor();
				evt.StopPropagation();

				// 다음 입력 바로 받을 수 있도록 re-focus + cursor 0 (Focus() 의 기본 전체선택 해제).
				inputField.schedule.Execute(() =>
				{
					inputField.Focus();
					inputField.cursorIndex = 0;
					inputField.selectIndex = 0;
				}).StartingIn(0);

				if (string.IsNullOrWhiteSpace(text))
					return;

				history.Add(text);
				OnSubmit.Invoke(text);
				return;
			}

			// 그 외 입력 — 실제 typing 일 때만 dropdown 닫고 히스토리 cursor 리셋.
			// UI Toolkit 은 키 한 번에 KeyDownEvent 를 두 번 보냄: (1) keyCode 이벤트(character=0), (2) text 이벤트(keyCode=None, character set).
			// 위 분기들이 keyCode 이벤트(Tab/↑/↓/Enter)는 처리·return 함. 여기 도달하면 둘 중 하나:
			//   a) 우리가 신경 안 쓰는 keyCode 이벤트 (modifier 등) — character==0
			//   b) text 이벤트 — character != 0 (control char 인 \t \r \n 은 a/b 분리 로직 망치니 제외)
			bool isPrintableTyping = evt.character != 0
				&& evt.character != '\t'
				&& evt.character != '\r'
				&& evt.character != '\n';

			if (isPrintableTyping)
			{
				if (dropdown.IsOpen)
					dropdown.Hide();
				history.ResetCursor();
			}
		}

		private void HandleTab()
		{
			if (dropdown.IsOpen)
			{
				dropdown.MoveSelection(1);
				return;
			}

			DevAutocomplete.Result result = DevAutocomplete.Compute(inputField.value);
			if (result.HasMatch == false)
				return;

			if (result.Candidates.Length == 1)
			{
				string newInput = DevAutocomplete.ApplyCandidate(inputField.value, result.Prefix, result.Candidates[0], true);
				SetInputAndCursorEnd(newInput);
				return;
			}

			// CommandLineField 의 worldBound 를 anchor 로 — dropdown 은 OverlayLayer 에서 panel 좌표로 absolute 배치.
			dropdown.Show(result.Prefix, result.Candidates, worldBound);
		}

		private void ConfirmDropdownSelection()
		{
			string selected = dropdown.SelectedCandidate;
			string prefix = dropdown.Prefix;
			dropdown.Hide();

			if (string.IsNullOrEmpty(selected) == false)
			{
				string newInput = DevAutocomplete.ApplyCandidate(inputField.value, prefix, selected, true);
				inputField.SetValueWithoutNotify(newInput);
			}

			// re-focus + cursor at end on next frame — Focus() 는 기본적으로 전체 선택하므로
			// cursorIndex/selectIndex 를 끝으로 collapse 시켜 selection 해제.
			int end = inputField.value.Length;
			inputField.schedule.Execute(() =>
			{
				inputField.Focus();
				inputField.cursorIndex = end;
				inputField.selectIndex = end;
			}).StartingIn(0);
		}

		private void NavigateHistory(bool up)
		{
			string entry = up ? history.Previous() : history.Next();
			if (entry == null)
				return;
			SetInputAndCursorEnd(entry);
		}

		/// <summary>텍스트를 교체하고 커서를 끝으로 보냄. SetValueWithoutNotify 만으론 cursorIndex 가 안 따라옴.</summary>
		private void SetInputAndCursorEnd(string text)
		{
			inputField.SetValueWithoutNotify(text);
			int end = text != null ? text.Length : 0;
			inputField.cursorIndex = end;
			inputField.selectIndex = end;
		}

		private void OnFocusInInternal(FocusInEvent evt) => OnFocusIn.Invoke();
		private void OnFocusOutInternal(FocusOutEvent evt) => OnFocusOut.Invoke();
	}
}
