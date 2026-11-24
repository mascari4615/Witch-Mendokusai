using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 탭 안 묶음과 왼쪽 목록. 머리글 하나가 한 묶음이고, 묶음이 둘 이상인 탭만 왼쪽 목록에 올림.
	// 목록 칸에 초점이 가면 그 묶음을 보임 (탭과 같은 방식)
	public partial class SettingView
	{
		private const string SIDE_GROUP_CLASS = "wm-setting-side-group";
		private const string SIDE_ITEM_CLASS = "wm-setting-side-item";
		private const string SIDE_ITEM_ACTIVE_CLASS = "wm-setting-side-item--active";
		private const string PAGE_CLASS = "wm-setting-page";

		private readonly Dictionary<string, VisualElement> sideGroups = new();
		private readonly Dictionary<string, List<VisualElement>> sectionPages = new();
		private readonly Dictionary<string, int> currentPages = new();

		// 본문의 머리글마다 묶음 상자를 만들어 뒤따르는 줄을 담음. 머리글 앞에 다른 요소가 있으면 설계 오류
		private void BuildSections(string tabKey, VisualElement content)
		{
			List<VisualElement> children = new(content.Children());
			List<VisualElement> pages = new();
			VisualElement page = null;
			foreach (VisualElement child in children)
			{
				if (child.ClassListContains("wm-setting-header"))
				{
					page = new VisualElement();
					page.AddToClassList(PAGE_CLASS);
					page.userData = ((Label)child).text;
					pages.Add(page);
				}
				if (page == null)
					throw new InvalidOperationException($"설정 탭 {tabKey} 의 첫 요소가 머리글이 아님");
				page.Add(child);
			}
			foreach (VisualElement addedPage in pages)
				content.Add(addedPage);
			sectionPages[tabKey] = pages;
			if (pages.Count < 2)
				return;

			VisualElement group = new VisualElement { userData = tabKey };
			group.AddToClassList(SIDE_GROUP_CLASS);
			group.style.display = DisplayStyle.None;
			sideList.Add(group);
			sideGroups[tabKey] = group;
			for (int i = 0; i < pages.Count; i++)
			{
				int pageIndex = i;
				Button item = new Button(() => SelectPage(tabKey, pageIndex)) { text = (string)pages[i].userData, userData = pageIndex };
				item.AddToClassList(SIDE_ITEM_CLASS);
				item.AddToClassList(NAV_CLASS);
				group.Add(item);
			}
			SelectPage(tabKey, 0, false);
		}

		private void SelectPage(string tabKey, int pageIndex, bool withSound = true)
		{
			List<VisualElement> pages = sectionPages[tabKey];
			if (withSound && currentPages.TryGetValue(tabKey, out int previous) && previous == pageIndex)
				return;
			// 키를 기다리는 중 묶음을 옮기면 기다림을 끝냄 (SwitchTab 과 같음). 안 그러면 안 보이는 동작에 키가 묶임
			if (withSound)
				inputManager.CancelRebind();
			for (int i = 0; i < pages.Count; i++)
				pages[i].style.display = i == pageIndex ? DisplayStyle.Flex : DisplayStyle.None;
			if (sideGroups.TryGetValue(tabKey, out VisualElement group))
			{
				for (int i = 0; i < group.childCount; i++)
					group[i].EnableInClassList(SIDE_ITEM_ACTIVE_CLASS, i == pageIndex);
			}
			currentPages[tabKey] = pageIndex;
			if (withSound == false)
				return;
			pages[pageIndex].GetFirstAncestorOfType<ScrollView>()?.ScrollTo(pages[pageIndex]);
			UiSfx.Play(UiSfxEvent.Tab);
		}

		// 탭을 바꿀 때: 그 탭의 목록만 보이고, 목록이 없는 탭은 본문이 넓어짐
		private void ShowSideGroup(string tabKey)
		{
			foreach (KeyValuePair<string, VisualElement> pair in sideGroups)
				pair.Value.style.display = pair.Key == tabKey ? DisplayStyle.Flex : DisplayStyle.None;
			bool hasSide = sideGroups.ContainsKey(tabKey);
			sideList.style.display = hasSide ? DisplayStyle.Flex : DisplayStyle.None;
		}

		// 목록 칸에 초점이 오면 그 묶음으로 (OnFocusIn)
		private void SelectPageOfSideItem(VisualElement item)
		{
			string tabKey = (string)item.parent.userData;
			SelectPage(tabKey, (int)item.userData);
		}
	}
}
