using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 전체 메뉴 칸 이동. 방향 키, 패드, 마우스 올리기가 같은 선택 하나를 움직임 (MenuFocus)
	public partial class FullMenuView
	{
		private MenuFocus focus;

		// 칸을 짓기 (BuildUI) 전에 만듦. 칸마다 RegisterNav 가 여기에 등록
		private void BuildNavigation()
		{
			focus = new MenuFocus(container, NAV_CLASS, () => IsOpen, () => Settings.NavOverlapTolerance, () => Settings.NavAcrossWeight)
			{
				FocusFirst = FocusFirst,
			};
		}

		private void RegisterNav(Button button) => focus.Register(button);

		private void FocusWhenReady() => focus.FocusWhenReady();

		private bool FocusFirst()
		{
			List<VisualElement> candidates = focus.Collect();
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
	}
}
