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
		private readonly List<Button> buttons = new List<Button>();
		private readonly List<VisualElement> icons = new List<VisualElement>();
		private readonly List<Label> labels = new List<Label>();

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

			title = popup.Q<Label>("gear-title");
			worn = popup.Q<Label>("gear-worn");
			rows = popup.Q<VisualElement>("gear-rows");
			modalController.Register(popup, Close);
			popup.Q<Button>("gear-close").clicked += Close;
		}

		public int SelectedSlot { get; private set; } = -1;

		public void Open(int slot)
		{
			SelectedSlot = slot;
			modalController.Show(popup);
		}

		public void Close()
		{
			SelectedSlot = -1;
			modalController.Hide(popup);
		}

		public void Render(IdleSnapshot snapshot, IdleState state, int wearer)
		{
			if (SelectedSlot < 0)
			{
				return;
			}

			title.text = wearer >= 0
				? IdleHeroes.KindOf(wearer).Name + " " + content.GearSlotName(SelectedSlot)
				: content.GearSlotName(SelectedSlot);

			IdleItem equipped = wearer >= 0 ? IdleGear.WornOf(state, wearer, SelectedSlot) : default;
			worn.text = content.EquippedGearText(equipped);

			EnsureRows(content.GearPopupSlotCount);
			int shown = 0;
			for (int index = 0; index < snapshot.Bag.Length; index++)
			{
				IdleItem item = snapshot.Bag[index];
				if ((int)item.Slot != SelectedSlot)
				{
					continue;
				}

				Button row = RowAt(shown);
				row.userData = index;
				row.SetEnabled(true);
				row.EnableInClassList("idle-choice-card--empty", false);
				visualPresenter.SetTierOutline(row, item.Tier);
				row.text = string.Empty;
				labels[shown].text = content.GearPotentialText(item.IsRaw, item.PotentialValue);
				icons[shown].style.display = DisplayStyle.Flex;
				visualPresenter.SetSprite(icons[shown], SelectedSlot, item.Tier);
				row.style.display = DisplayStyle.Flex;
				shown++;
			}

			for (int index = shown; index < buttons.Count; index++)
			{
				Button row = buttons[index];
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
				Button made = tree.Q<Button>("choice");
				VisualElement icon = made.Q<VisualElement>("choice-icon");
				Label label = made.Q<Label>("choice-label");
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
