using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle.UI
{
	internal sealed class HeroSelectionController
	{
		private readonly VisualElement popup;
		private readonly ModalController modalController;
		private readonly HeroVisualPresenter visualPresenter;
		private readonly UIContentSO content;
		private readonly Action<int> selected;
		private readonly Label pageLabel;
		private readonly Button pageBack;
		private readonly Button pageForward;
		private readonly List<Button> buttons = new List<Button>();
		private readonly List<VisualElement> icons = new List<VisualElement>();
		private readonly List<Label> labels = new List<Label>();
		private IdleSnapshot snapshot;
		private int page;

		public HeroSelectionController(
			VisualElement popup,
			VisualTreeAsset choiceCardAsset,
			ModalController modalController,
			HeroVisualPresenter visualPresenter,
			UIContentSO content,
			Action<int> selected)
		{
			this.popup = popup;
			this.modalController = modalController;
			this.visualPresenter = visualPresenter;
			this.content = content;
			this.selected = selected;

			modalController.Register(popup, Close);
			popup.Q<Button>("hero-close").clicked += Close;
			VisualElement grid = popup.Q<VisualElement>("hero-grid");
			pageLabel = popup.Q<Label>("hero-page-label");
			pageBack = popup.Q<Button>("hero-page-back");
			pageForward = popup.Q<Button>("hero-page-forward");
			pageBack.clicked += () => ChangePage(-1);
			pageForward.clicked += () => ChangePage(1);

			for (int index = 0; index < content.HeroPopupSlotCount; index++)
			{
				int captured = index;
				TemplateContainer choiceTree = choiceCardAsset.Instantiate();
				Button choice = choiceTree.Q<Button>("choice");
				VisualElement icon = choice.Q<VisualElement>("choice-icon");
				Label label = choice.Q<Label>("choice-label");
				choice.RemoveFromHierarchy();
				choice.clicked += () => SelectAt(captured);
				grid.Add(choice);
				buttons.Add(choice);
				icons.Add(icon);
				labels.Add(label);
			}
		}

		public int SelectedSeat { get; private set; } = -1;

		public void Open(int seat)
		{
			SelectedSeat = seat;
			page = 0;
			modalController.Show(popup);
		}

		public void Close()
		{
			SelectedSeat = -1;
			modalController.Hide(popup);
		}

		public void ClearSelection()
		{
			SelectedSeat = -1;
		}

		public void Render(IdleSnapshot current)
		{
			snapshot = current;
			if (popup.style.display != DisplayStyle.Flex)
			{
				return;
			}

			int pageCount = FixedGridPager.PageCount(current.Heroes.Length, content.HeroPopupSlotCount);
			page = FixedGridPager.ClampPage(page, current.Heroes.Length, content.HeroPopupSlotCount);
			pageLabel.text = content.PopupPageText(page + 1, pageCount);
			pageBack.SetEnabled(page > 0);
			pageForward.SetEnabled(page + 1 < pageCount);

			for (int index = 0; index < buttons.Count; index++)
			{
				Button choice = buttons[index];
				int heroIndex = FixedGridPager.ItemIndex(page, index, content.HeroPopupSlotCount);
				bool shown = heroIndex < current.Heroes.Length;
				choice.style.display = DisplayStyle.Flex;
				choice.SetEnabled(shown);
				choice.EnableInClassList("idle-choice-card--empty", shown == false);

				if (shown == false)
				{
					labels[index].text = string.Empty;
					icons[index].style.display = DisplayStyle.None;
					visualPresenter.SetStars(choice, 0);
					choice.EnableInClassList("idle-choice-card--selected", false);
					continue;
				}

				IdleHeroView hero = current.Heroes[heroIndex];
				labels[index].text = content.HeroChoiceText(hero.Name, hero.Stars, hero.Level, content.AxisName(hero.Axis));
				visualPresenter.SetAxis(icons[index], hero.Axis);
				visualPresenter.SetPortrait(icons[index], hero.Id);
				icons[index].style.display = DisplayStyle.Flex;
				visualPresenter.SetStars(choice, hero.Stars);
				int currentHero = SelectedSeat >= 0 && SelectedSeat < current.Party.Length
					? current.Party[SelectedSeat]
					: -1;
				choice.EnableInClassList("idle-choice-card--selected", currentHero == hero.Id);
			}
		}

		private void SelectAt(int index)
		{
			int heroIndex = FixedGridPager.ItemIndex(page, index, content.HeroPopupSlotCount);
			if (heroIndex >= 0 && heroIndex < snapshot.Heroes.Length)
			{
				selected(snapshot.Heroes[heroIndex].Id);
			}
		}

		private void ChangePage(int delta)
		{
			page += delta;
			Render(snapshot);
		}

	}
}
