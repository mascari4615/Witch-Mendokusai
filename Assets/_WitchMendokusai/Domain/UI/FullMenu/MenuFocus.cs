using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 전체 메뉴 결의 화면 (ESC 메뉴, 업적) 칸 이동. 방향 키, 패드, 마우스 올리기가 같은 선택 하나를 움직임.
	/// 기본 포커스 이동은 막음 (HUD 쪽 요소로 포커스가 나가지 않게). 후보는 navClass 가 붙고 보이는 요소
	/// </summary>
	public sealed class MenuFocus
	{
		private readonly VisualElement root;
		private readonly string navClass;
		private readonly Func<bool> isOpen;
		private readonly Func<float> overlapTolerance;
		private readonly Func<float> acrossWeight;

		/// <summary>후보를 고르는 범위를 좁힘 (예: 업적 상세 창이 떠 있으면 그 창 안만). 비우면 root 전체</summary>
		public Func<VisualElement> Scope { get; set; }

		/// <summary>후보가 없거나 선택 밖에서 방향을 누르면 부름. 처음 고를 칸을 정함</summary>
		public Func<bool> FocusFirst { get; set; }

		/// <param name="overlapTolerance">수치는 화면 설정 SO 에서 매번 읽음</param>
		public MenuFocus(VisualElement root, string navClass, Func<bool> isOpen, Func<float> overlapTolerance, Func<float> acrossWeight)
		{
			this.root = root;
			this.navClass = navClass;
			this.isOpen = isOpen;
			this.overlapTolerance = overlapTolerance;
			this.acrossWeight = acrossWeight;
			root.RegisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
		}

		/// <summary>칸으로 등록. 마우스를 올린 칸이 곧 선택된 칸 (강조가 둘로 갈리지 않게)</summary>
		public void Register(VisualElement element)
		{
			element.AddToClassList(navClass);
			element.RegisterCallback<PointerEnterEvent>(_ =>
			{
				if (isOpen() && element.focusController?.focusedElement != element)
				{
					element.Focus();
				}
			});
		}

		public List<VisualElement> Collect()
		{
			List<VisualElement> result = new();
			VisualElement scope = Scope != null ? Scope() : root;
			scope.Query<VisualElement>(className: navClass).ForEach(element =>
			{
				if (element.resolvedStyle.display == DisplayStyle.None || element.worldBound.width <= 0f || IsShown(element) == false)
				{
					return;
				}
				result.Add(element);
			});
			return result;
		}

		/// <summary>열린 직후에는 배치 계산 전이라 칸 크기가 0. 칸이 잡힐 때까지 다음 프레임에 다시</summary>
		public void FocusWhenReady()
		{
			bool done = false;
			root.schedule.Execute(() => done = isOpen() == false || FocusFirst()).Every(0).Until(() => done);
		}

		/// <summary>화면을 바꾼 직후 정한 칸에 포커스. 배치 계산 전이면 칸이 잡힐 때까지 기다리고, 칸이 사라졌거나 비면 처음 칸으로</summary>
		public void FocusLater(VisualElement target)
		{
			if (target == null)
			{
				FocusWhenReady();
				return;
			}
			bool done = false;
			root.schedule.Execute(() =>
			{
				if (isOpen() == false)
				{
					done = true;
				}
				else if (target.panel == null)
				{
					done = FocusFirst();
				}
				else if (target.worldBound.width > 0f && IsShown(target))
				{
					target.Focus();
					done = true;
				}
			}).Every(0).Until(() => done);
		}

		private void OnNavigationMove(NavigationMoveEvent evt)
		{
			evt.StopPropagation();
			root.focusController?.IgnoreEvent(evt);

			VisualElement current = root.focusController?.focusedElement as VisualElement;
			List<VisualElement> candidates = Collect();
			if (current == null || current.ClassListContains(navClass) == false || candidates.Contains(current) == false)
			{
				FocusFirst();
				return;
			}

			VisualElement next = WMFocusNavigation.FindNext(current, candidates, evt.direction, overlapTolerance(), acrossWeight());
			if (next != null)
			{
				WMFocusNavigation.MoveFocus(next);
			}
		}

		// 숨긴 화면 (display none) 안의 칸은 자기 display 가 Flex 라도 후보가 아님
		private static bool IsShown(VisualElement element)
		{
			for (VisualElement parent = element.parent; parent != null; parent = parent.parent)
			{
				if (parent.resolvedStyle.display == DisplayStyle.None)
				{
					return false;
				}
			}
			return true;
		}
	}
}
