using System;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle.UI
{
	internal sealed class SelectionPopupCoordinator
	{
		private readonly IdleSession session;
		private readonly UIContentSO content;
		private readonly DollSelectionController dollSelection;
		private readonly GearSelectionController gearSelection;
		private readonly Action closeAuxiliaryPopups;
		private readonly Action writeDown;
		private readonly Action requestRender;
		private readonly Action<string, float> showNote;
		private readonly float noteSeconds;
		private int gearSeat;

		public SelectionPopupCoordinator(
			VisualElement dollPopup,
			VisualElement gearPopup,
			VisualTreeAsset choiceCardAsset,
			ModalController modalController,
			DollVisualPresenter dollVisualPresenter,
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
			dollSelection = new DollSelectionController(
				dollPopup, choiceCardAsset, modalController,
				dollVisualPresenter, content, ChooseDoll);
			gearSelection = new GearSelectionController(
				gearPopup, choiceCardAsset, modalController,
				gearVisualPresenter, content, itemPage.Equip);
		}

		public int GearSeat => gearSeat;

		public int DollId => session.DollAtPartySlot(gearSeat);

		public int SelectingPartySeat => dollSelection.SelectedSeat;

		/// <summary>
		/// 자리 하나를 누름. 그 인형이 강화 대상 (사용자 2026-09-20: 아이콘을 누르면 선택 창이 떠서 강화를 못 함).
		/// 빈 자리만 선택 창. 인형 바꾸기는 큰 초상화 (OpenDoll)
		/// </summary>
		public void FocusDoll(int slot)
		{
			IdleSnapshot snapshot = session.Capture();
			if (slot < 0 || slot >= snapshot.Party.Length)
			{
				slot = 0;
			}

			if (snapshot.Party[slot] < 0)
			{
				OpenDoll(slot);
				return;
			}

			gearSeat = slot;
			dollSelection.Close();
			gearSelection.Close();
			requestRender();
		}

		public void OpenDoll(int slot)
		{
			IdleSnapshot snapshot = session.Capture();
			if (slot < 0 || slot >= snapshot.Party.Length)
			{
				slot = 0;
			}

			gearSeat = slot;
			gearSelection.Close();
			closeAuxiliaryPopups();
			dollSelection.Open(slot);
			requestRender();
		}

		public void OpenGear(int slot)
		{
			if (DollId < 0)
			{
				showNote(content.SelectDollBeforeGearText, noteSeconds);
				return;
			}

			dollSelection.Close();
			closeAuxiliaryPopups();
			gearSelection.Open(slot);
			requestRender();
		}

		public void CloseAll()
		{
			dollSelection.Close();
			gearSelection.Close();
		}

		public void ClearDollSelection()
		{
			dollSelection.ClearSelection();
		}

		public void Render(IdleSnapshot snapshot)
		{
			dollSelection.Render(snapshot);
			int dollId = DollId;
			IdleItem equipped = dollId >= 0 && gearSelection.SelectedSlot >= 0
				? session.WornOf(dollId, gearSelection.SelectedSlot)
				: default;
			gearSelection.Render(snapshot, equipped, dollId);
		}

		private void ChooseDoll(int dollId)
		{
			int slot = dollSelection.SelectedSeat;
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

			session.Send(new IdleSetPartyIntent(slot, dollId));
			gearSeat = slot;
			dollSelection.Close();
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
