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
		private readonly Label title;
		private readonly Label caption;
		private readonly List<Button> tabButtons = new List<Button>();
		private readonly VisualElement[] pages;

		public SidePanelController(
			VisualElement shell,
			UIContentSO content,
			Action<int> openTab)
		{
			this.content = content;
			side = shell.RequireQ<VisualElement>("side");
			VisualElement tabs = side.RequireQ<VisualElement>("tabs");
			pages = new VisualElement[content.TabCount];

			for (int index = 0; index < content.TabCount; index++)
			{
				int captured = index;
				Button tab = tabs.RequireQ<Button>("tab-" + index);
				tab.clicked += () => openTab(captured);
				tab.text = content.TabButtonText(index);
				tab.style.display = content.IsTabVisible(index) ? DisplayStyle.Flex : DisplayStyle.None;
				tabButtons.Add(tab);
			}

			title = side.RequireQ<Label>("panel-title");
			caption = side.RequireQ<Label>("panel-caption");
		}

		public float ResolvedWidth => side.resolvedStyle.width;

		public VisualElement BindPage(int index, string hostName, VisualElement root)
		{
			VisualElement host = root.RequireQ<VisualElement>(hostName);
			VisualElement page = host.RequireQ<VisualElement>("page");
			page.style.display = DisplayStyle.None;
			pages[index] = page;
			return page;
		}

		public void Apply(int openIndex, bool split)
		{
			side.style.display = split ? DisplayStyle.Flex : DisplayStyle.None;
			if (split)
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
		}
	}
}
