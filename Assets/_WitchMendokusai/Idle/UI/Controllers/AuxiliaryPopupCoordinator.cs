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
		private readonly OddsPopupController oddsPopup;
		private readonly GachaRevealController gachaReveal;
		private readonly UIContentSO content;
		private readonly Action closeSelectionPopups;
		private readonly Action requestRender;

		public AuxiliaryPopupCoordinator(
			VisualElement mapPopup,
			VisualElement goldPopup,
			VisualElement settingsPopupElement,
			VisualElement oddsPopupElement,
			VisualElement gachaPopupElement,
			VisualTreeAsset rowButtonAsset,
			VisualTreeAsset gachaCardAsset,
			HeroVisualPresenter heroVisualPresenter,
			RuntimeSettingsSO runtimeSettings,
			ModalController modalController,
			IdleSession session,
			UIContentSO content,
			Action closeSelectionPopups,
			Action<int> goToStage,
			Action requestRender,
			Action wipeAndRestart)
		{
			this.closeSelectionPopups = closeSelectionPopups;
			this.requestRender = requestRender;
			this.content = content;
			mapSelection = new MapSelectionController(
				mapPopup, rowButtonAsset, modalController,
				content, session.CanGoToStage, goToStage);
			goldDetails = new GoldDetailsController(goldPopup, modalController, content);
			settingsPopup = new SettingsPopupController(
				settingsPopupElement, modalController, session, content, requestRender, wipeAndRestart);
			oddsPopup = new OddsPopupController(oddsPopupElement, modalController, content);
			gachaReveal = new GachaRevealController(
				gachaPopupElement, modalController, content, heroVisualPresenter, gachaCardAsset, runtimeSettings);
		}

		public void Tick(float delta)
		{
			settingsPopup.Tick(delta);
			gachaReveal.Tick(delta);
		}

		/// <summary>뽑기 연출. 결과는 이미 판이 정한 것이고 여기는 보여주기만</summary>
		public void ShowGacha(System.Collections.Generic.IReadOnlyList<IdleHeroPull> result)
		{
			mapSelection.Close();
			closeSelectionPopups();
			goldDetails.Close();
			settingsPopup.Close();
			oddsPopup.Close();
			gachaReveal.Show(result);
			requestRender();
		}

		public void Render(IdleSnapshot snapshot)
		{
			goldDetails.Render(snapshot);
			settingsPopup.Render(snapshot);
			oddsPopup.Render(snapshot);
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
				oddsPopup.Close();
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
				oddsPopup.Close();
			});
			requestRender();
		}

		/// <summary>확률표. 상점 페이지의 버튼이 연다</summary>
		public void OpenOdds()
		{
			oddsPopup.Open(() =>
			{
				mapSelection.Close();
				closeSelectionPopups();
				goldDetails.Close();
				settingsPopup.Close();
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
				oddsPopup.Close();
			});
			requestRender();
		}

		public void CloseMap()
		{
			mapSelection.Close();
		}

		/// <summary>맵, 골드, 설정, 확률표 전부 닫기. Esc 첫 번째 몫</summary>
		public void CloseAll()
		{
			mapSelection.Close();
			CloseGoldAndSettings();
		}

		public void CloseGoldAndSettings()
		{
			goldDetails.Close();
			settingsPopup.Close();
			oddsPopup.Close();
			gachaReveal.Close();
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
