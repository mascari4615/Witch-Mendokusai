using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UISetting : UIPanel
	{
		[SerializeField] private Toggle framerateToggle;
		[SerializeField] private Slider masterVolumeSlider;
		[SerializeField] private Slider bgmVolumeSlider;
		[SerializeField] private Slider sfxVolumeSlider;
		[SerializeField] private Button dungeonExitButton;
		[SerializeField] private Button quitButton;
		[SerializeField] private Button clearDataButton;

		protected override void OnInit()
		{
			InitVolumeSliderValue();

			dungeonExitButton.onClick.AddListener(() =>
			{
				// HACK:
				UIManager.Instance.ToggleOverlayUI_Setting();
				Player.Instance.Object.ReceiveDamage(new DamageInfo(damage: 9999, DamageType.Critical, ignoreInvincible: true));
				// DungeonManager.Instance.EndDungeon();
			});

			quitButton.onClick.AddListener(() =>
			{
				Application.Quit();
			});

			clearDataButton.onClick.AddListener(() =>
			{
				DataManager.Instance.CreateNewGameData();
			});
		}

		private void InitVolumeSliderValue()
		{
			masterVolumeSlider.value = AudioManager.Instance.GetVolume(AudioManager.BusType.Master);
			bgmVolumeSlider.value = AudioManager.Instance.GetVolume(AudioManager.BusType.BGM);
			sfxVolumeSlider.value = AudioManager.Instance.GetVolume(AudioManager.BusType.SFX);
		}

		public void UpdateVolume(int busType) => UpdateVolume((AudioManager.BusType)busType);
		public void UpdateVolume(AudioManager.BusType busType)
		{
			switch (busType)
			{
				case AudioManager.BusType.Master:
					AudioManager.Instance.SetVolume(AudioManager.BusType.Master, masterVolumeSlider.value);
					break;
				case AudioManager.BusType.BGM:
					AudioManager.Instance.SetVolume(AudioManager.BusType.BGM, bgmVolumeSlider.value);
					break;
				case AudioManager.BusType.SFX:
					AudioManager.Instance.SetVolume(AudioManager.BusType.SFX, sfxVolumeSlider.value);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(busType), busType, null);
			}
		}

		public void ToggleFramerate()
		{
			Application.targetFrameRate = framerateToggle.isOn ? 60 : 30;
		}

		public override void UpdateUI()
		{
			// HACK: World 씬 에서만 아래 실행
			// TODO: 분리해야 할 듯 (= Common(or Dungeon) Pause Panel 만들기) - KarmoDDrine 2025-08-08
			if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "World")
				dungeonExitButton.transform.parent.gameObject.SetActive(DungeonManager.Instance.IsDungeon);
			else
				dungeonExitButton.transform.parent.gameObject.SetActive(false);
		}

		protected override void OnOpen()
		{
			TimeManager.Instance.Pause(gameObject);
		}

		protected override void OnClose()
		{
			TimeManager.Instance.Resume(gameObject);
		}
	}
}
