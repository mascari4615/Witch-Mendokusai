using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 설정 창 키보드 이동, 선택 틀, 위험 항목 확인 창
	public partial class SettingView
	{
		private const string NAV_CLASS = "wm-setting-nav";
		private const string HIDDEN_FRAME_CLASS = "wm-setting-frame--hidden";

		private VisualElement selectFrame;
		private VisualElement confirmLayer;
		private Label confirmTitle;
		private Label confirmDescription;
		private Button confirmCancel;
		private Button confirmAccept;
		private Action confirmAction;
		private VisualElement lastFocused;

		private bool IsConfirmOpen => confirmLayer != null && confirmLayer.style.display == DisplayStyle.Flex;

		private void BuildNavigation()
		{
			selectFrame = new VisualElement { pickingMode = PickingMode.Ignore };
			selectFrame.AddToClassList("wm-setting-frame");
			selectFrame.AddToClassList(HIDDEN_FRAME_CLASS);
			VisualElement frameInner = new VisualElement { pickingMode = PickingMode.Ignore };
			frameInner.AddToClassList("wm-setting-frame__inner");
			selectFrame.Add(frameInner);

			container.RegisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
			container.RegisterCallback<FocusInEvent>(OnFocusIn);
		}

		private void BuildConfirm()
		{
			confirmLayer = new VisualElement();
			confirmLayer.AddToClassList("wm-setting-confirm-layer");
			confirmLayer.style.display = DisplayStyle.None;

			VisualElement box = new VisualElement();
			box.AddToClassList("wm-setting-confirm");
			confirmTitle = new Label();
			confirmTitle.AddToClassList("wm-setting-confirm__title");
			box.Add(confirmTitle);
			confirmDescription = new Label();
			confirmDescription.AddToClassList("wm-setting-confirm__desc");
			box.Add(confirmDescription);

			VisualElement buttons = new VisualElement();
			buttons.AddToClassList("wm-setting-confirm__buttons");
			confirmCancel = new Button(CancelConfirm) { text = "취소" };
			confirmCancel.AddToClassList("wm-setting-btn");
			confirmCancel.AddToClassList(NAV_CLASS);
			buttons.Add(confirmCancel);
			confirmAccept = new Button(AcceptConfirm);
			confirmAccept.AddToClassList("wm-setting-btn");
			confirmAccept.AddToClassList("wm-setting-btn--danger");
			confirmAccept.AddToClassList(NAV_CLASS);
			buttons.Add(confirmAccept);
			box.Add(buttons);

			confirmLayer.Add(box);
			stage.Add(confirmLayer);
			stage.Add(selectFrame);
		}

		private void AskConfirm(string title, string description, string acceptLabel, Action action)
		{
			lastFocused = container.focusController?.focusedElement as VisualElement;
			confirmTitle.text = title;
			confirmDescription.text = description;
			confirmAccept.text = acceptLabel;
			confirmAction = action;
			confirmLayer.style.display = DisplayStyle.Flex;
			confirmCancel.schedule.Execute(() => confirmCancel.Focus());
		}

		private void AskConfirmWithSound(string title, string description, string acceptLabel, Action action)
		{
			UiSfx.Play(UiSfxEvent.Confirm);
			AskConfirm(title, description, acceptLabel, action);
		}

		private void CloseConfirm()
		{
			confirmLayer.style.display = DisplayStyle.None;
			confirmAction = null;
			VisualElement target = lastFocused;
			if (target != null)
				target.schedule.Execute(() => target.Focus());
		}

		private void CancelConfirm()
		{
			UiSfx.Play(UiSfxEvent.Cancel);
			CloseConfirm();
		}

		private void AcceptConfirm()
		{
			UiSfx.Play(UiSfxEvent.Confirm);
			Action action = confirmAction;
			CloseConfirm();
			action?.Invoke();
		}

		private void OnFocusIn(FocusInEvent evt)
		{
			if (evt.target is VisualElement element)
			{
				MoveFrameTo(element);
				if (element.ClassListContains("wm-setting-tab"))
				{
					foreach (KeyValuePair<string, Button> pair in tabButtons)
						if (pair.Value == element)
							SwitchTab(pair.Key);
				}
			}
		}

		// 슬라이더와 토글은 줄 전체, 나머지는 자기 영역에 틀 표시
		private void MoveFrameTo(VisualElement element)
		{
			VisualElement bounds = element;
			for (VisualElement walker = element; walker != null && walker != stage; walker = walker.parent)
			{
				if (walker.ClassListContains("wm-setting-row"))
				{
					bounds = walker;
					break;
				}
				if (walker.ClassListContains(NAV_CLASS))
					bounds = walker;
			}
			Rect rect = stage.WorldToLocal(bounds.worldBound);
			selectFrame.RemoveFromClassList(HIDDEN_FRAME_CLASS);
			selectFrame.style.left = rect.x - frameOutset;
			selectFrame.style.top = rect.y - frameOutset;
			selectFrame.style.width = rect.width + frameOutset * 2f;
			selectFrame.style.height = rect.height + frameOutset * 2f;
		}

		// 기본 포커스 이동 차단. HUD 쪽 요소로 포커스가 나가는 문제 방지
		private void OnNavigationMove(NavigationMoveEvent evt)
		{
			evt.StopPropagation();
			container.focusController?.IgnoreEvent(evt);

			VisualElement current = container.focusController?.focusedElement as VisualElement;
			if (current == null || current.ClassListContains(NAV_CLASS) == false)
			{
				FocusFirst();
				return;
			}

			bool isHorizontal = evt.direction == NavigationMoveEvent.Direction.Left || evt.direction == NavigationMoveEvent.Direction.Right;
			if (isHorizontal && current is Slider slider)
			{
				float sign = evt.direction == NavigationMoveEvent.Direction.Right ? 1f : -1f;
				slider.value = Mathf.Clamp(slider.value + sign * sliderStep, slider.lowValue, slider.highValue);
				return;
			}

			VisualElement next = FindNext(current, evt.direction);
			if (next == null)
				return;
			if (next.ClassListContains("wm-setting-tab"))
				next.Focus();
			else
				WMFocusNavigation.MoveFocus(next);
		}

		private void FocusFirst()
		{
			List<VisualElement> candidates = CollectCandidates();
			foreach (VisualElement candidate in candidates)
			{
				if (candidate.ClassListContains("wm-setting-tab") == false && candidate != closeButton)
				{
					candidate.Focus();
					return;
				}
			}
			if (candidates.Count > 0)
				candidates[0].Focus();
		}

		private List<VisualElement> CollectCandidates()
		{
			VisualElement scope = IsConfirmOpen ? confirmLayer : container;
			List<VisualElement> result = new();
			scope.Query<VisualElement>(className: NAV_CLASS).ForEach(element =>
			{
				if (element.resolvedStyle.display == DisplayStyle.None || element.worldBound.width <= 0f)
					return;
				if (IsConfirmOpen == false && confirmLayer.Contains(element))
					return;
				result.Add(element);
			});
			return result;
		}

		// 탭 줄 밖에서 위로 오면 현재 탭으로만 진입
		private VisualElement FindNext(VisualElement current, NavigationMoveEvent.Direction direction)
		{
			List<VisualElement> candidates = CollectCandidates();
			if (current.ClassListContains("wm-setting-tab") == false)
				candidates.RemoveAll(candidate => candidate.ClassListContains("wm-setting-tab") && candidate != tabButtons[currentTabKey]);
			return WMFocusNavigation.FindNext(current, candidates, direction, navOverlapTolerance, navAcrossWeight);
		}
	}
}
