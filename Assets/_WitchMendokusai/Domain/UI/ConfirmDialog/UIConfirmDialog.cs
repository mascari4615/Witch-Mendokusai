using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 확인창 공용 부품. 질문 한 줄, 설명 한 줄, 선택 버튼 2~3개
	/// 부모 요소 전체를 어두운 막으로 덮고 가운데 창. 좌우 방향키와 Tab 으로 이동, 확인 키로 고름
	/// 취소 키는 부모 쪽이 Cancel() 로 넘김 (마지막 선택지, 그대로 두기)
	/// 모양은 ConfirmDialog.uss
	/// </summary>
	public class UIConfirmDialog : VisualElement
	{
		public const string USS_CLASS = "wm-confirm";
		private const string USS_BOX = USS_CLASS + "__box";
		private const string USS_TITLE = USS_CLASS + "__title";
		private const string USS_TEXT = USS_CLASS + "__text";
		private const string USS_ROW = USS_CLASS + "__row";
		private const string USS_BUTTON = USS_CLASS + "__button";
		private const string USS_BUTTON_MAIN = USS_BUTTON + "--main";

		public readonly struct Choice
		{
			public readonly string Text;
			public readonly Action OnChosen;
			public readonly bool IsMain;

			public Choice(string text, Action onChosen, bool isMain = false)
			{
				Text = text;
				OnChosen = onChosen;
				IsMain = isMain;
			}
		}

		public bool IsOpen { get; private set; }

		private readonly Label titleLabel;
		private readonly Label textLabel;
		private readonly VisualElement row;
		private readonly List<Button> buttons = new();
		private readonly List<Choice> choices = new();
		private int focusedIndex;

		public UIConfirmDialog()
		{
			name = "ConfirmDialog";
			AddToClassList(USS_CLASS);
			AddToClassList(UIMotion.POP);
			VisualElement box = AddChild(this, new VisualElement(), USS_BOX);
			titleLabel = AddChild(box, new Label(), USS_TITLE);
			textLabel = AddChild(box, new Label(), USS_TEXT);
			row = AddChild(box, new VisualElement(), USS_ROW);
			RegisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
			// 창 안 어디를 눌러도 초점은 창 안에. 누른 뒤 초점이 창 밖이면 마지막 선택지 자리로
			RegisterCallback<PointerUpEvent>(_ => schedule.Execute(() =>
			{
				VisualElement focused = focusController?.focusedElement as VisualElement;
				if (IsOpen && (focused == null || Contains(focused) == false))
					buttons[focusedIndex].Focus();
			}));
			UIMotion.SetInstant(this, false);
		}

		/// <summary> 처음 초점은 마지막 선택지 (보통 "계속" 쪽, 실수로 버리지 않게) </summary>
		public void Open(string title, string text, params Choice[] newChoices)
		{
			titleLabel.text = title;
			textLabel.text = text;
			textLabel.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
			choices.Clear();
			choices.AddRange(newChoices);
			EnsureButtonCount(choices.Count);
			for (int i = 0; i < buttons.Count; i++)
			{
				bool isUsed = i < choices.Count;
				buttons[i].style.display = isUsed ? DisplayStyle.Flex : DisplayStyle.None;
				if (isUsed == false)
					continue;
				buttons[i].text = choices[i].Text;
				buttons[i].EnableInClassList(USS_BUTTON_MAIN, choices[i].IsMain);
			}
			IsOpen = true;
			BringToFront();
			UIMotion.Show(this);
			focusedIndex = choices.Count - 1;
			buttons[focusedIndex].Focus();
		}

		/// <summary> 고르지 않고 닫기. 부모 창이 통째로 닫힐 때, 콜백 없음 </summary>
		public void Dismiss()
		{
			if (IsOpen == false)
				return;
			IsOpen = false;
			UIMotion.Hide(this);
		}

		/// <summary> 취소 키. 마지막 선택지와 같음 </summary>
		public void Cancel() => Choose(choices.Count - 1);

		private void Choose(int index)
		{
			if (IsOpen == false)
				return;
			IsOpen = false;
			UIMotion.Hide(this);
			choices[index].OnChosen?.Invoke();
		}

		private void OnNavigationMove(NavigationMoveEvent evt)
		{
			int next = focusedIndex;
			switch (evt.direction)
			{
				case NavigationMoveEvent.Direction.Left:
				case NavigationMoveEvent.Direction.Previous:
					next = Mathf.Max(0, focusedIndex - 1);
					break;
				case NavigationMoveEvent.Direction.Right:
				case NavigationMoveEvent.Direction.Next:
					next = Mathf.Min(choices.Count - 1, focusedIndex + 1);
					break;
			}
			evt.StopPropagation();
			focusController?.IgnoreEvent(evt);
			if (next == focusedIndex)
				return;
			focusedIndex = next;
			WMFocusNavigation.MoveFocus(buttons[focusedIndex]);
		}

		private void EnsureButtonCount(int count)
		{
			while (buttons.Count < count)
			{
				int index = buttons.Count;
				Button button = AddChild(row, new Button(() => Choose(index)) { name = "ConfirmChoice" + index }, USS_BUTTON);
				button.RegisterCallback<FocusInEvent>(_ => focusedIndex = index);
				buttons.Add(button);
			}
		}

		private static T AddChild<T>(VisualElement parent, T child, string ussClass) where T : VisualElement
		{
			child.AddToClassList(ussClass);
			parent.Add(child);
			return child;
		}
	}
}
