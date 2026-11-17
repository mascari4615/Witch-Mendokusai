using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 전체 메뉴 칸 이동. 방향 키, 패드, 마우스 올리기가 같은 선택 하나를 움직임
	public partial class FullMenuView
	{
		private void BuildNavigation()
		{
			container.RegisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
		}

		// 마우스를 올린 칸이 곧 선택된 칸. 강조가 둘로 갈리지 않게
		private void RegisterNav(Button button)
		{
			button.AddToClassList(NAV_CLASS);
			button.RegisterCallback<PointerEnterEvent>(_ =>
			{
				if (IsOpen && button.focusController?.focusedElement != button)
					button.Focus();
			});
		}

		// 기본 포커스 이동 차단. HUD 쪽 요소로 포커스가 나가는 문제 방지 (SettingView 와 같은 이유)
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

			VisualElement next = WMFocusNavigation.FindNext(current, CollectCandidates(), evt.direction, Settings.NavOverlapTolerance, Settings.NavAcrossWeight);
			if (next != null)
				WMFocusNavigation.MoveFocus(next);
		}

		// 열린 직후에는 배치 계산 전이라 칸 크기가 0. 칸이 잡힐 때까지 다음 프레임에 다시
		private void FocusWhenReady()
		{
			bool done = false;
			container.schedule.Execute(() => done = IsOpen == false || FocusFirst()).Every(0).Until(() => done);
		}

		private bool FocusFirst()
		{
			List<VisualElement> candidates = CollectCandidates();
			if (candidates.Count == 0)
				return false;

			// 설정에서 돌아왔으면 떠나기 전 칸으로
			VisualElement returnTarget = focusOnReturn;
			focusOnReturn = null;
			if (returnTarget != null && candidates.Contains(returnTarget))
			{
				returnTarget.Focus();
				return true;
			}

			foreach (VisualElement candidate in candidates)
			{
				if (candidate != closeButton)
				{
					candidate.Focus();
					return true;
				}
			}
			return false;
		}

		private List<VisualElement> CollectCandidates()
		{
			List<VisualElement> result = new();
			container.Query<VisualElement>(className: NAV_CLASS).ForEach(element =>
			{
				if (element.resolvedStyle.display == DisplayStyle.None || element.worldBound.width <= 0f)
					return;
				result.Add(element);
			});
			return result;
		}
	}
}
