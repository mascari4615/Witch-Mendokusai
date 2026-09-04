using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle.UI
{
	internal sealed class SidePanelController
	{
		private readonly UIContentSO content;
		private readonly VisualElement side;
		private readonly VisualElement floatingTabs;
		private readonly Button closeButton;
		private readonly Label title;
		private readonly Label caption;
		private readonly List<Button> tabButtons = new List<Button>();
		private readonly List<Button> floatingButtons = new List<Button>();
		private readonly VisualElement[] pages;

		public SidePanelController(
			VisualElement shell,
			VisualElement battle,
			UIContentSO content,
			Action<int> openTab,
			Action close)
		{
			this.content = content;
			side = shell.Q<VisualElement>("side");
			floatingTabs = battle.Q<VisualElement>("floating-tabs");
			VisualElement tabs = side.Q<VisualElement>("tabs");
			pages = new VisualElement[content.TabCount];

			for (int index = 0; index < content.TabCount; index++)
			{
				int captured = index;
				Button tab = tabs.Q<Button>("tab-" + index);
				tab.clicked += () => openTab(captured);
				tab.text = content.TabName(index) + "\n" + content.TabCaption(index);
				tab.style.display = content.IsTabVisible(index) ? DisplayStyle.Flex : DisplayStyle.None;
				tabButtons.Add(tab);

				Button floating = floatingTabs.Q<Button>("floating-tab-" + index);
				floating.clicked += () => openTab(captured);
				floating.text = content.TabName(index);
				floating.style.display = content.IsTabVisible(index) ? DisplayStyle.Flex : DisplayStyle.None;
				floatingButtons.Add(floating);
			}

			closeButton = tabs.Q<Button>("side-close");
			closeButton.clicked += close;
			closeButton.BringToFront();
			title = side.Q<Label>("panel-title");
			caption = side.Q<Label>("panel-caption");
			floatingTabs.BringToFront();
		}

		public float ResolvedWidth => side.resolvedStyle.width;

		public VisualElement BindPage(int index, string hostName, VisualElement root)
		{
			VisualElement host = root.Q<VisualElement>(hostName);
			VisualElement page = host.Q<VisualElement>("page");
			page.style.display = DisplayStyle.None;
			pages[index] = page;
			return page;
		}

		public void Apply(int openIndex, bool split, bool sideOpen)
		{
			bool shown = split || sideOpen;
			side.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
			side.EnableInClassList("idle-side--drawer", split == false);
			closeButton.style.display = split ? DisplayStyle.None : DisplayStyle.Flex;
			floatingTabs.style.display = split ? DisplayStyle.None : DisplayStyle.Flex;
			if (shown)
			{
				ShowPage(openIndex);
			}
		}

		public void ShowPage(int index)
		{
			for (int pageIndex = 0; pageIndex < pages.Length; pageIndex++)
			{
				pages[pageIndex].style.display = pageIndex == index ? DisplayStyle.Flex : DisplayStyle.None;
			}
			title.text = content.TabName(index);
			caption.text = content.TabCaption(index);
		}

		public void RenderBadges(IdleSnapshot snapshot, int openIndex, bool panelShown)
		{
			SetBadge(0, IdleAdvice.HasSomethingToDo(snapshot, IdleTab.Hero)
				|| IdleAdvice.HasSomethingToDo(snapshot, IdleTab.Upgrade));
			SetBadge(1, IdleAdvice.HasSomethingToDo(snapshot, IdleTab.Gear));
			SetBadge(3, snapshot.CanPull);
			SetBadge(4, snapshot.PrestigeAward > 0L);
			SetBadge(6, IdleAdvice.HasSomethingToDo(snapshot, IdleTab.Base));
			for (int index = 0; index < tabButtons.Count; index++)
			{
				tabButtons[index].EnableInClassList("idle-tab--on", index == openIndex && panelShown);
			}
		}

		private void SetBadge(int index, bool shown)
		{
			tabButtons[index].EnableInClassList("idle-tab--badge", shown);
			floatingButtons[index].EnableInClassList("idle-tab--badge", shown);
		}
	}
}
