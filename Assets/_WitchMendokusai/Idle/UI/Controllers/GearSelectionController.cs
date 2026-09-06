using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle.UI
{
	internal sealed class GearSelectionController
	{
		private readonly VisualElement popup;
		private readonly Label title;
		private readonly Label worn;
		private readonly VisualElement rows;
		private readonly VisualTreeAsset choiceCardAsset;
		private readonly ModalController modalController;
		private readonly GearVisualPresenter visualPresenter;
		private readonly UIContentSO content;
		private readonly Action<int> selected;
		private readonly Label pageLabel;
		private readonly Button pageBack;
		private readonly Button pageForward;
		private readonly List<Button> buttons = new List<Button>();
		private readonly List<VisualElement> icons = new List<VisualElement>();
		private readonly List<Label> labels = new List<Label>();
		private readonly List<int> matchingBagIndices = new List<int>();
		private IdleSnapshot snapshot;
		private IdleItem equipped;
		private int wearer;
		private int page;

		public GearSelectionController(
			VisualElement popup,
			VisualTreeAsset choiceCardAsset,
			ModalController modalController,
			GearVisualPresenter visualPresenter,
			UIContentSO content,
			Action<int> selected)
		{
			this.popup = popup;
			this.choiceCardAsset = choiceCardAsset;
			this.modalController = modalController;
			this.visualPresenter = visualPresenter;
			this.content = content;
			this.selected = selected;

			title = popup.RequireQ<Label>("gear-title");
			worn = popup.RequireQ<Label>("gear-worn");
			rows = popup.RequireQ<VisualElement>("gear-rows");
			pageLabel = popup.RequireQ<Label>("gear-page-label");
			pageBack = popup.RequireQ<Button>("gear-page-back");
			pageForward = popup.RequireQ<Button>("gear-page-forward");
			modalController.Register(popup, Close);
			popup.RequireQ<Button>("gear-close").clicked += Close;
			pageBack.clicked += () => ChangePage(-1);
			pageForward.clicked += () => ChangePage(1);
		}

		public int SelectedSlot { get; private set; } = -1;

		public void Open(int slot)
		{
			SelectedSlot = slot;
			page = 0;
			modalController.Show(popup);
		}

		public void Close()
		{
			SelectedSlot = -1;
			modalController.Hide(popup);
		}

		public void Render(IdleSnapshot snapshot, IdleItem equipped, int wearer)
		{
			if (SelectedSlot < 0)
			{
				return;
			}

			this.snapshot = snapshot;
			this.equipped = equipped;
			this.wearer = wearer;
			title.text = wearer >= 0
				? IdleHeroes.KindOf(wearer).Name + " " + content.GearSlotName(SelectedSlot)
				: content.GearSlotName(SelectedSlot);

			worn.text = content.EquippedGearText(equipped);

			EnsureRows(content.GearPopupSlotCount);
			matchingBagIndices.Clear();
			for (int index = 0; index < snapshot.Bag.Length; index++)
			{
				IdleItem item = snapshot.Bag[index];
				if ((int)item.Slot == SelectedSlot)
				{
					matchingBagIndices.Add(index);
				}
			}

			int pageCount = FixedGridPager.PageCount(matchingBagIndices.Count, content.GearPopupSlotCount);
			page = FixedGridPager.ClampPage(page, matchingBagIndices.Count, content.GearPopupSlotCount);
			pageLabel.text = content.PopupPageText(page + 1, pageCount);
			pageBack.SetEnabled(page > 0);
			pageForward.SetEnabled(page + 1 < pageCount);

			for (int index = 0; index < buttons.Count; index++)
			{
				Button row = buttons[index];
				int matchingIndex = FixedGridPager.ItemIndex(page, index, content.GearPopupSlotCount);
				if (matchingIndex < matchingBagIndices.Count)
				{
					int bagIndex = matchingBagIndices[matchingIndex];
					IdleItem item = snapshot.Bag[bagIndex];
					row.userData = bagIndex;
					row.SetEnabled(true);
					row.EnableInClassList("idle-choice-card--empty", false);
					visualPresenter.SetTierOutline(row, item.Tier);
					row.text = string.Empty;
					labels[index].text = content.GearPotentialText(item.IsRaw, item.PotentialValue);
					icons[index].style.display = DisplayStyle.Flex;
					visualPresenter.SetSprite(icons[index], SelectedSlot, item.Tier);
					row.style.display = DisplayStyle.Flex;
					continue;
				}

				row.userData = -1;
				row.text = string.Empty;
				row.SetEnabled(false);
				row.EnableInClassList("idle-choice-card--empty", true);
				visualPresenter.SetTierOutline(row, 0);
				labels[index].text = string.Empty;
				icons[index].style.display = DisplayStyle.None;
				row.style.display = DisplayStyle.Flex;
			}
		}

		private void ChangePage(int delta)
		{
			page += delta;
			Render(snapshot, equipped, wearer);
		}

		private void EnsureRows(int count)
		{
			for (int index = 0; index < count; index++)
			{
				RowAt(index);
			}
		}

		private Button RowAt(int index)
		{
			while (buttons.Count <= index)
			{
				TemplateContainer tree = choiceCardAsset.Instantiate();
				Button made = tree.RequireQ<Button>("choice");
				VisualElement icon = made.RequireQ<VisualElement>("choice-icon");
				Label label = made.RequireQ<Label>("choice-label");
				made.RemoveFromHierarchy();
				made.AddToClassList("idle-gear-card");
				rows.Add(made);
				int captured = buttons.Count;
				made.clicked += () => SelectAt(captured);
				buttons.Add(made);
				icons.Add(icon);
				labels.Add(label);
			}

			return buttons[index];
		}

		private void SelectAt(int rowIndex)
		{
			if (rowIndex < 0 || rowIndex >= buttons.Count)
			{
				return;
			}

			if (buttons[rowIndex].userData is int bagIndex && bagIndex >= 0)
			{
				selected(bagIndex);
				Close();
			}
		}
	}
}
