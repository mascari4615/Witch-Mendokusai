using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	public class AudioManager : Singleton<AudioManager>
	{
		public enum BusType
		{
			Master = 0,
			BGM = 1,
			SFX = 2
		}

		private readonly Bus[] buses = new Bus[3];
		private EventInstance sfxVolumeTestEvent;
		private EventInstance bgmEvent;
		private PLAYBACK_STATE pbState;
		private readonly List<string> bgmTitles = new();
		private int bgmIndex = 0;

		protected override void Awake()
		{
			base.Awake();

			buses[(int)BusType.Master] = RuntimeManager.GetBus("bus:/");
			buses[(int)BusType.BGM] = RuntimeManager.GetBus("bus:/BGM");
			buses[(int)BusType.SFX] = RuntimeManager.GetBus("bus:/SFX");
			sfxVolumeTestEvent = RuntimeManager.CreateInstance("event:/SFX/SFXTest");

			// https://qa.fmod.com/t/get-master-bank-and-all-events/18635/8
			RuntimeManager.StudioSystem.getBankList(out Bank[] loadedBanks);
			foreach (Bank bank in loadedBanks)
			{
				bank.getEventList(out EventDescription[] eventDescriptions);
				foreach (EventDescription eventDesc in eventDescriptions)
				{
					eventDesc.getPath(out string eventPath);
					// Debug.Log(eventPath);

					if (eventPath.StartsWith("event:/BGM/"))
						bgmTitles.Add(eventPath);
				}
			}
		}

		private void Start()
		{
			UpdateVolume();
			PlayMusic(bgmTitles[Random.Range(0, bgmTitles.Count)]);
		}

		private void UpdateVolume()
		{
			buses[(int)BusType.Master].setVolume(GetVolume(BusType.Master));
			buses[(int)BusType.BGM].setVolume(GetVolume(BusType.BGM));
			buses[(int)BusType.SFX].setVolume(GetVolume(BusType.SFX));
		}

		private void Update()
		{
			// TODO : else if (DataManager.Instance.CurGameData.muteOnOutfocus)
			{
				// TODO : master.setVolume(0);
			}

			// if (SceneManager.GetActiveScene().buildIndex == 0)
			{
				bgmEvent.getPlaybackState(out pbState);

				if (pbState != PLAYBACK_STATE.STOPPED)
					return;

				Debug.Log("BGM End");
				PlayMusic(bgmTitles[bgmIndex = (bgmIndex + 1) % bgmTitles.Count]);
			}
		}

		public void StopMusic() => bgmEvent.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);

		public void PlayMusic(string eventPath)
		{
			bgmEvent.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
			bgmEvent = RuntimeManager.CreateInstance(eventPath);
			bgmEvent.start();
		}

		public float GetVolume(BusType busType)
		{
			string key = $"Volume{(int)busType}";

			if (!PlayerPrefs.HasKey(key))
				PlayerPrefs.SetFloat(key, 1);

			float volume = PlayerPrefs.GetFloat(key);
			return volume;
		}

		public void SetVolume(BusType busType, float volume)
		{
			var key = $"Volume{(int)busType}";
			PlayerPrefs.SetFloat(key, volume);
			buses[(int)busType].setVolume(volume);

			if (busType == BusType.SFX)
			{
				sfxVolumeTestEvent.getPlaybackState(out var playbackState);
				if (playbackState != PLAYBACK_STATE.PLAYING)
					sfxVolumeTestEvent.start();
			}
		}
	}
}