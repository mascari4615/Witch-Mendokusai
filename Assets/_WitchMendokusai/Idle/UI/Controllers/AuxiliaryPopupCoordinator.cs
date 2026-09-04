using System;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle.UI
{
	internal sealed class AuxiliaryPopupCoordinator
	{
		private readonly MapSelectionController mapSelection;
		private readonly GoldDetailsController goldDetails;
		private readonly SettingsPopupController settingsPopup;
		private readonly UIContentSO content;
		private readonly Action closeSelectionPopups;
		private readonly Action requestRender;

		public AuxiliaryPopupCoordinator(
			VisualElement mapPopup,
			VisualElement goldPopup,
			VisualElement settingsPopupElement,
			VisualTreeAsset rowButtonAsset,
			ModalController modalController,
			IdleSession session,
			UIContentSO content,
			Action closeSelectionPopups,
			Action<int> goToStage,
			Action requestRender)
		{
			this.closeSelectionPopups = closeSelectionPopups;
			this.requestRender = requestRender;
			this.content = content;
			mapSelection = new MapSelectionController(
				mapPopup, rowButtonAsset, modalController,
				content, session.CanGoToStage, goToStage);
			goldDetails = new GoldDetailsController(goldPopup, modalController, content);
			settingsPopup = new SettingsPopupController(
				settingsPopupElement, modalController, session, content, requestRender);
		}

		public void Tick(float delta)
		{
			settingsPopup.Tick(delta);
		}

		public void Render(IdleSnapshot snapshot)
		{
			goldDetails.Render(snapshot);
			settingsPopup.Render(snapshot);
			if (mapSelection.IsOpen)
			{
				mapSelection.Render(snapshot);
			}
		}

		public void OpenGold()
		{
			goldDetails.Open(() =>
			{
				mapSelection.Close();
				closeSelectionPopups();
				settingsPopup.Close();
			});
			requestRender();
		}

		public void OpenSettings()
		{
			settingsPopup.Open(() =>
			{
				mapSelection.Close();
				closeSelectionPopups();
				goldDetails.Close();
			});
			requestRender();
		}

		public void ToggleMap()
		{
			mapSelection.Toggle(() =>
			{
				closeSelectionPopups();
				goldDetails.Close();
				settingsPopup.Close();
			});
			requestRender();
		}

		public void CloseMap()
		{
			mapSelection.Close();
		}

		public void CloseGoldAndSettings()
		{
			goldDetails.Close();
			settingsPopup.Close();
		}

		public void ShowNote(string text, float seconds)
		{
			settingsPopup.ShowNote(text, seconds);
		}

		public void ShowAway(VisualElement popup, IdleAwayReport report)
		{
			if (report.HasAnything)
			{
				AwayReportPresenter.Bind(popup, report, content);
			}
		}
	}
}
