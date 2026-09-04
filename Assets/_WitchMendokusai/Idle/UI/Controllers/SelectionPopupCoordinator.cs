using System;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle.UI
{
	internal sealed class SelectionPopupCoordinator
	{
		private readonly IdleSession session;
		private readonly UIContentSO content;
		private readonly HeroSelectionController heroSelection;
		private readonly GearSelectionController gearSelection;
		private readonly Action closeAuxiliaryPopups;
		private readonly Action writeDown;
		private readonly Action requestRender;
		private readonly Action<string, float> showNote;
		private readonly float noteSeconds;
		private int gearSeat;

		public SelectionPopupCoordinator(
			VisualElement heroPopup,
			VisualElement gearPopup,
			VisualTreeAsset choiceCardAsset,
			ModalController modalController,
			HeroVisualPresenter heroVisualPresenter,
			GearVisualPresenter gearVisualPresenter,
			IdleSession session,
			UIContentSO content,
			ItemPageController itemPage,
			Action closeAuxiliaryPopups,
			Action writeDown,
			Action requestRender,
			Action<string, float> showNote,
			float noteSeconds)
		{
			this.session = session;
			this.content = content;
			this.closeAuxiliaryPopups = closeAuxiliaryPopups;
			this.writeDown = writeDown;
			this.requestRender = requestRender;
			this.showNote = showNote;
			this.noteSeconds = noteSeconds;
			heroSelection = new HeroSelectionController(
				heroPopup, choiceCardAsset, modalController,
				heroVisualPresenter, content, ChooseHero);
			gearSelection = new GearSelectionController(
				gearPopup, choiceCardAsset, modalController,
				gearVisualPresenter, content, itemPage.Equip);
		}

		public int GearSeat => gearSeat;

		public int HeroId => session.HeroAtPartySlot(gearSeat);

		public int SelectingPartySeat => heroSelection.SelectedSeat;

		public void OpenHero(int slot)
		{
			IdleSnapshot snapshot = session.Capture();
			if (slot < 0 || slot >= snapshot.Party.Length)
			{
				slot = 0;
			}

			gearSeat = slot;
			gearSelection.Close();
			closeAuxiliaryPopups();
			heroSelection.Open(slot);
			requestRender();
		}

		public void OpenGear(int slot)
		{
			if (HeroId < 0)
			{
				showNote(content.SelectHeroBeforeGearText, noteSeconds);
				return;
			}

			heroSelection.Close();
			closeAuxiliaryPopups();
			gearSelection.Open(slot);
			requestRender();
		}

		public void CloseAll()
		{
			heroSelection.Close();
			gearSelection.Close();
		}

		public void ClearHeroSelection()
		{
			heroSelection.ClearSelection();
		}

		public void Render(IdleSnapshot snapshot)
		{
			heroSelection.Render(snapshot);
			int heroId = HeroId;
			IdleItem equipped = heroId >= 0 && gearSelection.SelectedSlot >= 0
				? session.WornOf(heroId, gearSelection.SelectedSlot)
				: default;
			gearSelection.Render(snapshot, equipped, heroId);
		}

		private void ChooseHero(int heroId)
		{
			int slot = heroSelection.SelectedSeat;
			if (slot < 0)
			{
				slot = FirstEmptySeat();
			}

			if (slot < 0)
			{
				showNote(content.PartyFullFeedback, noteSeconds);
				requestRender();
				return;
			}

			session.Send(new IdleSetPartyIntent(slot, heroId));
			gearSeat = slot;
			heroSelection.Close();
			writeDown();
			requestRender();
		}

		private int FirstEmptySeat()
		{
			IdleSnapshot snapshot = session.Capture();
			for (int slot = 0; slot < snapshot.Party.Length; slot++)
			{
				if (snapshot.Party[slot] < 0)
				{
					return slot;
				}
			}

			return -1;
		}
	}
}
