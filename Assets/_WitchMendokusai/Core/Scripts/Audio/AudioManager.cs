using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	public class AudioManager : MonoBehaviour
	{
		public static AudioManager Instance { get; private set; }

		public static bool TryGetExistingInstance(out AudioManager mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		public enum BusType
		{
			Master = 0,
			BGM = 1,
			SFX = 2
		}

		private readonly Bus[] buses = new Bus[3];
		private EventInstance sfxVolumeTestEvent;
		private EventInstance bgmEvent;
		private EventInstance ambientEvent;
		private PLAYBACK_STATE pbState;
		private readonly List<string> bgmTitles = new();
		private int bgmIndex = 0;
		private bool audioReady; // FMOD 뱅크 로드 성공 여부. false 면 모든 오디오 no-op(부팅·게임 계속, 무음).

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;

			// 오디오는 부팅 필수의존 아님. FMOD 의 에디터 Play 뱅크 로딩은 *메인 에디터*에서만 돌아
			// MPPM 가상 플레이어엔 뱅크가 안 올라옴 → GetBus 가 throw → 예전엔 DI 컨테이너 빌드 abort
			// → 게임 전체 부팅 실패였음(WM-191 멀티 테스트서 발견). 뱅크 없으면 audioReady=false 로 무음
			// 진행(크래시 X — 뱅크 있는 메인 에디터/standalone 에선 audioReady=true → 동작 100% 동일=회귀 0).
			try
			{
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
						if (eventPath.StartsWith("event:/BGM/"))
							bgmTitles.Add(eventPath);
					}
				}
				audioReady = true;
			}
			catch (System.Exception exception)
			{
				audioReady = false;
				Debug.LogWarning($"[AudioManager] FMOD 뱅크 미로드 — 오디오 비활성(무음)으로 진행, 부팅 계속. "
					+ $"{exception.GetType().Name}: {exception.Message}");
			}
		}

		private void OnDestroy()
		{
			if (Instance == this)
				Instance = null;
		}

		private void Start()
		{
			if (audioReady == false)
				return;
			UpdateVolume();
			if (bgmTitles.Count > 0)
				PlayMusic(bgmTitles[Random.Range(0, bgmTitles.Count)]);
		}

		private void UpdateVolume()
		{
			if (audioReady == false)
				return;
			buses[(int)BusType.Master].setVolume(GetVolume(BusType.Master));
			buses[(int)BusType.BGM].setVolume(GetVolume(BusType.BGM));
			buses[(int)BusType.SFX].setVolume(GetVolume(BusType.SFX));
		}

		/// <summary>
		/// 소리를 「듣는 귀」가 카메라에 붙어 있는지 확인하고, 없으면 붙인다.
		///
		/// ★ 귀가 없으면 방향·거리감이 통째로 사라진다 — 소리는 나는데 *어디서 나는지*가 없다.
		///   무대(씬)마다 사람이 손으로 붙이게 돼 있어서, 2026-08-07 실기 기준 **두 무대에만** 있었다.
		///   빠뜨렸다는 신호가 로그 경고 한 줄뿐이라 아무도 안 본다.
		/// ★ 왜 코드로 보장하나: 새 무대를 만들 때마다 다시 빠질 자리다. 무대 수가 늘수록 확실해지는
		///   쪽이 옳다. 이미 붙어 있으면 아무것도 안 한다(둘이 되면 FMOD 가 따로 경고한다).
		/// ★ 카메라는 무대가 바뀌면 갈린다 — 그래서 한 번이 아니라 바뀔 때마다 확인한다.
		/// </summary>
		private Camera lastListenerCamera;

		private void EnsureStudioListener()
		{
			Camera camera = Camera.main;
			if (camera == null || camera == lastListenerCamera)
				return;

			lastListenerCamera = camera;
			if (camera.GetComponent<StudioListener>() != null
				|| camera.GetComponentInChildren<StudioListener>(true) != null)
			{
				return;
			}

			camera.gameObject.AddComponent<StudioListener>();
			Debug.Log("[AudioManager] 카메라에 소리 듣는 귀가 없어 붙였다 — 3D 거리감이 살아난다.");
		}

		private void Update()
		{
			EnsureStudioListener();

			if (audioReady == false || bgmTitles.Count == 0)
				return;

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

		public void StopMusic()
		{
			if (audioReady == false)
				return;
			bgmEvent.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
		}

		public void PlayAmbient(string eventPath)
		{
			if (audioReady == false)
				return;
			ambientEvent.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
			ambientEvent.release();
			if (string.IsNullOrEmpty(eventPath) == false)
			{
				try
				{
					ambientEvent = RuntimeManager.CreateInstance(eventPath);
					ambientEvent.start();
				}
				catch (EventNotFoundException)
				{
					// FMOD 뱅크에 등록 안 된 event — silent skip (warning 로그). WeatherSO SfxKey
					// (e.g. weather_clear) 가 실제 FMOD event 명과 mismatch 일 때 Play 중단 방지.
					Debug.LogWarning($"[AudioManager] FMOD event not found: '{eventPath}' — ambient skip");
				}
			}
		}

		public void StopAmbient()
		{
			if (audioReady == false)
				return;
			ambientEvent.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
			ambientEvent.release();
		}

		public void PlaySfx(string eventPath)
		{
			if (audioReady == false)
				return;
			if (string.IsNullOrEmpty(eventPath) == false)
				RuntimeManager.PlayOneShot(eventPath);
		}

		public void PlayMusic(string eventPath)
		{
			if (audioReady == false)
				return;
			bgmEvent.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
			bgmEvent = RuntimeManager.CreateInstance(eventPath);
			bgmEvent.start();
		}

		public float GetVolume(BusType busType)
		{
			string key = $"Volume{(int)busType}";

			if (PlayerPrefs.HasKey(key) == false)
				PlayerPrefs.SetFloat(key, 1);

			float volume = PlayerPrefs.GetFloat(key);
			return volume;
		}

		public void SetVolume(BusType busType, float volume)
		{
			string key = $"Volume{(int)busType}";
			PlayerPrefs.SetFloat(key, volume);
			if (audioReady == false)
				return;
			buses[(int)busType].setVolume(volume);

			if (busType == BusType.SFX)
			{
				sfxVolumeTestEvent.getPlaybackState(out PLAYBACK_STATE playbackState);
				if (playbackState != PLAYBACK_STATE.PLAYING)
					sfxVolumeTestEvent.start();
			}
		}
	}
}
