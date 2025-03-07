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

		private void Start()
		{
			InitVolumeSliderValue();

			dungeonExitButton.onClick.AddListener(() =>
			{
				// HACK:
				Player.Instance.Object.ReceiveDamage(new DamageInfo(9999, DamageType.Critical));
				// DungeonManager.Instance.EndDungeon();
			});

			quitButton.onClick.AddListener(() =>
			{
				Application.Quit();
			});

			clearDataButton.onClick.AddListener(() =>
			{
				DataManager.Instance.PlayFabManager.CreateAndSavePlayerData();
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
			dungeonExitButton.gameObject.SetActive(UIManager.Instance.CurCanvas == MCanvasType.Dungeon);
		}

		public override void OnOpen()
		{
			TimeManager.Instance.Pause();
		}

		public override void OnClose()
		{
			TimeManager.Instance.Resume();
		}
	}
}
