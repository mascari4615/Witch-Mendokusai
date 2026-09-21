using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle.UI
{
	internal sealed class DollSelectionController
	{
		private readonly VisualElement popup;
		private readonly ModalController modalController;
		private readonly DollVisualPresenter visualPresenter;
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

		public DollSelectionController(
			VisualElement popup,
			VisualTreeAsset choiceCardAsset,
			ModalController modalController,
			DollVisualPresenter visualPresenter,
			UIContentSO content,
			Action<int> selected)
		{
			this.popup = popup;
			this.modalController = modalController;
			this.visualPresenter = visualPresenter;
			this.content = content;
			this.selected = selected;

			modalController.Register(popup, Close);
			popup.RequireQ<Button>("doll-close").clicked += Close;
			VisualElement grid = popup.RequireQ<VisualElement>("doll-grid");
			pageLabel = popup.RequireQ<Label>("doll-page-label");
			pageBack = popup.RequireQ<Button>("doll-page-back");
			pageForward = popup.RequireQ<Button>("doll-page-forward");
			pageBack.clicked += () => ChangePage(-1);
			pageForward.clicked += () => ChangePage(1);

			for (int index = 0; index < content.DollPopupSlotCount; index++)
			{
				int captured = index;
				TemplateContainer choiceTree = choiceCardAsset.Instantiate();
				Button choice = choiceTree.RequireQ<Button>("choice");
				VisualElement icon = choice.RequireQ<VisualElement>("choice-icon");
				Label label = choice.RequireQ<Label>("choice-label");
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

			int pageCount = FixedGridPager.PageCount(current.Dolls.Length, content.DollPopupSlotCount);
			page = FixedGridPager.ClampPage(page, current.Dolls.Length, content.DollPopupSlotCount);
			pageLabel.text = content.PopupPageText(page + 1, pageCount);
			pageBack.SetEnabled(page > 0);
			pageForward.SetEnabled(page + 1 < pageCount);

			for (int index = 0; index < buttons.Count; index++)
			{
				Button choice = buttons[index];
				int dollIndex = FixedGridPager.ItemIndex(page, index, content.DollPopupSlotCount);
				bool shown = dollIndex < current.Dolls.Length;
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

				IdleDollView doll = current.Dolls[dollIndex];
				labels[index].text = content.DollChoiceText(doll.Name, doll.Stars, doll.Level, content.AxisName(doll.Axis));
				visualPresenter.SetAxis(icons[index], doll.Axis);
				visualPresenter.SetPortrait(icons[index], doll.Id);
				icons[index].style.display = DisplayStyle.Flex;
				visualPresenter.SetStars(choice, doll.Stars);
				int currentDoll = SelectedSeat >= 0 && SelectedSeat < current.Party.Length
					? current.Party[SelectedSeat]
					: -1;
				choice.EnableInClassList("idle-choice-card--selected", currentDoll == doll.Id);
			}
		}

		private void SelectAt(int index)
		{
			int dollIndex = FixedGridPager.ItemIndex(page, index, content.DollPopupSlotCount);
			if (dollIndex >= 0 && dollIndex < snapshot.Dolls.Length)
			{
				selected(snapshot.Dolls[dollIndex].Id);
			}
		}

		private void ChangePage(int delta)
		{
			page += delta;
			Render(snapshot);
		}

	}
}
